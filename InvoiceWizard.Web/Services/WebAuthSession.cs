using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceWizard.Web.Models;
using Microsoft.JSInterop;

namespace InvoiceWizard.Web.Services;

public class WebAuthSession(IHttpClientFactory httpClientFactory, IJSRuntime jsRuntime)
{
    private const string StorageKey = "invoicewizard.auth";
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public event Action? StateChanged;

    public bool IsInitialized { get; private set; }
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }
    public BootstrapStateModel BootstrapState { get; private set; } = new();
    public AuthSessionModel? CurrentSession { get; private set; }

    public string? AccessToken => CurrentSession?.AccessToken;
    public bool IsAuthenticated => CurrentSession is not null;
    public bool CanManageUsers => string.Equals(CurrentSession?.User.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool HasMobileAccess => CurrentSession?.License?.IncludesMobileAccess ?? true;

    public async Task EnsureInitializedAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        await SetBusyAsync(true);
        try
        {
            BootstrapState = await GetBootstrapStateAsync();
            var storedJson = await jsRuntime.InvokeAsync<string?>("invoiceWizardAuth.getSession");
            if (!string.IsNullOrWhiteSpace(storedJson))
            {
                var stored = JsonSerializer.Deserialize<AuthSessionModel>(storedJson, _jsonOptions);
                if (stored is not null)
                {
                    var refreshed = await TryGetCurrentUserAsync(stored.AccessToken);
                    if (refreshed is not null)
                    {
                        CurrentSession = refreshed;
                        await PersistSessionAsync();
                    }
                    else
                    {
                        await ClearStoredSessionAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInitialized = true;
            await SetBusyAsync(false);
            NotifyStateChanged();
        }
    }

    public async Task LoginAsync(LoginRequestModel request)
    {
        await SetBusyAsync(true);
        try
        {
            ErrorMessage = null;
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync("api/auth/login", request);
            await EnsureSuccessAsync(response);
            CurrentSession = await response.Content.ReadFromJsonAsync<AuthSessionModel>(_jsonOptions)
                ?? throw new InvalidOperationException("Die Anmeldedaten konnten nicht gelesen werden.");
            BootstrapState = await GetBootstrapStateAsync();
            await PersistSessionAsync();
        }
        finally
        {
            await SetBusyAsync(false);
            NotifyStateChanged();
        }
    }

    public async Task BootstrapAdminAsync(BootstrapAdminRequestModel request)
    {
        await SetBusyAsync(true);
        try
        {
            ErrorMessage = null;
            using var client = CreateClient();
            var response = await client.PostAsJsonAsync("api/auth/bootstrap-admin", request);
            await EnsureSuccessAsync(response);
            CurrentSession = await response.Content.ReadFromJsonAsync<AuthSessionModel>(_jsonOptions)
                ?? throw new InvalidOperationException("Die Setup-Antwort konnte nicht gelesen werden.");
            BootstrapState = await GetBootstrapStateAsync();
            await PersistSessionAsync();
        }
        finally
        {
            await SetBusyAsync(false);
            NotifyStateChanged();
        }
    }

    public async Task LogoutAsync()
    {
        CurrentSession = null;
        ErrorMessage = null;
        BootstrapState = await GetBootstrapStateAsync();
        await ClearStoredSessionAsync();
        NotifyStateChanged();
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("BackendAnonymous");

    private async Task<BootstrapStateModel> GetBootstrapStateAsync()
    {
        using var client = CreateClient();
        return await client.GetFromJsonAsync<BootstrapStateModel>("api/auth/bootstrap-state", _jsonOptions) ?? new BootstrapStateModel();
    }

    private async Task<AuthSessionModel?> TryGetCurrentUserAsync(string accessToken)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("api/auth/me");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var model = await response.Content.ReadFromJsonAsync<AuthSessionModel>(_jsonOptions);
        if (model is not null)
        {
            model.AccessToken = accessToken;
        }
        return model;
    }

    private async Task PersistSessionAsync()
    {
        if (CurrentSession is null)
        {
            await ClearStoredSessionAsync();
            return;
        }

        var json = JsonSerializer.Serialize(CurrentSession, _jsonOptions);
        await jsRuntime.InvokeVoidAsync("invoiceWizardAuth.setSession", json);
    }

    private Task ClearStoredSessionAsync()
        => jsRuntime.InvokeVoidAsync("invoiceWizardAuth.clearSession").AsTask();

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        ErrorMessage = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        throw new InvalidOperationException(ErrorMessage);
    }

    private async Task SetBusyAsync(bool value)
    {
        IsBusy = value;
        await Task.Yield();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
