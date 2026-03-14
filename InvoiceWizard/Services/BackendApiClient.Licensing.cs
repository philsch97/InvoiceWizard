using System.Net.Http.Json;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard.Services;

public partial class BackendApiClient
{
    public async Task<List<SubscriptionPlanViewModel>> GetSubscriptionPlansAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<SubscriptionPlanDto>>("api/license-activations/plans", _jsonOptions) ?? [];
        return items.Select(x => new SubscriptionPlanViewModel
        {
            SubscriptionPlanId = x.SubscriptionPlanId,
            Code = x.Code,
            Name = x.Name,
            MaxUsers = x.MaxUsers,
            MaxProjects = x.MaxProjects,
            MaxCustomers = x.MaxCustomers,
            IncludesMobileAccess = x.IncludesMobileAccess
        }).ToList();
    }

    public async Task<List<LicenseActivationViewModel>> GetLicenseActivationsAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<LicenseActivationDto>>("api/license-activations", _jsonOptions) ?? [];
        return items.Select(MapLicenseActivation).ToList();
    }

    public async Task<List<ManagedTenantLicenseViewModel>> GetManagedTenantLicensesAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<ManagedTenantLicenseDto>>("api/tenant-licenses", _jsonOptions) ?? [];
        return items.Select(MapManagedTenantLicense).ToList();
    }

    public async Task<LicenseActivationViewModel> CreateLicenseActivationAsync(string planCode, string? customerEmail, DateTime? validUntil, int? maxUsersOverride, int? maxProjectsOverride, int? maxCustomersOverride, bool? includesMobileAccessOverride, string billingCycle, decimal priceNet, bool renewsAutomatically)
    {
        var response = await _httpClient.PostAsJsonAsync("api/license-activations", new
        {
            planCode,
            customerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim(),
            validUntil,
            maxUsersOverride,
            maxProjectsOverride,
            maxCustomersOverride,
            includesMobileAccessOverride,
            billingCycle,
            priceNet,
            renewsAutomatically
        });
        response.EnsureSuccessStatusCode();
        return MapLicenseActivation((await response.Content.ReadFromJsonAsync<LicenseActivationDto>(_jsonOptions)) ?? new LicenseActivationDto());
    }

    public async Task<ManagedTenantLicenseViewModel> UpdateManagedTenantLicenseAsync(ManagedTenantLicenseViewModel item)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenant-licenses/{item.TenantLicenseId}", new
        {
            planCode = item.PlanCode,
            billingCycle = item.BillingCycle,
            priceNet = item.PriceNet,
            renewsAutomatically = item.RenewsAutomatically,
            validUntil = item.ValidUntil,
            nextBillingDate = item.NextBillingDate,
            cancelledAt = item.CancelledAt,
            graceUntil = item.GraceUntil,
            maxUsersOverride = item.MaxUsers,
            maxProjectsOverride = item.MaxProjects,
            maxCustomersOverride = item.MaxCustomers,
            includesMobileAccessOverride = item.IncludesMobileAccess,
            isActive = item.IsActive
        });
        response.EnsureSuccessStatusCode();
        return MapManagedTenantLicense((await response.Content.ReadFromJsonAsync<ManagedTenantLicenseDto>(_jsonOptions)) ?? new ManagedTenantLicenseDto());
    }

    private static LicenseActivationViewModel MapLicenseActivation(LicenseActivationDto item)
    {
        return new LicenseActivationViewModel
        {
            LicenseActivationId = item.LicenseActivationId,
            ActivationCode = item.ActivationCode,
            PlanCode = item.PlanCode,
            PlanName = item.PlanName,
            CustomerEmail = item.CustomerEmail,
            ValidUntil = item.ValidUntil,
            IsUsed = item.IsUsed,
            UsedAt = item.UsedAt,
            UsedByDisplayName = item.UsedByDisplayName,
            CreatedAt = item.CreatedAt,
            CreatedByDisplayName = item.CreatedByDisplayName,
            MaxUsers = item.MaxUsers,
            MaxProjects = item.MaxProjects,
            MaxCustomers = item.MaxCustomers,
            IncludesMobileAccess = item.IncludesMobileAccess,
            BillingCycle = item.BillingCycle,
            PriceNet = item.PriceNet,
            RenewsAutomatically = item.RenewsAutomatically
        };
    }

    private static ManagedTenantLicenseViewModel MapManagedTenantLicense(ManagedTenantLicenseDto item)
    {
        return new ManagedTenantLicenseViewModel
        {
            TenantLicenseId = item.TenantLicenseId,
            TenantId = item.TenantId,
            TenantName = item.TenantName,
            TenantSlug = item.TenantSlug,
            PlanCode = item.PlanCode,
            PlanName = item.PlanName,
            MaxUsers = item.MaxUsers,
            MaxProjects = item.MaxProjects,
            MaxCustomers = item.MaxCustomers,
            IncludesMobileAccess = item.IncludesMobileAccess,
            BillingCycle = item.BillingCycle,
            PriceNet = item.PriceNet,
            RenewsAutomatically = item.RenewsAutomatically,
            ValidFrom = item.ValidFrom,
            ValidUntil = item.ValidUntil,
            NextBillingDate = item.NextBillingDate,
            CancelledAt = item.CancelledAt,
            GraceUntil = item.GraceUntil,
            IsActive = item.IsActive,
            Status = item.Status
        };
    }

    private class SubscriptionPlanDto
    {
        public int SubscriptionPlanId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int MaxUsers { get; set; }
        public int MaxProjects { get; set; }
        public int MaxCustomers { get; set; }
        public bool IncludesMobileAccess { get; set; }
        public string BillingCycle { get; set; } = "";
        public decimal PriceNet { get; set; }
        public bool RenewsAutomatically { get; set; }
    }

    private class LicenseActivationDto
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

    private class ManagedTenantLicenseDto
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
}
