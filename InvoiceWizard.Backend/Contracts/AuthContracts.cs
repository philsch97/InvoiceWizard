using System.ComponentModel.DataAnnotations;

namespace InvoiceWizard.Backend.Contracts;

public class BootstrapStateDto
{
    public bool HasUsers { get; set; }
    public bool HasTenants { get; set; }
}

public class BootstrapAdminRequest
{
    [Required]
    [MaxLength(200)]
    public string TenantName { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = "";

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = "";

    [Required]
    [MaxLength(200)]
    public string Password { get; set; } = "";
}

public class AuthResponseDto
{
    public string AccessToken { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public AuthUserDto User { get; set; } = new();
    public AuthTenantDto Tenant { get; set; } = new();
    public AuthLicenseDto? License { get; set; }
}

public class AuthUserDto
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class AuthTenantDto
{
    public int TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}

public class AuthLicenseDto
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

public class TenantUserDto
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

public class CreateTenantUserRequest
{
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = "";

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string Password { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Employee";
}

public class UpdateTenantUserRequest
{
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Employee";

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? Password { get; set; }
}
