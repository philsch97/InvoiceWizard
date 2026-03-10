using System.Net.Http.Json;
using InvoiceWizard.Web.Models;

namespace InvoiceWizard.Web.Services;

public class BackendApiClient(HttpClient httpClient)
{
    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<DashboardSummary>("api/dashboard/summary", cancellationToken) ?? new DashboardSummary();

    public async Task<IReadOnlyList<CustomerItem>> GetCustomersAsync(CancellationToken cancellationToken = default)
        => await httpClient.GetFromJsonAsync<List<CustomerItem>>("api/customers", cancellationToken) ?? [];

    public async Task<CustomerItem> SaveCustomerAsync(SaveCustomerModel model, int? customerId = null, CancellationToken cancellationToken = default)
    {
        var response = customerId.HasValue
            ? await httpClient.PutAsJsonAsync($"api/customers/{customerId.Value}", model, cancellationToken)
            : await httpClient.PostAsJsonAsync("api/customers", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerItem>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/customers/{customerId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ProjectItem>> GetProjectsAsync(int? customerId = null, CancellationToken cancellationToken = default)
    {
        var url = customerId.HasValue ? $"api/projects?customerId={customerId.Value}" : "api/projects";
        return await httpClient.GetFromJsonAsync<List<ProjectItem>>(url, cancellationToken) ?? [];
    }

    public async Task<ProjectItem> CreateProjectAsync(int customerId, SaveProjectModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/customers/{customerId}/projects", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectItem>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/projects/{projectId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<WorkTimeItem>> GetWorkTimesAsync(int? customerId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (customerId.HasValue)
        {
            parts.Add($"customerId={customerId.Value}");
        }

        if (projectId.HasValue)
        {
            parts.Add($"projectId={projectId.Value}");
        }

        var url = parts.Count == 0 ? "api/worktimeentries" : $"api/worktimeentries?{string.Join("&", parts)}";
        return await httpClient.GetFromJsonAsync<List<WorkTimeItem>>(url, cancellationToken) ?? [];
    }

    public async Task<WorkTimeItem> CreateWorkTimeAsync(SaveWorkTimeModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/worktimeentries", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkTimeItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<WorkTimeItem> UpdateWorkTimeStatusAsync(int workTimeEntryId, UpdateWorkTimeStatusModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/worktimeentries/{workTimeEntryId}/status", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkTimeItem>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteWorkTimeAsync(int workTimeEntryId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/worktimeentries/{workTimeEntryId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
