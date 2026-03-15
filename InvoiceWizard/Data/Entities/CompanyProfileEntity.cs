namespace InvoiceWizard.Data.Entities;

public class CompanyProfileEntity
{
    public string CompanyName { get; set; } = "";
    public string CompanyStreet { get; set; } = "";
    public string CompanyHouseNumber { get; set; } = "";
    public string CompanyPostalCode { get; set; } = "";
    public string CompanyCity { get; set; } = "";
    public string CompanyEmailAddress { get; set; } = "";
    public string CompanyPhoneNumber { get; set; } = "";
    public string TaxNumber { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankIban { get; set; } = "";
    public string BankBic { get; set; } = "";
    public int NextRevenueInvoiceNumber { get; set; } = 1;
    public int NextCustomerNumber { get; set; } = 1;
    public string RevenueInvoiceNumberPreview { get; set; } = "";

    public string CompanyAddress => string.Join(", ", new[]
    {
        string.Join(" ", new[] { CompanyStreet, CompanyHouseNumber }.Where(x => !string.IsNullOrWhiteSpace(x))),
        string.Join(" ", new[] { CompanyPostalCode, CompanyCity }.Where(x => !string.IsNullOrWhiteSpace(x)))
    }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
