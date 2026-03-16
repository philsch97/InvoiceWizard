using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Services;

public interface ISoneparService
{
    Task<SoneparConnectionStatusDto> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<SoneparConnectionStatusDto> SaveConnectionAsync(int tenantId, SaveSoneparConnectionRequest request, CancellationToken cancellationToken = default);
    Task DeleteConnectionAsync(int tenantId, CancellationToken cancellationToken = default);
    Task<SoneparProductSearchResponse> SearchProductsAsync(int tenantId, SoneparProductSearchRequest request, CancellationToken cancellationToken = default);
}

public class SoneparService(
    InvoiceWizardDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    HttpClient httpClient) : ISoneparService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("InvoiceWizard.SoneparCredentials.v1");

    public async Task<SoneparConnectionStatusDto> GetStatusAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await db.TenantSoneparConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        return connection is null
            ? new SoneparConnectionStatusDto()
            : MapStatus(connection);
    }

    public async Task<SoneparConnectionStatusDto> SaveConnectionAsync(int tenantId, SaveSoneparConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(request);
        var token = await RequestAccessTokenAsync(normalized, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Sonepar hat kein Zugriffstoken zurueckgegeben.");
        }

        var connection = await db.TenantSoneparConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (connection is null)
        {
            connection = new TenantSoneparConnection
            {
                TenantId = tenantId
            };
            db.TenantSoneparConnections.Add(connection);
        }

        connection.Username = normalized.Username;
        connection.PasswordCipherText = _protector.Protect(normalized.Password);
        connection.CustomerNumberCipherText = _protector.Protect(normalized.CustomerNumber);
        connection.ClientIdCipherText = _protector.Protect(normalized.ClientId);
        connection.OrganizationId = normalized.OrganizationId;
        connection.OmdVersion = normalized.OmdVersion;
        connection.TokenUrl = normalized.TokenUrl;
        connection.OpenMasterDataBaseUrl = normalized.OpenMasterDataBaseUrl;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        connection.LastValidatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return MapStatus(connection);
    }

    public async Task DeleteConnectionAsync(int tenantId, CancellationToken cancellationToken = default)
    {
        var connection = await db.TenantSoneparConnections.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (connection is null)
        {
            return;
        }

        db.TenantSoneparConnections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SoneparProductSearchResponse> SearchProductsAsync(int tenantId, SoneparProductSearchRequest request, CancellationToken cancellationToken = default)
    {
        var connection = await db.TenantSoneparConnections.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Fuer diesen Mandanten ist noch keine Sonepar-Anmeldung hinterlegt.");
        }

        var query = (request.Query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new InvalidOperationException("Bitte einen Suchbegriff eingeben.");
        }

        var credentials = Decrypt(connection);
        var token = await RequestAccessTokenAsync(credentials, cancellationToken);
        var url = BuildSearchUrl(credentials, NormalizeSearchType(request.SearchType), query);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildRemoteErrorMessage(body, response.ReasonPhrase), null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var products = ParseProducts(document.RootElement).ToList();
        return new SoneparProductSearchResponse
        {
            SearchType = NormalizeSearchType(request.SearchType),
            Query = query,
            ProductCount = products.Count,
            Products = products
        };
    }

    private async Task<string> RequestAccessTokenAsync(SoneparCredentials credentials, CancellationToken cancellationToken)
    {
        var formValues = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("username", credentials.Username),
            new("password", credentials.Password),
            new("client_id", credentials.ClientId),
            new("customerNumber", credentials.CustomerNumber),
            new("customernumber", credentials.CustomerNumber),
            new("customer_number", credentials.CustomerNumber)
        };

        var tokenUrl = AppendOrganizationId(credentials.TokenUrl, credentials.OrganizationId);
        using var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(formValues), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildRemoteErrorMessage(body, response.ReasonPhrase), null, response.StatusCode);
        }

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (TryGetString(document.RootElement, out var token, "access_token", "accessToken", "token"))
        {
            return token;
        }

        return string.Empty;
    }

    private static string BuildSearchUrl(SoneparCredentials credentials, string searchType, string query)
    {
        var baseUrl = credentials.OpenMasterDataBaseUrl.TrimEnd('/');
        var version = Uri.EscapeDataString(credentials.OmdVersion);
        var encodedQuery = Uri.EscapeDataString(query);
        var org = Uri.EscapeDataString(credentials.OrganizationId);

        return searchType switch
        {
            "Gtin" => $"{baseUrl}/product/byGTIN/{version}?OrganizationId={org}&gtin={encodedQuery}",
            "ManufacturerData" => $"{baseUrl}/product/byManufacturerData/{version}?OrganizationId={org}&query={encodedQuery}",
            _ => $"{baseUrl}/product/bySupplierPID/{version}?OrganizationId={org}&supplierPID={encodedQuery}"
        };
    }

    private static string AppendOrganizationId(string tokenUrl, string organizationId)
    {
        var separator = tokenUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return tokenUrl.Contains("OrganizationId=", StringComparison.OrdinalIgnoreCase)
            ? tokenUrl
            : $"{tokenUrl}{separator}OrganizationId={Uri.EscapeDataString(organizationId)}";
    }

    private IEnumerable<SoneparProductDto> ParseProducts(JsonElement root)
    {
        foreach (var element in EnumerateCandidateProducts(root))
        {
            yield return new SoneparProductDto
            {
                SupplierPid = FindString(element, "supplierPID", "supplierPid", "productId", "articleNumber"),
                Gtin = FindString(element, "gtin", "GTIN", "ean"),
                ManufacturerName = FindString(element, "manufacturerName", "manufacturer"),
                ManufacturerPartNumber = FindString(element, "manufacturerPartNumber", "partNumber", "manufacturerNumber"),
                DescriptionShort = FindString(element, "descriptionShort", "shortDescription", "description", "name"),
                DescriptionLong = FindString(element, "descriptionLong", "longDescription", "marketingText", "productText"),
                Unit = FindString(element, "salesUnit", "unit", "unitOfMeasure"),
                NetPrice = FindDecimal(element, "netPrice", "customerNetPrice", "purchasePrice", "price"),
                PriceQuantity = FindDecimal(element, "priceQuantity", "quantity", "priceBaseQuantity"),
                Currency = FindString(element, "currency", "priceCurrency"),
                MetalSurcharge = FindDecimal(element, "metalSurcharge", "metalSurchargeValue"),
                RawJson = JsonSerializer.Serialize(element, JsonOptions)
            };
        }
    }

    private static IEnumerable<JsonElement> EnumerateCandidateProducts(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        foreach (var propertyName in new[] { "products", "items", "data", "results", "result" })
        {
            if (TryGetProperty(root, propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.EnumerateArray())
                    {
                        yield return item;
                    }

                    yield break;
                }

                if (property.ValueKind == JsonValueKind.Object)
                {
                    yield return property;
                    yield break;
                }
            }
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
        }
    }

    private static string FindString(JsonElement element, params string[] names)
    {
        return TryGetStringRecursive(element, out var value, names) ? value : string.Empty;
    }

    private static decimal? FindDecimal(JsonElement element, params string[] names)
    {
        return TryGetDecimalRecursive(element, out var value, names) ? value : null;
    }

    private static bool TryGetStringRecursive(JsonElement element, out string value, params string[] names)
    {
        if (TryGetString(element, out value, names))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                {
                    if (TryGetStringRecursive(property.Value, out value, names))
                    {
                        return true;
                    }
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetStringRecursive(item, out value, names))
                {
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDecimalRecursive(JsonElement element, out decimal value, params string[] names)
    {
        if (TryGetDecimal(element, out value, names))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                {
                    if (TryGetDecimalRecursive(property.Value, out value, names))
                    {
                        return true;
                    }
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetDecimalRecursive(item, out value, names))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var candidate in element.EnumerateObject())
            {
                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    return true;
                }
            }
        }

        property = default;
        return false;
    }

    private static bool TryGetString(JsonElement element, out string value, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var property))
            {
                value = property.ValueKind switch
                {
                    JsonValueKind.String => property.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.ToString(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    _ => string.Empty
                };

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDecimal(JsonElement element, out decimal value, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
                {
                    return true;
                }

                if (property.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string NormalizeSearchType(string? searchType)
    {
        var normalized = (searchType ?? string.Empty).Trim();
        return normalized switch
        {
            "Gtin" => "Gtin",
            "ManufacturerData" => "ManufacturerData",
            _ => "SupplierPid"
        };
    }

    private static SoneparCredentials Normalize(SaveSoneparConnectionRequest request)
    {
        return new SoneparCredentials
        {
            Username = request.Username.Trim(),
            Password = request.Password,
            CustomerNumber = request.CustomerNumber.Trim(),
            ClientId = request.ClientId.Trim(),
            OrganizationId = string.IsNullOrWhiteSpace(request.OrganizationId) ? "07" : request.OrganizationId.Trim(),
            OmdVersion = string.IsNullOrWhiteSpace(request.OmdVersion) ? "9.0.1" : request.OmdVersion.Trim(),
            TokenUrl = request.TokenUrl.Trim().TrimEnd('/'),
            OpenMasterDataBaseUrl = request.OpenMasterDataBaseUrl.Trim().TrimEnd('/')
        };
    }

    private SoneparCredentials Decrypt(TenantSoneparConnection connection)
    {
        return new SoneparCredentials
        {
            Username = connection.Username,
            Password = _protector.Unprotect(connection.PasswordCipherText),
            CustomerNumber = _protector.Unprotect(connection.CustomerNumberCipherText),
            ClientId = _protector.Unprotect(connection.ClientIdCipherText),
            OrganizationId = connection.OrganizationId,
            OmdVersion = connection.OmdVersion,
            TokenUrl = connection.TokenUrl,
            OpenMasterDataBaseUrl = connection.OpenMasterDataBaseUrl
        };
    }

    private static SoneparConnectionStatusDto MapStatus(TenantSoneparConnection connection)
    {
        return new SoneparConnectionStatusDto
        {
            IsConfigured = true,
            Username = connection.Username,
            CustomerNumberMasked = MaskProtectedValue(connection.CustomerNumberCipherText),
            ClientIdMasked = MaskProtectedValue(connection.ClientIdCipherText),
            OrganizationId = connection.OrganizationId,
            OmdVersion = connection.OmdVersion,
            TokenUrl = connection.TokenUrl,
            OpenMasterDataBaseUrl = connection.OpenMasterDataBaseUrl,
            UpdatedAtUtc = connection.UpdatedAtUtc,
            LastValidatedAtUtc = connection.LastValidatedAtUtc
        };
    }

    private static string MaskProtectedValue(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return string.Empty;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(protectedValue)));
        return $"***{hash[^4..]}";
    }

    private static string BuildRemoteErrorMessage(string body, string? reasonPhrase)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Sonepar-Anfrage fehlgeschlagen ({reasonPhrase ?? "unbekannt"}).";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (TryGetStringRecursive(document.RootElement, out var message, "error_description", "error", "message", "title"))
            {
                return message;
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private sealed class SoneparCredentials
    {
        public string Username { get; init; } = "";
        public string Password { get; init; } = "";
        public string CustomerNumber { get; init; } = "";
        public string ClientId { get; init; } = "";
        public string OrganizationId { get; init; } = "";
        public string OmdVersion { get; init; } = "";
        public string TokenUrl { get; init; } = "";
        public string OpenMasterDataBaseUrl { get; init; } = "";
    }
}
