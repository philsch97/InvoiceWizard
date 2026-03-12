using System.Net.Http.Headers;
using System.Net.Http.Json;
using InvoiceWizard.Web.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace InvoiceWizard.Web.Services;

public class BackendApiClient(HttpClient httpClient)
{
    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
        => await TryGetAsync("api/dashboard/summary", new DashboardSummary(), cancellationToken);

    public async Task<List<CustomerItem>> GetCustomersAsync(CancellationToken cancellationToken = default)
        => await TryGetListAsync<CustomerItem>("api/customers", cancellationToken);

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

    public async Task<List<ProjectItem>> GetProjectsAsync(int? customerId = null, CancellationToken cancellationToken = default)
    {
        var url = customerId.HasValue ? $"api/projects?customerId={customerId.Value}" : "api/projects";
        return await TryGetListAsync<ProjectItem>(url, cancellationToken);
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

    public async Task<ProjectDetailsItem> GetProjectDetailsAsync(int projectId, CancellationToken cancellationToken = default)
        => await TryGetAsync($"api/projects/{projectId}", new ProjectDetailsItem(), cancellationToken);

    public async Task<ProjectDetailsItem> UpdateProjectDetailsAsync(int projectId, SaveProjectDetailsModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/projects/{projectId}/details", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDetailsItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<List<WorkTimeItem>> GetWorkTimesAsync(int? customerId = null, int? projectId = null, CancellationToken cancellationToken = default)
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
        return await TryGetListAsync<WorkTimeItem>(url, cancellationToken);
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

    public async Task<List<TodoListItem>> GetTodoListsAsync(int customerId, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var url = projectId.HasValue
            ? $"api/todolists?customerId={customerId}&projectId={projectId.Value}"
            : $"api/todolists?customerId={customerId}";
        return await TryGetListAsync<TodoListItem>(url, cancellationToken);
    }

    public async Task<TodoListItem> CreateTodoListAsync(int customerId, int? projectId, string title, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/todolists", new { customerId, projectId, title }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task DeleteTodoListAsync(int todoListId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/todolists/{todoListId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TodoListItem> CreateTodoItemAsync(int todoListId, string text, int? parentTodoItemId = null, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/todolists/{todoListId}/items", new { text, parentTodoItemId }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<TodoListItem> UpdateTodoItemStateAsync(int todoItemId, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/todoitems/{todoItemId}/state", new { isCompleted }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<TodoListItem> DeleteTodoItemAsync(int todoItemId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/todoitems/{todoItemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<TodoListItem> UploadTodoAttachmentAsync(int todoListId, IBrowserFile file, string caption, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream(10_000_000, cancellationToken);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        using var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", file.Name);
        content.Add(new StringContent(caption ?? string.Empty), "caption");
        var response = await httpClient.PostAsync($"api/todolists/{todoListId}/attachments", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<TodoListItem> DeleteTodoAttachmentAsync(int todoAttachmentId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/todoattachments/{todoAttachmentId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoListItem>(cancellationToken: cancellationToken))!;
    }

    public async Task<List<TenantUserModel>> GetTenantUsersAsync(CancellationToken cancellationToken = default)
        => await TryGetListAsync<TenantUserModel>("api/tenant-users", cancellationToken);

    public async Task<TenantUserModel> CreateTenantUserAsync(CreateTenantUserModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/tenant-users", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantUserModel>(cancellationToken: cancellationToken))!;
    }

    public async Task<TenantUserModel> UpdateTenantUserAsync(int appUserId, UpdateTenantUserModel model, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/tenant-users/{appUserId}", model, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantUserModel>(cancellationToken: cancellationToken))!;
    }

    private async Task<T> TryGetAsync<T>(string url, T fallback, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<T>(url, cancellationToken) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private async Task<List<T>> TryGetListAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<List<T>>(url, cancellationToken) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
