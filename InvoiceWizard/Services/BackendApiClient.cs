using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InvoiceWizard.Data.Entities;
using InvoiceWizard.Data.ViewModels;

namespace InvoiceWizard.Services;

public partial class BackendApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public BackendApiClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("INVOICEWIZARD_API_BASEURL") ?? "http://localhost:5142/";
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public void SetAccessToken(string? accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<AuthBootstrapStateViewModel> GetBootstrapStateAsync()
    {
        var state = await _httpClient.GetFromJsonAsync<BootstrapStateDto>("api/auth/bootstrap-state", _jsonOptions) ?? new BootstrapStateDto();
        return new AuthBootstrapStateViewModel
        {
            HasUsers = state.HasUsers,
            HasTenants = state.HasTenants
        };
    }

    public async Task<AuthSessionViewModel> BootstrapAdminAsync(string tenantName, string displayName, string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/bootstrap-admin", new
        {
            tenantName,
            displayName,
            email,
            password
        });
        await EnsureSuccessWithMessageAsync(response);
        var session = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions) ?? new AuthResponseDto();
        return MapAuthSession(session);
    }

    public async Task<AuthSessionViewModel> ActivateLicenseAsync(string activationCode, string tenantName, string displayName, string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/activate-license", new
        {
            activationCode,
            tenantName,
            displayName,
            email,
            password
        });
        await EnsureSuccessWithMessageAsync(response);
        var session = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions) ?? new AuthResponseDto();
        return MapAuthSession(session);
    }

    public async Task<AuthSessionViewModel> LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", new
        {
            email,
            password
        });
        await EnsureSuccessWithMessageAsync(response);
        var session = await response.Content.ReadFromJsonAsync<AuthResponseDto>(_jsonOptions) ?? new AuthResponseDto();
        return MapAuthSession(session);
    }

    public async Task<AuthSessionViewModel> GetCurrentSessionAsync()
    {
        var session = await _httpClient.GetFromJsonAsync<AuthResponseDto>("api/auth/me", _jsonOptions) ?? new AuthResponseDto();
        return MapAuthSession(session);
    }

    public async Task<List<TenantUserViewModel>> GetTenantUsersAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<TenantUserDto>>("api/tenant-users", _jsonOptions) ?? [];
        return items.Select(MapTenantUser).ToList();
    }

    public async Task<TenantUserViewModel> CreateTenantUserAsync(CreateTenantUserViewModel user)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tenant-users", new
        {
            displayName = user.DisplayName,
            email = user.Email,
            password = user.Password,
            role = user.Role
        });
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<TenantUserDto>(_jsonOptions) ?? new TenantUserDto();
        return MapTenantUser(item);
    }

    public async Task<TenantUserViewModel> UpdateTenantUserAsync(int appUserId, UpdateTenantUserViewModel user)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenant-users/{appUserId}", new
        {
            displayName = user.DisplayName,
            role = user.Role,
            isActive = user.IsActive,
            password = string.IsNullOrWhiteSpace(user.Password) ? null : user.Password
        });
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<TenantUserDto>(_jsonOptions) ?? new TenantUserDto();
        return MapTenantUser(item);
    }

    public async Task<List<CustomerEntity>> GetCustomersAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<CustomerDto>>("api/customers", _jsonOptions) ?? [];
        return items.Select(MapCustomer).OrderBy(x => x.Name).ToList();
    }

    public async Task<CompanyProfileEntity> GetCompanyProfileAsync()
    {
        var item = await _httpClient.GetFromJsonAsync<CompanyProfileDto>("api/company-profile", _jsonOptions) ?? new CompanyProfileDto();
        return MapCompanyProfile(item);
    }

    public async Task<CompanyProfileEntity> SaveCompanyProfileAsync(CompanyProfileEntity profile)
    {
        var response = await _httpClient.PutAsJsonAsync("api/company-profile", new
        {
            companyName = profile.CompanyName,
            companyStreet = profile.CompanyStreet,
            companyHouseNumber = profile.CompanyHouseNumber,
            companyPostalCode = profile.CompanyPostalCode,
            companyCity = profile.CompanyCity,
            companyEmailAddress = NormalizeOptionalEmail(profile.CompanyEmailAddress),
            companyPhoneNumber = profile.CompanyPhoneNumber,
            taxNumber = profile.TaxNumber,
            bankName = profile.BankName,
            bankIban = profile.BankIban,
            bankBic = profile.BankBic,
            nextRevenueInvoiceNumber = profile.NextRevenueInvoiceNumber,
            nextCustomerNumber = profile.NextCustomerNumber
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<CompanyProfileDto>(_jsonOptions) ?? new CompanyProfileDto();
        return MapCompanyProfile(item);
    }

    public async Task<(string InvoiceNumber, string CustomerNumber)> ReserveRevenueInvoiceNumberAsync(int customerId)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invoices/reserve-revenue-number", new { customerId });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<ReserveRevenueInvoiceNumberDto>(_jsonOptions) ?? new ReserveRevenueInvoiceNumberDto();
        return (item.InvoiceNumber, item.CustomerNumber);
    }

    public async Task<CustomerEntity> SaveCustomerAsync(CustomerEntity customer, int? customerId = null)
    {
        var payload = new
        {
            customerNumber = string.IsNullOrWhiteSpace(customer.CustomerNumber) ? null : customer.CustomerNumber,
            name = customer.Name,
            firstName = customer.FirstName,
            lastName = customer.LastName,
            street = customer.Street,
            houseNumber = customer.HouseNumber,
            postalCode = customer.PostalCode,
            city = customer.City,
            emailAddress = NormalizeOptionalEmail(customer.EmailAddress),
            phoneNumber = customer.PhoneNumber,
            defaultMarkupPercent = customer.DefaultMarkupPercent
        };

        HttpResponseMessage response = customerId.HasValue
            ? await _httpClient.PutAsJsonAsync($"api/customers/{customerId.Value}", payload)
            : await _httpClient.PostAsJsonAsync("api/customers", payload);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<CustomerDto>(_jsonOptions) ?? new CustomerDto();
        return MapCustomer(item);
    }

    public async Task DeleteCustomerAsync(int customerId)
    {
        var response = await _httpClient.DeleteAsync($"api/customers/{customerId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ProjectSelectionItem>> GetProjectSelectionsAsync(int customerId, bool includeAll = false)
    {
        var items = await _httpClient.GetFromJsonAsync<List<ProjectDto>>($"api/projects?customerId={customerId}", _jsonOptions) ?? [];
        var result = new List<ProjectSelectionItem>();
        if (includeAll)
        {
            result.Add(new ProjectSelectionItem { ProjectId = null, Name = "Alle Projekte" });
        }

        result.AddRange(items.OrderBy(x => x.Name).Select(x => new ProjectSelectionItem { ProjectId = x.ProjectId, Name = x.Name }));
        return result;
    }

    public async Task<ProjectDto> SaveProjectAsync(int customerId, string name)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/customers/{customerId}/projects", new { name });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDto>(_jsonOptions) ?? new ProjectDto();
    }

    public async Task<ProjectEntity> GetProjectDetailsAsync(int projectId)
    {
        var item = await _httpClient.GetFromJsonAsync<ProjectDetailsDto>($"api/projects/{projectId}", _jsonOptions) ?? new ProjectDetailsDto();
        return MapProjectDetails(item);
    }

    public async Task<ProjectEntity> UpdateProjectDetailsAsync(ProjectEntity project)
    {
        var payload = new
        {
            connectionUserSameAsCustomer = project.ConnectionUserSameAsCustomer,
            connectionUserFirstName = project.ConnectionUserFirstName,
            connectionUserLastName = project.ConnectionUserLastName,
            connectionUserStreet = project.ConnectionUserStreet,
            connectionUserHouseNumber = project.ConnectionUserHouseNumber,
            connectionUserPostalCode = project.ConnectionUserPostalCode,
            connectionUserCity = project.ConnectionUserCity,
            connectionUserParcelNumber = project.ConnectionUserParcelNumber,
            connectionUserEmailAddress = NormalizeOptionalEmail(project.ConnectionUserEmailAddress),
            connectionUserPhoneNumber = project.ConnectionUserPhoneNumber,
            propertyOwnerSameAsCustomer = project.PropertyOwnerSameAsCustomer,
            propertyOwnerFirstName = project.PropertyOwnerFirstName,
            propertyOwnerLastName = project.PropertyOwnerLastName,
            propertyOwnerStreet = project.PropertyOwnerStreet,
            propertyOwnerHouseNumber = project.PropertyOwnerHouseNumber,
            propertyOwnerPostalCode = project.PropertyOwnerPostalCode,
            propertyOwnerCity = project.PropertyOwnerCity,
            propertyOwnerEmailAddress = NormalizeOptionalEmail(project.PropertyOwnerEmailAddress),
            propertyOwnerPhoneNumber = project.PropertyOwnerPhoneNumber
        };

        var response = await _httpClient.PutAsJsonAsync($"api/projects/{project.ProjectId}/details", payload);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<ProjectDetailsDto>(_jsonOptions) ?? new ProjectDetailsDto();
        return MapProjectDetails(item);
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        var response = await _httpClient.DeleteAsync($"api/projects/{projectId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<WorkTimeEntryEntity>> GetWorkTimeEntriesAsync(int? customerId = null, int? projectId = null)
    {
        var query = new List<string>();
        if (customerId.HasValue) query.Add($"customerId={customerId.Value}");
        if (projectId.HasValue) query.Add($"projectId={projectId.Value}");
        var url = query.Count == 0 ? "api/worktimeentries" : $"api/worktimeentries?{string.Join("&", query)}";
        var items = await _httpClient.GetFromJsonAsync<List<WorkTimeDto>>(url, _jsonOptions) ?? [];
        return items.Select(MapWorkTime).ToList();
    }

    public async Task SaveWorkTimeAsync(WorkTimeEntryEntity entry)
    {
        var response = await _httpClient.PostAsJsonAsync("api/worktimeentries", new
        {
            customerId = entry.CustomerId,
            projectId = entry.ProjectId,
            workDate = entry.WorkDate,
            startTime = entry.StartTime,
            endTime = entry.EndTime,
            breakMinutes = entry.BreakMinutes,
            hourlyRate = entry.HourlyRate,
            travelKilometers = entry.TravelKilometers,
            travelRatePerKilometer = entry.TravelRatePerKilometer,
            description = entry.Description,
            comment = entry.Comment
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveWorkTimeAsync(int entryId, WorkTimeEntryEntity entry)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/worktimeentries/{entryId}", new
        {
            customerId = entry.CustomerId,
            projectId = entry.ProjectId,
            workDate = entry.WorkDate,
            startTime = entry.StartTime,
            endTime = entry.EndTime,
            breakMinutes = entry.BreakMinutes,
            hourlyRate = entry.HourlyRate,
            travelKilometers = entry.TravelKilometers,
            travelRatePerKilometer = entry.TravelRatePerKilometer,
            description = entry.Description,
            comment = entry.Comment
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task<WorkTimeEntryEntity?> GetActiveWorkTimeClockAsync()
    {
        var response = await _httpClient.GetAsync("api/worktimeentries/clock/active");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is 0 || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return null;
        }

        var item = await response.Content.ReadFromJsonAsync<WorkTimeDto?>(_jsonOptions);
        return item is null ? null : MapWorkTime(item);
    }

    public async Task<WorkTimeEntryEntity> StartWorkTimeClockAsync(int customerId, int? projectId, decimal hourlyRate, decimal travelRatePerKilometer, string description, DateTimeOffset startedAt)
    {
        var response = await _httpClient.PostAsJsonAsync("api/worktimeentries/clock/start", new
        {
            customerId,
            projectId,
            hourlyRate,
            travelRatePerKilometer,
            description,
            startedAt
        });
        response.EnsureSuccessStatusCode();
        return MapWorkTime((await response.Content.ReadFromJsonAsync<WorkTimeDto>(_jsonOptions)) ?? new WorkTimeDto());
    }

    public async Task<WorkTimeEntryEntity> StartWorkTimePauseAsync(DateTimeOffset changedAt)
    {
        var response = await _httpClient.PostAsJsonAsync("api/worktimeentries/clock/pause/start", new { changedAt });
        response.EnsureSuccessStatusCode();
        return MapWorkTime((await response.Content.ReadFromJsonAsync<WorkTimeDto>(_jsonOptions)) ?? new WorkTimeDto());
    }

    public async Task<WorkTimeEntryEntity> StopWorkTimePauseAsync(DateTimeOffset changedAt)
    {
        var response = await _httpClient.PostAsJsonAsync("api/worktimeentries/clock/pause/stop", new { changedAt });
        response.EnsureSuccessStatusCode();
        return MapWorkTime((await response.Content.ReadFromJsonAsync<WorkTimeDto>(_jsonOptions)) ?? new WorkTimeDto());
    }

    public async Task<WorkTimeEntryEntity> StopWorkTimeClockAsync(DateTimeOffset endedAt, decimal travelKilometers, string comment)
    {
        var response = await _httpClient.PostAsJsonAsync("api/worktimeentries/clock/stop", new { endedAt, travelKilometers, comment });
        response.EnsureSuccessStatusCode();
        return MapWorkTime((await response.Content.ReadFromJsonAsync<WorkTimeDto>(_jsonOptions)) ?? new WorkTimeDto());
    }

    public async Task UpdateWorkTimeStatusAsync(int id, string? invoiceNumber, bool markInvoiced, bool markPaid)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/worktimeentries/{id}/status", new { customerInvoiceNumber = invoiceNumber, markInvoiced, markPaid });
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateWorkTimeRevenueLinkAsync(int id, int? revenueInvoiceId, string? revenueInvoiceNumber, bool markInvoiced)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/worktimeentries/{id}/revenue-link", new { revenueInvoiceId, revenueInvoiceNumber, markInvoiced });
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task UpdateWorkTimeExportAsync(int id, decimal unitPrice, decimal lineTotal)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/worktimeentry-exports/{id}", new { exportedUnitPrice = unitPrice, exportedLineTotal = lineTotal, lastExportedAt = DateTime.UtcNow });
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteWorkTimeAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/worktimeentries/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<BankAccountSummaryEntity> GetBankingSummaryAsync()
    {
        var item = await _httpClient.GetFromJsonAsync<BankAccountSummaryDto>("api/banking/summary", _jsonOptions) ?? new BankAccountSummaryDto();
        return new BankAccountSummaryEntity
        {
            TransactionCount = item.TransactionCount,
            CurrentBalance = item.CurrentBalance,
            LastBookingDate = item.LastBookingDate,
            AccountIban = item.AccountIban,
            AccountName = item.AccountName
        };
    }

    public async Task<BankImportResultEntity> ImportBankStatementFileAsync(string fileName, byte[] fileBytes)
    {
        var response = await _httpClient.PostAsJsonAsync("api/banking/imports/file", new
        {
            fileName,
            csvContentBase64 = Convert.ToBase64String(fileBytes)
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<BankImportResultDto>(_jsonOptions) ?? new BankImportResultDto();
        return new BankImportResultEntity
        {
            ImportId = item.ImportId,
            FileName = item.FileName,
            AccountName = item.AccountName,
            AccountIban = item.AccountIban,
            Currency = item.Currency,
            ImportedTransactions = item.ImportedTransactions,
            SkippedTransactions = item.SkippedTransactions,
            CurrentBalance = item.CurrentBalance,
            Warnings = item.Warnings ?? []
        };
    }

    public async Task<List<BankTransactionEntity>> GetBankTransactionsAsync(bool showAssigned = true, bool showIgnored = false)
    {
        var items = await _httpClient.GetFromJsonAsync<List<BankTransactionDto>>($"api/banking/transactions?showAssigned={showAssigned.ToString().ToLowerInvariant()}&showIgnored={showIgnored.ToString().ToLowerInvariant()}", _jsonOptions) ?? [];
        return items.Select(MapBankTransaction).ToList();
    }

    public async Task<List<BankInvoiceCandidateEntity>> GetBankInvoiceCandidatesAsync(int bankTransactionId)
    {
        var items = await _httpClient.GetFromJsonAsync<List<BankInvoiceCandidateDto>>($"api/banking/transactions/{bankTransactionId}/candidates", _jsonOptions) ?? [];
        return items.Select(MapBankInvoiceCandidate).ToList();
    }

    public async Task<BankTransactionEntity> AssignBankTransactionAsync(int bankTransactionId, BankInvoiceCandidateEntity candidate, decimal? assignedAmount = null, string? note = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/banking/transactions/{bankTransactionId}/assignments", new
        {
            supplierInvoiceId = candidate.SupplierInvoiceId,
            revenueInvoiceId = candidate.RevenueInvoiceId,
            manualCategory = (string?)null,
            customerInvoiceNumber = candidate.CustomerInvoiceNumber,
            customerId = candidate.CustomerId,
            assignedAmount,
            note = note ?? string.Empty
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<BankTransactionDto>(_jsonOptions) ?? new BankTransactionDto();
        return MapBankTransaction(item);
    }

    public async Task<BankTransactionEntity> AssignBankTransactionWithoutReceiptAsync(int bankTransactionId, string manualCategory, decimal? assignedAmount = null, string? note = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/banking/transactions/{bankTransactionId}/assignments", new
        {
            supplierInvoiceId = (int?)null,
            manualCategory,
            customerInvoiceNumber = (string?)null,
            customerId = (int?)null,
            assignedAmount,
            note = note ?? string.Empty
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<BankTransactionDto>(_jsonOptions) ?? new BankTransactionDto();
        return MapBankTransaction(item);
    }

    public async Task DeleteBankTransactionAssignmentAsync(int assignmentId)
    {
        var response = await _httpClient.DeleteAsync($"api/banking/assignments/{assignmentId}");
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<BankTransactionEntity> IgnoreBankTransactionAsync(int bankTransactionId, string comment)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/banking/transactions/{bankTransactionId}/ignore", new { comment });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<BankTransactionDto>(_jsonOptions) ?? new BankTransactionDto();
        return MapBankTransaction(item);
    }

    public async Task<BankTransactionEntity> UnignoreBankTransactionAsync(int bankTransactionId)
    {
        var response = await _httpClient.DeleteAsync($"api/banking/transactions/{bankTransactionId}/ignore");
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<BankTransactionDto>(_jsonOptions) ?? new BankTransactionDto();
        return MapBankTransaction(item);
    }

    public async Task<List<TodoListEntity>> GetTodoListsAsync(int customerId, int? projectId = null)
    {
        var url = projectId.HasValue
            ? $"api/todolists?customerId={customerId}&projectId={projectId.Value}"
            : $"api/todolists?customerId={customerId}";
        var items = await _httpClient.GetFromJsonAsync<List<TodoListDto>>(url, _jsonOptions) ?? [];
        return items.Select(MapTodoList).ToList();
    }

    public async Task<TodoListEntity> CreateTodoListAsync(int customerId, int? projectId, string title)
    {
        var response = await _httpClient.PostAsJsonAsync("api/todolists", new { customerId, projectId, title });
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public async Task DeleteTodoListAsync(int todoListId)
    {
        var response = await _httpClient.DeleteAsync($"api/todolists/{todoListId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<TodoListEntity> CreateTodoItemAsync(int todoListId, string text, int? parentTodoItemId = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/todolists/{todoListId}/items", new { text, parentTodoItemId });
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public async Task<TodoListEntity> UpdateTodoItemStateAsync(int todoItemId, bool isCompleted)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/todoitems/{todoItemId}/state", new { isCompleted });
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public async Task<TodoListEntity> DeleteTodoItemAsync(int todoItemId)
    {
        var response = await _httpClient.DeleteAsync($"api/todoitems/{todoItemId}");
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public async Task<TodoListEntity> UploadTodoAttachmentAsync(int todoListId, string filePath, string caption)
    {
        using var stream = File.OpenRead(filePath);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(filePath));
        using var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        content.Add(new StringContent(caption ?? string.Empty), "caption");
        var response = await _httpClient.PostAsync($"api/todolists/{todoListId}/attachments", content);
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public async Task<TodoListEntity> DeleteTodoAttachmentAsync(int todoAttachmentId)
    {
        var response = await _httpClient.DeleteAsync($"api/todoattachments/{todoAttachmentId}");
        response.EnsureSuccessStatusCode();
        return MapTodoList((await response.Content.ReadFromJsonAsync<TodoListDto>(_jsonOptions)) ?? new TodoListDto());
    }

    public Task<byte[]> DownloadTodoAttachmentAsync(int todoAttachmentId)
        => _httpClient.GetByteArrayAsync($"api/todoattachments/{todoAttachmentId}/content");

    public async Task<List<InvoiceLineRow>> GetInvoiceLineRowsAsync(bool showCompleted)
    {
        var items = await _httpClient.GetFromJsonAsync<List<InvoiceLineDto>>($"api/invoicelines?showCompleted={showCompleted.ToString().ToLowerInvariant()}", _jsonOptions) ?? [];
        return items.Select(x => new InvoiceLineRow(MapInvoiceLine(x))).ToList();
    }

    public async Task CreateAllocationAsync(int invoiceLineId, int customerId, int projectId, decimal qty, decimal customerUnitPrice, bool isSmallMaterial)
    {
        var response = await _httpClient.PostAsJsonAsync("api/allocations", new { invoiceLineId, customerId, projectId, allocatedQuantity = qty, customerUnitPrice, isSmallMaterial });
        response.EnsureSuccessStatusCode();
    }

    public async Task SetInvoiceLineGeneralSmallMaterialAsync(int invoiceLineId, bool isGeneralSmallMaterial)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/invoicelines/{invoiceLineId}/general-small-material", new { isGeneralSmallMaterial });
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task SetInvoiceLineInventoryStockAsync(int invoiceLineId, bool isInventoryStock)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/invoicelines/{invoiceLineId}/inventory-stock", new { isInventoryStock });
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task UpdateAllocationQuantityAsync(int allocationId, decimal qty)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/allocations/{allocationId}/quantity", new { allocatedQuantity = qty });
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<LineAllocationEntity>> GetAllocationsAsync(int? customerId = null, int? projectId = null)
    {
        var query = new List<string>();
        if (customerId.HasValue) query.Add($"customerId={customerId.Value}");
        if (projectId.HasValue) query.Add($"projectId={projectId.Value}");
        var url = query.Count == 0 ? "api/allocations" : $"api/allocations?{string.Join("&", query)}";
        var items = await _httpClient.GetFromJsonAsync<List<AllocationDto>>(url, _jsonOptions) ?? [];
        return items.Select(MapAllocation).ToList();
    }

    public async Task UpdateAllocationStatusAsync(int allocationId, string? invoiceNumber, bool markInvoiced, bool markPaid)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/allocations/{allocationId}/status", new { customerInvoiceNumber = invoiceNumber, markInvoiced, markPaid });
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAllocationRevenueLinkAsync(int allocationId, int? revenueInvoiceId, string? revenueInvoiceNumber, bool markInvoiced)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/allocations/{allocationId}/revenue-link", new { revenueInvoiceId, revenueInvoiceNumber, markInvoiced });
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task UpdateAllocationExportAsync(int allocationId, decimal markupPercent, decimal unitPrice, decimal lineTotal)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/allocations/{allocationId}/export", new { exportedMarkupPercent = markupPercent, exportedUnitPrice = unitPrice, exportedLineTotal = lineTotal, lastExportedAt = DateTime.UtcNow });
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAllocationAsync(int allocationId)
    {
        var response = await _httpClient.DeleteAsync($"api/allocations/{allocationId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteInvoiceLineAsync(int invoiceLineId)
    {
        var response = await _httpClient.DeleteAsync($"api/invoicelines/{invoiceLineId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveInvoiceAsync(string invoiceDirection, string invoiceStatus, string invoiceNumber, DateTime invoiceDate, DateTime? deliveryDate, DateTime? paymentDueDate, int? customerId, string supplierName, string accountingCategory, string subject, bool applySmallBusinessRegulation, decimal invoiceTotalAmount, decimal shippingCostNet, decimal shippingCostGross, string sourcePdfPath, string originalPdfFileName, string? pdfContentBase64, string contentHash, IEnumerable<ManualInvoiceLineInput> lines, bool hasSupplierInvoice = true)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invoices", new
        {
            invoiceDirection,
            invoiceStatus,
            hasSupplierInvoice,
            customerId,
            invoiceNumber,
            invoiceDate,
            deliveryDate,
            paymentDueDate,
            supplierName,
            accountingCategory,
            subject,
            applySmallBusinessRegulation,
            invoiceTotalAmount,
            shippingCostNet,
            shippingCostGross,
            sourcePdfPath,
            originalPdfFileName,
            pdfContentBase64,
            contentHash,
            lines = lines.Select(line => new
            {
                line.Position,
                line.ArticleNumber,
                line.Ean,
                line.Description,
                line.Quantity,
                line.Unit,
                netUnitPrice = line.NetUnitPrice,
                metalSurcharge = line.MetalSurcharge,
                grossListPrice = line.GrossListPrice,
                grossUnitPrice = line.GrossUnitPrice,
                priceBasisQuantity = line.PriceBasisQuantity,
                shippingNetShare = line.ShippingNetShare,
                shippingGrossShare = line.ShippingGrossShare,
                line.LineTotal,
                grossLineTotal = line.GrossLineTotal
            }).ToList()
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<InvoiceSaveResultDto>(_jsonOptions) ?? new InvoiceSaveResultDto();
        return item.InvoiceId;
    }

    public async Task<List<InvoiceEntity>> GetInvoicesAsync()
    {
        var items = await _httpClient.GetFromJsonAsync<List<InvoiceListDto>>("api/invoices", _jsonOptions) ?? [];
        return items.Select(MapInvoice).ToList();
    }

    public async Task<InvoiceEntity> GetInvoiceAsync(int invoiceId)
    {
        var item = await _httpClient.GetFromJsonAsync<InvoiceDetailDto>($"api/invoices/{invoiceId}", _jsonOptions) ?? new InvoiceDetailDto();
        return MapInvoice(item);
    }

    public async Task<InvoiceEntity> UpdateInvoiceAsync(int invoiceId, string invoiceDirection, string invoiceStatus, string invoiceNumber, DateTime invoiceDate, DateTime? deliveryDate, DateTime? paymentDueDate, int? customerId, string supplierName, string accountingCategory, string subject, bool applySmallBusinessRegulation, decimal invoiceTotalAmount, decimal shippingCostNet, decimal shippingCostGross, string sourcePdfPath, string originalPdfFileName, string? pdfContentBase64, string contentHash, IEnumerable<ManualInvoiceLineInput> lines, bool hasSupplierInvoice = true)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/invoices/{invoiceId}", new
        {
            invoiceDirection,
            invoiceStatus,
            hasSupplierInvoice,
            customerId,
            invoiceNumber,
            invoiceDate,
            deliveryDate,
            paymentDueDate,
            supplierName,
            accountingCategory,
            subject,
            applySmallBusinessRegulation,
            invoiceTotalAmount,
            shippingCostNet,
            shippingCostGross,
            sourcePdfPath,
            originalPdfFileName,
            pdfContentBase64,
            contentHash,
            lines = lines.Select(line => new
            {
                line.Position,
                line.ArticleNumber,
                line.Ean,
                line.Description,
                line.Quantity,
                line.Unit,
                netUnitPrice = line.NetUnitPrice,
                metalSurcharge = line.MetalSurcharge,
                grossListPrice = line.GrossListPrice,
                grossUnitPrice = line.GrossUnitPrice,
                priceBasisQuantity = line.PriceBasisQuantity,
                shippingNetShare = line.ShippingNetShare,
                shippingGrossShare = line.ShippingGrossShare,
                line.LineTotal,
                grossLineTotal = line.GrossLineTotal
            }).ToList()
        });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<InvoiceDetailDto>(_jsonOptions) ?? new InvoiceDetailDto();
        return MapInvoice(item);
    }

    public async Task<InvoiceEntity> FinalizeInvoiceAsync(int invoiceId)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/finalize", new { });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<InvoiceDetailDto>(_jsonOptions) ?? new InvoiceDetailDto();
        return MapInvoice(item);
    }

    public async Task<InvoiceEntity> CancelInvoiceAsync(int invoiceId, string reason)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/invoices/{invoiceId}/cancel", new { reason });
        await EnsureSuccessWithMessageAsync(response);
        var item = await response.Content.ReadFromJsonAsync<InvoiceDetailDto>(_jsonOptions) ?? new InvoiceDetailDto();
        return MapInvoice(item);
    }

    public async Task DeleteInvoiceAsync(int invoiceId)
    {
        var response = await _httpClient.DeleteAsync($"api/invoices/{invoiceId}");
        await EnsureSuccessWithMessageAsync(response);
    }

    public Task<byte[]> DownloadInvoicePdfAsync(int invoiceId)
        => _httpClient.GetByteArrayAsync($"api/invoices/{invoiceId}/pdf");

    public async Task UploadInvoicePdfAsync(int invoiceId, string originalPdfFileName, byte[] pdfBytes)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/invoices/{invoiceId}/pdf", new
        {
            originalPdfFileName,
            pdfContentBase64 = Convert.ToBase64String(pdfBytes)
        });
        await EnsureSuccessWithMessageAsync(response);
    }

    public async Task<AnalyticsResponseDto> GetAnalyticsAsync(int? customerId = null, int? projectId = null)
    {
        var query = new List<string>();
        if (customerId.HasValue) query.Add($"customerId={customerId.Value}");
        if (projectId.HasValue) query.Add($"projectId={projectId.Value}");
        var url = query.Count == 0 ? "api/analytics/details" : $"api/analytics/details?{string.Join("&", query)}";
        return await _httpClient.GetFromJsonAsync<AnalyticsResponseDto>(url, _jsonOptions) ?? new AnalyticsResponseDto();
    }

    private static AuthSessionViewModel MapAuthSession(AuthResponseDto item)
    {
        return new AuthSessionViewModel
        {
            AccessToken = item.AccessToken,
            ExpiresAtUtc = item.ExpiresAtUtc,
            User = new AuthUserViewModel
            {
                AppUserId = item.User.AppUserId,
                Email = item.User.Email,
                DisplayName = item.User.DisplayName,
                Role = item.User.Role,
                IsPlatformAdmin = item.User.IsPlatformAdmin
            },
            Tenant = new AuthTenantViewModel
            {
                TenantId = item.Tenant.TenantId,
                Name = item.Tenant.Name,
                Slug = item.Tenant.Slug
            },
            License = item.License is null ? null : new AuthLicenseViewModel
            {
                TenantLicenseId = item.License.TenantLicenseId,
                PlanCode = item.License.PlanCode,
                PlanName = item.License.PlanName,
                MaxUsers = item.License.MaxUsers,
                MaxProjects = item.License.MaxProjects,
                MaxCustomers = item.License.MaxCustomers,
                IncludesMobileAccess = item.License.IncludesMobileAccess,
                BillingCycle = item.License.BillingCycle,
                PriceNet = item.License.PriceNet,
                RenewsAutomatically = item.License.RenewsAutomatically,
                NextBillingDate = item.License.NextBillingDate,
                CancelledAt = item.License.CancelledAt,
                GraceUntil = item.License.GraceUntil,
                Status = item.License.Status,
                ValidFrom = item.License.ValidFrom,
                ValidUntil = item.License.ValidUntil,
                IsActive = item.License.IsActive
            }
        };
    }
    private static TenantUserViewModel MapTenantUser(TenantUserDto item)
    {
        return new TenantUserViewModel
        {
            AppUserId = item.AppUserId,
            Email = item.Email,
            DisplayName = item.DisplayName,
            Role = item.Role,
            IsActive = item.IsActive,
            IsDefault = item.IsDefault,
            CreatedAt = item.CreatedAt,
            LastLoginAt = item.LastLoginAt
        };
    }

    private static CustomerEntity MapCustomer(CustomerDto item)
    {
        return new CustomerEntity
        {
            CustomerId = item.CustomerId,
            CustomerNumber = item.CustomerNumber,
            Name = item.Name,
            FirstName = item.FirstName,
            LastName = item.LastName,
            Street = item.Street,
            HouseNumber = item.HouseNumber,
            PostalCode = item.PostalCode,
            City = item.City,
            EmailAddress = item.EmailAddress,
            PhoneNumber = item.PhoneNumber,
            DefaultMarkupPercent = item.DefaultMarkupPercent
        };
    }

    private static CompanyProfileEntity MapCompanyProfile(CompanyProfileDto item)
    {
        return new CompanyProfileEntity
        {
            CompanyName = item.CompanyName,
            CompanyStreet = item.CompanyStreet,
            CompanyHouseNumber = item.CompanyHouseNumber,
            CompanyPostalCode = item.CompanyPostalCode,
            CompanyCity = item.CompanyCity,
            CompanyEmailAddress = item.CompanyEmailAddress ?? "",
            CompanyPhoneNumber = item.CompanyPhoneNumber,
            TaxNumber = item.TaxNumber,
            BankName = item.BankName,
            BankIban = item.BankIban,
            BankBic = item.BankBic,
            NextRevenueInvoiceNumber = item.NextRevenueInvoiceNumber,
            NextCustomerNumber = item.NextCustomerNumber,
            RevenueInvoiceNumberPreview = item.RevenueInvoiceNumberPreview
        };
    }

    private static ProjectEntity MapProjectDetails(ProjectDetailsDto item)
    {
        return new ProjectEntity
        {
            ProjectId = item.ProjectId,
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            Name = item.Name,
            ConnectionUserSameAsCustomer = item.ConnectionUserSameAsCustomer,
            ConnectionUserFirstName = item.ConnectionUserFirstName,
            ConnectionUserLastName = item.ConnectionUserLastName,
            ConnectionUserStreet = item.ConnectionUserStreet,
            ConnectionUserHouseNumber = item.ConnectionUserHouseNumber,
            ConnectionUserPostalCode = item.ConnectionUserPostalCode,
            ConnectionUserCity = item.ConnectionUserCity,
            ConnectionUserParcelNumber = item.ConnectionUserParcelNumber,
            ConnectionUserEmailAddress = item.ConnectionUserEmailAddress,
            ConnectionUserPhoneNumber = item.ConnectionUserPhoneNumber,
            PropertyOwnerSameAsCustomer = item.PropertyOwnerSameAsCustomer,
            PropertyOwnerFirstName = item.PropertyOwnerFirstName,
            PropertyOwnerLastName = item.PropertyOwnerLastName,
            PropertyOwnerStreet = item.PropertyOwnerStreet,
            PropertyOwnerHouseNumber = item.PropertyOwnerHouseNumber,
            PropertyOwnerPostalCode = item.PropertyOwnerPostalCode,
            PropertyOwnerCity = item.PropertyOwnerCity,
            PropertyOwnerEmailAddress = item.PropertyOwnerEmailAddress,
            PropertyOwnerPhoneNumber = item.PropertyOwnerPhoneNumber
        };
    }

    private static WorkTimeEntryEntity MapWorkTime(WorkTimeDto item)
    {
        return new WorkTimeEntryEntity
        {
            WorkTimeEntryId = item.WorkTimeEntryId,
            AppUserId = item.AppUserId,
            UserDisplayName = item.UserDisplayName ?? "",
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            ProjectId = item.ProjectId,
            Project = item.ProjectId.HasValue ? new ProjectEntity { ProjectId = item.ProjectId.Value, Name = item.ProjectName ?? "" } : null,
            RevenueInvoiceId = item.RevenueInvoiceId,
            WorkDate = item.WorkDate,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            BreakMinutes = item.BreakMinutes,
            HoursWorked = item.HoursWorked,
            HourlyRate = item.HourlyRate,
            TravelKilometers = item.TravelKilometers,
            TravelRatePerKilometer = item.TravelRatePerKilometer,
            Description = item.Description,
            Comment = item.Comment,
            CustomerInvoiceNumber = item.CustomerInvoiceNumber,
            CustomerInvoicedAt = item.CustomerInvoicedAt,
            IsPaid = item.IsPaid,
            PaidAt = item.PaidAt,
            IsClockActive = item.IsClockActive,
            PauseStartedAtUtc = item.PauseStartedAtUtc,
            ExportedLineTotal = item.LineTotal,
            ExportedUnitPrice = item.HourlyRate
        };
    }

    private static InvoiceEntity MapInvoice(InvoiceListDto item)
    {
        return new InvoiceEntity
        {
            InvoiceId = item.InvoiceId,
            InvoiceDirection = item.InvoiceDirection,
            CustomerId = item.CustomerId,
            InvoiceStatus = item.InvoiceStatus,
            InvoiceNumber = item.InvoiceNumber,
            InvoiceDate = item.InvoiceDate,
            DeliveryDate = item.DeliveryDate,
            PaymentDueDate = item.PaymentDueDate,
            SupplierName = item.SupplierName,
            HasSupplierInvoice = item.HasSupplierInvoice,
            AccountingCategory = item.AccountingCategory,
            Subject = item.Subject,
            ApplySmallBusinessRegulation = item.ApplySmallBusinessRegulation,
            InvoiceTotalAmount = item.InvoiceTotalAmount,
            ShippingCostNet = item.ShippingCostNet,
            ShippingCostGross = item.ShippingCostGross,
            OriginalPdfFileName = item.OriginalPdfFileName,
            HasStoredPdf = item.HasStoredPdf,
            DraftSavedAt = item.DraftSavedAt,
            FinalizedAt = item.FinalizedAt,
            CancelledAt = item.CancelledAt,
            CancellationReason = item.CancellationReason,
            Lines = item is InvoiceDetailDto detail
                ? detail.Lines.Select(line => new InvoiceLineEntity
                {
                    Position = line.Position,
                    ArticleNumber = line.ArticleNumber,
                    Ean = line.Ean,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    NetUnitPrice = line.NetUnitPrice,
                    MetalSurcharge = line.MetalSurcharge,
                    GrossListPrice = line.GrossListPrice,
                    GrossUnitPrice = line.GrossUnitPrice,
                    PriceBasisQuantity = line.PriceBasisQuantity,
                    ShippingNetShare = line.ShippingNetShare,
                    ShippingGrossShare = line.ShippingGrossShare,
                    LineTotal = line.LineTotal,
                    GrossLineTotal = line.GrossLineTotal
                }).ToList()
                : new List<InvoiceLineEntity>()
        };
    }

    private static BankTransactionEntity MapBankTransaction(BankTransactionDto item)
    {
        return new BankTransactionEntity
        {
            BankTransactionId = item.BankTransactionId,
            ImportId = item.ImportId,
            BookingDate = item.BookingDate,
            ValueDate = item.ValueDate,
            Amount = item.Amount,
            BalanceAfterBooking = item.BalanceAfterBooking,
            Currency = item.Currency,
            CounterpartyName = item.CounterpartyName,
            CounterpartyIban = item.CounterpartyIban,
            Purpose = item.Purpose,
            Reference = item.Reference,
            TransactionType = item.TransactionType,
            AccountIban = item.AccountIban,
            ImportFileName = item.ImportFileName,
            ImportedAt = item.ImportedAt,
            IsIgnored = item.IsIgnored,
            IgnoredComment = item.IgnoredComment,
            IgnoredAt = item.IgnoredAt,
            AssignedAmount = item.AssignedAmount,
            RemainingAmount = item.RemainingAmount,
            Assignments = item.Assignments.Select(x => new BankTransactionAssignmentEntity
            {
                BankTransactionAssignmentId = x.BankTransactionAssignmentId,
                BankTransactionId = x.BankTransactionId,
                AssignmentType = x.AssignmentType,
                SupplierInvoiceId = x.SupplierInvoiceId,
                RevenueInvoiceId = x.RevenueInvoiceId,
                ManualCategory = x.ManualCategory,
                SupplierInvoiceNumber = x.SupplierInvoiceNumber,
                RevenueInvoiceNumber = x.RevenueInvoiceNumber,
                CustomerInvoiceNumber = x.CustomerInvoiceNumber,
                CustomerId = x.CustomerId,
                PartyName = x.PartyName,
                AssignedAmount = x.AssignedAmount,
                Note = x.Note,
                AssignedAt = x.AssignedAt
            }).ToList()
        };
    }

    private static BankInvoiceCandidateEntity MapBankInvoiceCandidate(BankInvoiceCandidateDto item)
    {
        return new BankInvoiceCandidateEntity
        {
            CandidateType = item.CandidateType,
            SupplierInvoiceId = item.SupplierInvoiceId,
            RevenueInvoiceId = item.RevenueInvoiceId,
            SupplierInvoiceNumber = item.SupplierInvoiceNumber,
            RevenueInvoiceNumber = item.RevenueInvoiceNumber,
            CustomerInvoiceNumber = item.CustomerInvoiceNumber,
            CustomerId = item.CustomerId,
            PartyName = item.PartyName,
            InvoiceDate = item.InvoiceDate,
            TotalAmount = item.TotalAmount,
            AssignedAmount = item.AssignedAmount,
            RemainingAmount = item.RemainingAmount,
            IsPaid = item.IsPaid,
            MatchScore = item.MatchScore,
            MatchReason = item.MatchReason
        };
    }

    private static TodoListEntity MapTodoList(TodoListDto item)
    {
        return new TodoListEntity
        {
            TodoListId = item.TodoListId,
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            ProjectId = item.ProjectId,
            Project = item.ProjectId.HasValue ? new ProjectEntity { ProjectId = item.ProjectId.Value, Name = item.ProjectName ?? "" } : null,
            Title = item.Title,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            OpenItemCount = item.OpenItemCount,
            CompletedItemCount = item.CompletedItemCount,
            Items = item.Items.Select(MapTodoItem).ToList(),
            Attachments = item.Attachments.Select(x => new TodoAttachmentEntity
            {
                TodoAttachmentId = x.TodoAttachmentId,
                FileName = x.FileName,
                ContentType = x.ContentType,
                Caption = x.Caption,
                FileSize = x.FileSize,
                UploadedAt = x.UploadedAt,
                DownloadUrl = x.DownloadUrl
            }).ToList()
        };
    }

    private static TodoItemEntity MapTodoItem(TodoItemDto item)
    {
        return new TodoItemEntity
        {
            TodoItemId = item.TodoItemId,
            TodoListId = item.TodoListId,
            ParentTodoItemId = item.ParentTodoItemId,
            Text = item.Text,
            IsCompleted = item.IsCompleted,
            SortOrder = item.SortOrder,
            Children = item.Children.Select(MapTodoItem).ToList()
        };
    }

    private static InvoiceLineEntity MapInvoiceLine(InvoiceLineDto item)
    {
        var invoice = new InvoiceEntity { InvoiceId = item.InvoiceId, InvoiceDirection = item.InvoiceDirection, InvoiceNumber = item.InvoiceNumber, InvoiceDate = item.InvoiceDate, HasSupplierInvoice = item.HasSupplierInvoice, AccountingCategory = item.AccountingCategory };
        var line = new InvoiceLineEntity
        {
            InvoiceLineId = item.InvoiceLineId,
            InvoiceId = item.InvoiceId,
            Invoice = invoice,
            Position = item.Position,
            ArticleNumber = item.ArticleNumber,
            Ean = item.Ean,
            Description = item.Description,
            Quantity = item.Quantity,
            Unit = item.Unit,
            NetUnitPrice = item.NetUnitPrice,
            MetalSurcharge = item.MetalSurcharge,
            GrossListPrice = item.GrossListPrice,
            GrossUnitPrice = item.GrossUnitPrice,
            PriceBasisQuantity = item.PriceBasisQuantity,
            ShippingNetShare = item.ShippingNetShare,
            ShippingGrossShare = item.ShippingGrossShare,
            LineTotal = item.LineTotal,
            GrossLineTotal = item.GrossLineTotal,
            IsGeneralSmallMaterial = item.IsGeneralSmallMaterial,
            IsInventoryStock = item.IsInventoryStock,
            IsPaid = item.IsPaid,
            PaidAt = item.PaidAt
        };

        line.Allocations = item.Allocations.Select(MapAllocation).ToList();
        foreach (var allocation in line.Allocations)
        {
            allocation.InvoiceLine = line;
        }

        return line;
    }

    private static LineAllocationEntity MapAllocation(AllocationDto item)
    {
        return new LineAllocationEntity
        {
            LineAllocationId = item.LineAllocationId,
            InvoiceLineId = item.InvoiceLineId,
            InvoiceLine = new InvoiceLineEntity
            {
                InvoiceLineId = item.InvoiceLineId,
                ArticleNumber = item.ArticleNumber,
                Description = item.Description,
                Unit = item.Unit,
                NetUnitPrice = item.NetUnitPrice,
                MetalSurcharge = item.MetalSurcharge,
                GrossListPrice = item.GrossListPrice,
                GrossUnitPrice = item.GrossUnitPrice,
                PriceBasisQuantity = item.PriceBasisQuantity,
                ShippingNetShare = item.ShippingNetShare,
                ShippingGrossShare = item.ShippingGrossShare,
                LineTotal = item.LineTotal,
                GrossLineTotal = item.GrossLineTotal,
                Invoice = new InvoiceEntity { InvoiceDirection = item.InvoiceDirection, InvoiceNumber = item.InvoiceNumber, InvoiceDate = item.InvoiceDate, HasSupplierInvoice = item.HasSupplierInvoice, AccountingCategory = item.AccountingCategory }
            },
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            ProjectId = item.ProjectId,
            Project = item.ProjectId.HasValue ? new ProjectEntity { ProjectId = item.ProjectId.Value, Name = item.ProjectName ?? "" } : null,
            AllocatedQuantity = item.AllocatedQuantity,
            CustomerUnitPrice = item.CustomerUnitPrice,
            RevenueInvoiceId = item.RevenueInvoiceId,
            IsSmallMaterial = item.IsSmallMaterial,
            AllocatedAt = item.AllocatedAt,
            CustomerInvoiceNumber = item.CustomerInvoiceNumber,
            CustomerInvoicedAt = item.CustomerInvoicedAt,
            IsPaid = item.IsPaid,
            PaidAt = item.PaidAt,
            ExportedMarkupPercent = item.ExportedMarkupPercent,
            ExportedUnitPrice = item.ExportedUnitPrice,
            ExportedLineTotal = item.ExportedLineTotal,
            LastExportedAt = item.LastExportedAt
        };
    }

    private static string GetContentType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static string? NormalizeOptionalEmail(string? emailAddress)
    {
        return string.IsNullOrWhiteSpace(emailAddress) ? null : emailAddress.Trim();
    }

    private async Task EnsureSuccessWithMessageAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Serverfehler: {(int)response.StatusCode} ({response.ReasonPhrase})"
            : body;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("message", out var messageElement))
                {
                    message = messageElement.GetString() ?? body;
                }
                else if (document.RootElement.TryGetProperty("title", out var titleElement))
                {
                    message = titleElement.GetString() ?? body;
                }
            }
            catch (JsonException)
            {
                message = body;
            }
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound && string.IsNullOrWhiteSpace(body))
        {
            message = "Der Server kennt diese Funktion noch nicht. Bitte den aktuellen Stand deployen und die WPF danach neu starten.";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private class BootstrapStateDto
    {
        public bool HasUsers { get; set; }
        public bool HasTenants { get; set; }
    }

    private class AuthResponseDto
    {
        public string AccessToken { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public AuthUserDto User { get; set; } = new();
        public AuthTenantDto Tenant { get; set; } = new();
        public AuthLicenseDto? License { get; set; }
    }

    private class AuthUserDto
    {
        public int AppUserId { get; set; }
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsPlatformAdmin { get; set; }
    }

    private class AuthTenantDto
    {
        public int TenantId { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
    }

    private class AuthLicenseDto
    {
        public int TenantLicenseId { get; set; }
        public string PlanCode { get; set; } = "";
        public string PlanName { get; set; } = "";
        public int MaxUsers { get; set; }
        public int MaxProjects { get; set; }
        public int MaxCustomers { get; set; }
        public bool IncludesMobileAccess { get; set; }
        public string BillingCycle { get; set; } = "";
        public decimal PriceNet { get; set; }
        public bool RenewsAutomatically { get; set; }
        public DateTime? NextBillingDate { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? GraceUntil { get; set; }
        public string Status { get; set; } = "";
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool IsActive { get; set; }
    }

    private class TenantUserDto
    {
        public int AppUserId { get; set; }
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsPlatformAdmin { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    private class CustomerDto
    {
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Street { get; set; } = "";
        public string HouseNumber { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
        public string EmailAddress { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public decimal DefaultMarkupPercent { get; set; }
    }

    private class CompanyProfileDto
    {
        public string CompanyName { get; set; } = "";
        public string CompanyStreet { get; set; } = "";
        public string CompanyHouseNumber { get; set; } = "";
        public string CompanyPostalCode { get; set; } = "";
        public string CompanyCity { get; set; } = "";
        public string? CompanyEmailAddress { get; set; }
        public string CompanyPhoneNumber { get; set; } = "";
        public string TaxNumber { get; set; } = "";
        public string BankName { get; set; } = "";
        public string BankIban { get; set; } = "";
        public string BankBic { get; set; } = "";
        public int NextRevenueInvoiceNumber { get; set; }
        public int NextCustomerNumber { get; set; }
        public string RevenueInvoiceNumberPreview { get; set; } = "";
    }

    private class ReserveRevenueInvoiceNumberDto
    {
        public string InvoiceNumber { get; set; } = "";
        public string CustomerNumber { get; set; } = "";
    }

    private class InvoiceSaveResultDto
    {
        public int InvoiceId { get; set; }
    }

    public class ProjectDto { public int ProjectId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public string Name { get; set; } = ""; public int OpenWorkItems { get; set; } public decimal LoggedHours { get; set; } }
    private class ProjectDetailsDto { public int ProjectId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public string Name { get; set; } = ""; public bool ConnectionUserSameAsCustomer { get; set; } public string ConnectionUserFirstName { get; set; } = ""; public string ConnectionUserLastName { get; set; } = ""; public string ConnectionUserStreet { get; set; } = ""; public string ConnectionUserHouseNumber { get; set; } = ""; public string ConnectionUserPostalCode { get; set; } = ""; public string ConnectionUserCity { get; set; } = ""; public string ConnectionUserParcelNumber { get; set; } = ""; public string ConnectionUserEmailAddress { get; set; } = ""; public string ConnectionUserPhoneNumber { get; set; } = ""; public bool PropertyOwnerSameAsCustomer { get; set; } public string PropertyOwnerFirstName { get; set; } = ""; public string PropertyOwnerLastName { get; set; } = ""; public string PropertyOwnerStreet { get; set; } = ""; public string PropertyOwnerHouseNumber { get; set; } = ""; public string PropertyOwnerPostalCode { get; set; } = ""; public string PropertyOwnerCity { get; set; } = ""; public string PropertyOwnerEmailAddress { get; set; } = ""; public string PropertyOwnerPhoneNumber { get; set; } = ""; }
    private class WorkTimeDto { public int WorkTimeEntryId { get; set; } public int? AppUserId { get; set; } public string? UserDisplayName { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public DateTime WorkDate { get; set; } public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } public int BreakMinutes { get; set; } public decimal HoursWorked { get; set; } public decimal HourlyRate { get; set; } public decimal TravelKilometers { get; set; } public decimal TravelRatePerKilometer { get; set; } public int? RevenueInvoiceId { get; set; } public string Description { get; set; } = ""; public string Comment { get; set; } = ""; public string? CustomerInvoiceNumber { get; set; } public DateTime? CustomerInvoicedAt { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public bool IsClockActive { get; set; } public DateTime? PauseStartedAtUtc { get; set; } public decimal LineTotal { get; set; } }
    private class BankAccountSummaryDto { public int TransactionCount { get; set; } public decimal? CurrentBalance { get; set; } public DateTime? LastBookingDate { get; set; } public string AccountIban { get; set; } = ""; public string AccountName { get; set; } = ""; }
    private class BankImportResultDto { public int ImportId { get; set; } public string FileName { get; set; } = ""; public string AccountName { get; set; } = ""; public string AccountIban { get; set; } = ""; public string Currency { get; set; } = "EUR"; public int ImportedTransactions { get; set; } public int SkippedTransactions { get; set; } public decimal? CurrentBalance { get; set; } public List<string>? Warnings { get; set; } }
    private class BankTransactionDto { public int BankTransactionId { get; set; } public int ImportId { get; set; } public DateTime BookingDate { get; set; } public DateTime? ValueDate { get; set; } public decimal Amount { get; set; } public decimal? BalanceAfterBooking { get; set; } public string Currency { get; set; } = "EUR"; public string CounterpartyName { get; set; } = ""; public string CounterpartyIban { get; set; } = ""; public string Purpose { get; set; } = ""; public string Reference { get; set; } = ""; public string TransactionType { get; set; } = ""; public string AccountIban { get; set; } = ""; public string ImportFileName { get; set; } = ""; public DateTime ImportedAt { get; set; } public bool IsIgnored { get; set; } public string IgnoredComment { get; set; } = ""; public DateTime? IgnoredAt { get; set; } public decimal AssignedAmount { get; set; } public decimal RemainingAmount { get; set; } public List<BankTransactionAssignmentDto> Assignments { get; set; } = []; }
    private class BankTransactionAssignmentDto { public int BankTransactionAssignmentId { get; set; } public int BankTransactionId { get; set; } public string AssignmentType { get; set; } = ""; public int? SupplierInvoiceId { get; set; } public int? RevenueInvoiceId { get; set; } public string? ManualCategory { get; set; } public string? SupplierInvoiceNumber { get; set; } public string? RevenueInvoiceNumber { get; set; } public string? CustomerInvoiceNumber { get; set; } public int? CustomerId { get; set; } public string PartyName { get; set; } = ""; public decimal AssignedAmount { get; set; } public string Note { get; set; } = ""; public DateTime AssignedAt { get; set; } }
    private class BankInvoiceCandidateDto { public string CandidateType { get; set; } = ""; public int? SupplierInvoiceId { get; set; } public int? RevenueInvoiceId { get; set; } public string? SupplierInvoiceNumber { get; set; } public string? RevenueInvoiceNumber { get; set; } public string? CustomerInvoiceNumber { get; set; } public int? CustomerId { get; set; } public string PartyName { get; set; } = ""; public DateTime InvoiceDate { get; set; } public decimal TotalAmount { get; set; } public decimal AssignedAmount { get; set; } public decimal RemainingAmount { get; set; } public bool IsPaid { get; set; } public decimal MatchScore { get; set; } public string MatchReason { get; set; } = ""; }
    private class TodoListDto { public int TodoListId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public string Title { get; set; } = ""; public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public int OpenItemCount { get; set; } public int CompletedItemCount { get; set; } public List<TodoItemDto> Items { get; set; } = []; public List<TodoAttachmentDto> Attachments { get; set; } = []; }
    private class TodoItemDto { public int TodoItemId { get; set; } public int TodoListId { get; set; } public int? ParentTodoItemId { get; set; } public string Text { get; set; } = ""; public bool IsCompleted { get; set; } public int SortOrder { get; set; } public List<TodoItemDto> Children { get; set; } = []; }
    private class TodoAttachmentDto { public int TodoAttachmentId { get; set; } public string FileName { get; set; } = ""; public string ContentType { get; set; } = ""; public string Caption { get; set; } = ""; public long FileSize { get; set; } public DateTime UploadedAt { get; set; } public string DownloadUrl { get; set; } = ""; }
    private class InvoiceLineDto { public int InvoiceLineId { get; set; } public int InvoiceId { get; set; } public string InvoiceDirection { get; set; } = ""; public string InvoiceNumber { get; set; } = ""; public DateTime InvoiceDate { get; set; } public bool HasSupplierInvoice { get; set; } public string AccountingCategory { get; set; } = ""; public int Position { get; set; } public string ArticleNumber { get; set; } = ""; public string Ean { get; set; } = ""; public string Description { get; set; } = ""; public decimal Quantity { get; set; } public string Unit { get; set; } = ""; public decimal NetUnitPrice { get; set; } public decimal MetalSurcharge { get; set; } public decimal GrossListPrice { get; set; } public decimal GrossUnitPrice { get; set; } public decimal PriceBasisQuantity { get; set; } public decimal ShippingNetShare { get; set; } public decimal ShippingGrossShare { get; set; } public decimal LineTotal { get; set; } public decimal GrossLineTotal { get; set; } public bool IsGeneralSmallMaterial { get; set; } public bool IsInventoryStock { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public List<AllocationDto> Allocations { get; set; } = []; }
    private class AllocationDto { public int LineAllocationId { get; set; } public int InvoiceLineId { get; set; } public string InvoiceDirection { get; set; } = ""; public string InvoiceNumber { get; set; } = ""; public DateTime InvoiceDate { get; set; } public bool HasSupplierInvoice { get; set; } public string AccountingCategory { get; set; } = ""; public string ArticleNumber { get; set; } = ""; public string Description { get; set; } = ""; public string Unit { get; set; } = ""; public decimal NetUnitPrice { get; set; } public decimal MetalSurcharge { get; set; } public decimal GrossListPrice { get; set; } public decimal GrossUnitPrice { get; set; } public decimal PriceBasisQuantity { get; set; } public decimal ShippingNetShare { get; set; } public decimal ShippingGrossShare { get; set; } public decimal LineTotal { get; set; } public decimal GrossLineTotal { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public decimal AllocatedQuantity { get; set; } public decimal CustomerUnitPrice { get; set; } public int? RevenueInvoiceId { get; set; } public bool IsSmallMaterial { get; set; } public DateTime AllocatedAt { get; set; } public string? CustomerInvoiceNumber { get; set; } public DateTime? CustomerInvoicedAt { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public decimal ExportedMarkupPercent { get; set; } public decimal ExportedUnitPrice { get; set; } public decimal ExportedLineTotal { get; set; } public DateTime? LastExportedAt { get; set; } }
    private class InvoiceListDto { public int InvoiceId { get; set; } public string InvoiceDirection { get; set; } = ""; public string InvoiceStatus { get; set; } = ""; public int? CustomerId { get; set; } public string InvoiceNumber { get; set; } = ""; public DateTime InvoiceDate { get; set; } public DateTime? DeliveryDate { get; set; } public DateTime? PaymentDueDate { get; set; } public string SupplierName { get; set; } = ""; public bool HasSupplierInvoice { get; set; } public string AccountingCategory { get; set; } = ""; public string Subject { get; set; } = ""; public bool ApplySmallBusinessRegulation { get; set; } public decimal InvoiceTotalAmount { get; set; } public decimal ShippingCostNet { get; set; } public decimal ShippingCostGross { get; set; } public string OriginalPdfFileName { get; set; } = ""; public bool HasStoredPdf { get; set; } public DateTime? DraftSavedAt { get; set; } public DateTime? FinalizedAt { get; set; } public DateTime? CancelledAt { get; set; } public string CancellationReason { get; set; } = ""; }
    private class InvoiceDetailDto : InvoiceListDto { public List<SaveInvoiceLineDto> Lines { get; set; } = []; }
    private class SaveInvoiceLineDto { public int Position { get; set; } public string ArticleNumber { get; set; } = ""; public string Ean { get; set; } = ""; public string Description { get; set; } = ""; public decimal Quantity { get; set; } public string Unit { get; set; } = ""; public decimal NetUnitPrice { get; set; } public decimal MetalSurcharge { get; set; } public decimal GrossListPrice { get; set; } public decimal GrossUnitPrice { get; set; } public decimal PriceBasisQuantity { get; set; } public decimal ShippingNetShare { get; set; } public decimal ShippingGrossShare { get; set; } public decimal LineTotal { get; set; } public decimal GrossLineTotal { get; set; } }
}

public class AnalyticsResponseDto
{
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
    public decimal OpenRevenue { get; set; }
    public List<AnalyticsMonthViewModel> Monthly { get; set; } = new();
    public List<ProjectAnalyticsRow> Projects { get; set; } = new();
    public List<ExpenseCategoryAnalyticsRow> ExpenseCategories { get; set; } = new();
}






















