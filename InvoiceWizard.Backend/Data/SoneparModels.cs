namespace InvoiceWizard.Backend.Domain;

public class TenantSoneparConnection
{
    public int TenantSoneparConnectionId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Username { get; set; } = "";
    public string PasswordCipherText { get; set; } = "";
    public string CustomerNumberCipherText { get; set; } = "";
    public string ClientIdCipherText { get; set; } = "";
    public string OrganizationId { get; set; } = "07";
    public string OmdVersion { get; set; } = "9.0.1";
    public string TokenUrl { get; set; } = "https://www.sonepar.de/api/authentication/v1/oauth2/token";
    public string OpenMasterDataBaseUrl { get; set; } = "https://www.sonepar.de/open-masterdata/v1";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidatedAtUtc { get; set; }
}
