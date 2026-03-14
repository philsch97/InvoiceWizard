namespace InvoiceWizard.Data.ViewModels;

public class SubscriptionPlanViewModel
{
    public int SubscriptionPlanId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
    public string DisplayLabel => $"{Name} ({MaxUsers} Benutzer / {MaxProjects} Projekte / {MaxCustomers} Kunden)";
    public override string ToString() => DisplayLabel;
}

public class LicenseActivationViewModel
{
    public int LicenseActivationId { get; set; }
    public string ActivationCode { get; set; } = "";
    public string PlanCode { get; set; } = "";
    public string PlanName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public DateTime? ValidUntil { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByDisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByDisplayName { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
    public string BillingCycle { get; set; } = "";
    public decimal PriceNet { get; set; }
    public bool RenewsAutomatically { get; set; }
    public string StatusLabel => IsUsed ? $"Verwendet{(UsedAt.HasValue ? $" am {UsedAt:dd.MM.yyyy HH:mm}" : string.Empty)}" : "Offen";
    public string LimitsLabel => $"{MaxUsers} Benutzer / {MaxProjects} Projekte / {MaxCustomers} Kunden";
    public string BillingLabel => $"{BillingCycleLabel} / {PriceNet:N2} EUR";
    public string BillingCycleLabel => BillingCycle switch
    {
        "Yearly" => "Jaehrlich",
        "Manual" => "Manuell",
        _ => "Monatlich"
    };
}

public class ManagedTenantLicenseViewModel
{
    public int TenantLicenseId { get; set; }
    public int TenantId { get; set; }
    public string TenantName { get; set; } = "";
    public string TenantSlug { get; set; } = "";
    public string PlanCode { get; set; } = "";
    public string PlanName { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
    public string BillingCycle { get; set; } = "";
    public decimal PriceNet { get; set; }
    public bool RenewsAutomatically { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? GraceUntil { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = "";
    public string LimitsLabel => $"{MaxUsers} / {MaxProjects} / {MaxCustomers}";
    public string BillingCycleLabel => BillingCycle switch
    {
        "Yearly" => "Jaehrlich",
        "Manual" => "Manuell",
        _ => "Monatlich"
    };
    public string BillingLabel => $"{BillingCycleLabel} / {PriceNet:N2} EUR";
}
