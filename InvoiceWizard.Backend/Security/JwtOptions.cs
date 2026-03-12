namespace InvoiceWizard.Backend.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "InvoiceWizard";
    public string Audience { get; set; } = "InvoiceWizard.Clients";
    public string SigningKey { get; set; } = "change-this-development-signing-key-please";
    public int AccessTokenMinutes { get; set; } = 480;
}
