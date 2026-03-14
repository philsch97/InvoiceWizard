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
}
