namespace InvoiceWizard.Backend.Domain;

public static class TenantRoles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
}

public class Tenant
{
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<UserTenantMembership> Memberships { get; set; } = new();
    public List<TenantLicense> Licenses { get; set; } = new();
}

public class AppUser
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsPlatformAdmin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public List<UserTenantMembership> Memberships { get; set; } = new();
}

public class UserTenantMembership
{
    public int UserTenantMembershipId { get; set; }
    public int AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Role { get; set; } = TenantRoles.Employee;
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SubscriptionPlan
{
    public int SubscriptionPlanId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TenantLicense> Licenses { get; set; } = new();
    public List<LicenseActivation> LicenseActivations { get; set; } = new();
}

public class TenantLicense
{
    public int TenantLicenseId { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxProjectsOverride { get; set; }
    public int? MaxCustomersOverride { get; set; }
    public bool? IncludesMobileAccessOverride { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class LicenseActivation
{
    public int LicenseActivationId { get; set; }
    public string ActivationCode { get; set; } = "";
    public int SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    public string CustomerEmail { get; set; } = "";
    public DateTime? ValidUntil { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxProjectsOverride { get; set; }
    public int? MaxCustomersOverride { get; set; }
    public bool? IncludesMobileAccessOverride { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public int? UsedByAppUserId { get; set; }
    public AppUser? UsedByAppUser { get; set; }
    public int CreatedByAppUserId { get; set; }
    public AppUser CreatedByAppUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
