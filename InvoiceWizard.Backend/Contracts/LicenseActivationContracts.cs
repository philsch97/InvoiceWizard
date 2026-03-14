using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class SubscriptionPlanListItemDto
{
    public int SubscriptionPlanId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int MaxUsers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxCustomers { get; set; }
    public bool IncludesMobileAccess { get; set; }
}

public class LicenseActivationDto
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
}

public class CreateLicenseActivationRequest
{
    [Required]
    [MaxLength(100)]
    public string PlanCode { get; set; } = "";

    [EmailAddress]
    [MaxLength(320)]
    public string? CustomerEmail { get; set; }

    public DateTime? ValidUntil { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxProjectsOverride { get; set; }
    public int? MaxCustomersOverride { get; set; }
    public bool? IncludesMobileAccessOverride { get; set; }
    [MaxLength(50)]
    public string BillingCycle { get; set; } = "Monthly";
    [Range(0, 999999.99)]
    public decimal PriceNet { get; set; }
    public bool RenewsAutomatically { get; set; } = true;
}

public class TenantLicenseAdminDto
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
}

public class UpdateTenantLicenseRequest
{
    [Required]
    [MaxLength(100)]
    public string PlanCode { get; set; } = "";
    [MaxLength(50)]
    public string BillingCycle { get; set; } = "Monthly";
    [Range(0, 999999.99)]
    public decimal PriceNet { get; set; }
    public bool RenewsAutomatically { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? GraceUntil { get; set; }
    public int? MaxUsersOverride { get; set; }
    public int? MaxProjectsOverride { get; set; }
    public int? MaxCustomersOverride { get; set; }
    public bool? IncludesMobileAccessOverride { get; set; }
    public bool IsActive { get; set; } = true;
}
