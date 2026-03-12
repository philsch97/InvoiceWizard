namespace InvoiceWizard.Data.ViewModels;

public class AuthBootstrapStateViewModel
{
    public bool HasUsers { get; set; }
    public bool HasTenants { get; set; }
}

public class AuthSessionViewModel
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public AuthUserViewModel User { get; set; } = new();
    public AuthTenantViewModel Tenant { get; set; } = new();
    public AuthLicenseViewModel? License { get; set; }
}

public class AuthUserViewModel
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class AuthTenantViewModel
{
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class AuthLicenseViewModel
{
    public int TenantLicenseId { get; set; }
    public string PlanCode { get; set; } = "";
    public string PlanName { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsActive { get; set; }
}
