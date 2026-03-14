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

    public async Task<LicenseActivationViewModel> CreateLicenseActivationAsync(string planCode, string? customerEmail, DateTime? validUntil, int? maxUsersOverride, int? maxProjectsOverride, int? maxCustomersOverride, bool? includesMobileAccessOverride)
    {
        var response = await _httpClient.PostAsJsonAsync("api/license-activations", new
        {
            planCode,
            customerEmail,
            validUntil,
            maxUsersOverride,
            maxProjectsOverride,
            maxCustomersOverride,
            includesMobileAccessOverride
        });
        response.EnsureSuccessStatusCode();
        return MapLicenseActivation((await response.Content.ReadFromJsonAsync<LicenseActivationDto>(_jsonOptions)) ?? new LicenseActivationDto());
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
            IncludesMobileAccess = item.IncludesMobileAccess
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
    }
}
