namespace InvoiceWizard.Web.Models;

public class BootstrapStateModel
{
    public bool HasUsers { get; set; }
    public bool HasTenants { get; set; }
}

public class LoginRequestModel
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class StoredLoginCredentials
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class BootstrapAdminRequestModel
{
    public string TenantName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthSessionModel
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public AuthUserModel User { get; set; } = new();
    public AuthTenantModel Tenant { get; set; } = new();
    public AuthLicenseModel? License { get; set; }
}

public class AuthUserModel
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class AuthTenantModel
{
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class AuthLicenseModel
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

public class TenantUserModel
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateTenantUserModel
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "Employee";
}

public class UpdateTenantUserModel
{
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Employee";
    public bool IsActive { get; set; } = true;
    public string? Password { get; set; }
}
