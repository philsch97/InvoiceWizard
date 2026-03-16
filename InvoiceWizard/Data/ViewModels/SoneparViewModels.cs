using System.Collections.ObjectModel;

namespace InvoiceWizard.Data.ViewModels;

public class SoneparConnectionViewModel
{
    public bool IsConfigured { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string CustomerNumber { get; set; } = "";
    public string CustomerNumberMasked { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientIdMasked { get; set; } = "";
    public string OrganizationId { get; set; } = "07";
    public string OmdVersion { get; set; } = "9.0.1";
    public string TokenUrl { get; set; } = "https://www.sonepar.de/api/authentication/v1/oauth2/token";
    public string OpenMasterDataBaseUrl { get; set; } = "https://www.sonepar.de/open-masterdata/v1";
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
}

public class SoneparSearchResultViewModel
{
    public string SearchType { get; set; } = "SupplierPid";
    public string Query { get; set; } = "";
    public ObservableCollection<SoneparProductViewModel> Products { get; set; } = new();
}

public class SoneparProductViewModel
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

    public string NetPriceLabel => NetPrice.HasValue
        ? $"{NetPrice.Value:N2} {Currency}".Trim()
        : "-";
}
