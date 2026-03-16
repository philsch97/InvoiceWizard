using System.Collections.ObjectModel;
using System.Net.Http.Json;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard.Services;

public partial class BackendApiClient
{
    public async Task<SoneparConnectionViewModel> GetSoneparConnectionAsync()
    {
        var item = await _httpClient.GetFromJsonAsync<SoneparConnectionDto>("api/sonepar/connection", _jsonOptions) ?? new SoneparConnectionDto();
        return MapSoneparConnection(item);
    }

    public async Task<SoneparConnectionViewModel> SaveSoneparConnectionAsync(SoneparConnectionViewModel connection)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sonepar/connection", new
        {
            username = connection.Username,
            password = connection.Password,
            customerNumber = connection.CustomerNumber,
            clientId = connection.ClientId,
            organizationId = connection.OrganizationId,
            omdVersion = connection.OmdVersion,
            tokenUrl = connection.TokenUrl,
            openMasterDataBaseUrl = connection.OpenMasterDataBaseUrl
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<SoneparConnectionDto>(_jsonOptions) ?? new SoneparConnectionDto();
        return MapSoneparConnection(item);
    }

    public async Task DeleteSoneparConnectionAsync()
    {
        var response = await _httpClient.DeleteAsync("api/sonepar/connection");
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<SoneparSearchResultViewModel> SearchSoneparProductsAsync(string searchType, string query)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sonepar/search", new
        {
            searchType,
            query
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<SoneparSearchResponseDto>(_jsonOptions) ?? new SoneparSearchResponseDto();
        return new SoneparSearchResultViewModel
        {
            SearchType = item.SearchType,
            Query = item.Query,
            Products = new ObservableCollection<SoneparProductViewModel>(item.Products.Select(MapSoneparProduct))
        };
    }

    private static SoneparConnectionViewModel MapSoneparConnection(SoneparConnectionDto item)
    {
        return new SoneparConnectionViewModel
        {
            IsConfigured = item.IsConfigured,
            Username = item.Username,
            CustomerNumberMasked = item.CustomerNumberMasked,
            ClientIdMasked = item.ClientIdMasked,
            OrganizationId = string.IsNullOrWhiteSpace(item.OrganizationId) ? "07" : item.OrganizationId,
            OmdVersion = string.IsNullOrWhiteSpace(item.OmdVersion) ? "9.0.1" : item.OmdVersion,
            TokenUrl = string.IsNullOrWhiteSpace(item.TokenUrl) ? "https://www.sonepar.de/api/authentication/v1/oauth2/token" : item.TokenUrl,
            OpenMasterDataBaseUrl = string.IsNullOrWhiteSpace(item.OpenMasterDataBaseUrl) ? "https://www.sonepar.de/open-masterdata/v1" : item.OpenMasterDataBaseUrl,
            UpdatedAtUtc = item.UpdatedAtUtc,
            LastValidatedAtUtc = item.LastValidatedAtUtc
        };
    }

    private static SoneparProductViewModel MapSoneparProduct(SoneparProductDto item)
    {
        return new SoneparProductViewModel
        {
            SupplierPid = item.SupplierPid,
            Gtin = item.Gtin,
            ManufacturerName = item.ManufacturerName,
            ManufacturerPartNumber = item.ManufacturerPartNumber,
            DescriptionShort = item.DescriptionShort,
            DescriptionLong = item.DescriptionLong,
            Unit = item.Unit,
            NetPrice = item.NetPrice,
            PriceQuantity = item.PriceQuantity,
            Currency = item.Currency,
            MetalSurcharge = item.MetalSurcharge,
            RawJson = item.RawJson
        };
    }

    private class SoneparConnectionDto
    {
        public bool IsConfigured { get; set; }
        public string Username { get; set; } = "";
        public string CustomerNumberMasked { get; set; } = "";
        public string ClientIdMasked { get; set; } = "";
        public string OrganizationId { get; set; } = "";
        public string OmdVersion { get; set; } = "";
        public string TokenUrl { get; set; } = "";
        public string OpenMasterDataBaseUrl { get; set; } = "";
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? LastValidatedAtUtc { get; set; }
    }

    private class SoneparSearchResponseDto
    {
        public string SearchType { get; set; } = "";
        public string Query { get; set; } = "";
        public int ProductCount { get; set; }
        public List<SoneparProductDto> Products { get; set; } = [];
    }

    private class SoneparProductDto
    {
        public string SupplierPid { get; set; } = "";
        public string Gtin { get; set; } = "";
        public string ManufacturerName { get; set; } = "";
        public string ManufacturerPartNumber { get; set; } = "";
        public string DescriptionShort { get; set; } = "";
        public string DescriptionLong { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal? NetPrice { get; set; }
        public decimal? PriceQuantity { get; set; }
        public string Currency { get; set; } = "";
        public decimal? MetalSurcharge { get; set; }
        public string RawJson { get; set; } = "";
    }
}
