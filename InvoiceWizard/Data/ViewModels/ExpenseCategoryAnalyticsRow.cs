namespace InvoiceWizard.Data.ViewModels;

public class ExpenseCategoryAnalyticsRow
{
    public string AccountingCategory { get; set; } = "";
    public decimal Amount { get; set; }

    public string AccountingCategoryLabel => AccountingCategory switch
    {
        "Tools" => "Werkzeug",
        "Services" => "Dienstleistungen",
        "Office" => "Buero",
        "Vehicle" => "Fahrzeug",
        "Other" => "Sonstiges",
        _ => "Material und Waren"
    };
}
