using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class SoneparConnectionStatusDto
{
    public bool IsConfigured { get; set; }
    public string Username { get; set; } = "";
    public string CustomerNumberMasked { get; set; } = "";
    public string ClientIdMasked { get; set; } = "";
    public string OrganizationId { get; set; } = "07";
    public string OmdVersion { get; set; } = "9.0.1";
    public string TokenUrl { get; set; } = "";
    public string OpenMasterDataBaseUrl { get; set; } = "";
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
}

public class SaveSoneparConnectionRequest
{
    [Required]
    public string Username { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
    [Required]
    public string CustomerNumber { get; set; } = "";
    [Required]
    public string ClientId { get; set; } = "";
    [Required]
    public string OrganizationId { get; set; } = "07";
    [Required]
    public string OmdVersion { get; set; } = "9.0.1";
    [Required]
    public string TokenUrl { get; set; } = "";
    [Required]
    public string OpenMasterDataBaseUrl { get; set; } = "";
}

public class SoneparProductSearchRequest
{
    [Required]
    public string SearchType { get; set; } = "SupplierPid";
    [Required]
    public string Query { get; set; } = "";
}

public class SoneparProductSearchResponse
{
    public string SearchType { get; set; } = "";
    public string Query { get; set; } = "";
    public int ProductCount { get; set; }
    public List<SoneparProductDto> Products { get; set; } = new();
}

public class SoneparProductDto
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
