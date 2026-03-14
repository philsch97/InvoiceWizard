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
        response.EnsureSuccessStatusCode();
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
        response.EnsureSuccessStatusCode();
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
        response.EnsureSuccessStatusCode();
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

    public async Task<CustomerEntity> SaveCustomerAsync(CustomerEntity customer, int? customerId = null)
    {
        var payload = new
        {
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

    public async Task UpdateWorkTimeStatusAsync(int id, string? invoiceNumber, bool markInvoiced, bool markPaid)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/worktimeentries/{id}/status", new { customerInvoiceNumber = invoiceNumber, markInvoiced, markPaid });
        response.EnsureSuccessStatusCode();
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

    public async Task SaveInvoiceAsync(string invoiceNumber, DateTime invoiceDate, string supplierName, string sourcePdfPath, string contentHash, IEnumerable<ManualInvoiceLineInput> lines, bool hasSupplierInvoice = true)
    {
        var response = await _httpClient.PostAsJsonAsync("api/invoices", new
        {
            hasSupplierInvoice,
            invoiceNumber,
            invoiceDate,
            supplierName,
            sourcePdfPath,
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
                priceBasisQuantity = line.PriceBasisQuantity,
                line.LineTotal
            }).ToList()
        });
        response.EnsureSuccessStatusCode();
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
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            ProjectId = item.ProjectId,
            Project = item.ProjectId.HasValue ? new ProjectEntity { ProjectId = item.ProjectId.Value, Name = item.ProjectName ?? "" } : null,
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
            ExportedLineTotal = item.LineTotal,
            ExportedUnitPrice = item.HourlyRate
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
        var invoice = new InvoiceEntity { InvoiceId = item.InvoiceId, InvoiceNumber = item.InvoiceNumber, InvoiceDate = item.InvoiceDate, HasSupplierInvoice = item.HasSupplierInvoice };
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
            PriceBasisQuantity = item.PriceBasisQuantity,
            LineTotal = item.LineTotal,
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
                PriceBasisQuantity = item.PriceBasisQuantity,
                Invoice = new InvoiceEntity { InvoiceNumber = item.InvoiceNumber, InvoiceDate = item.InvoiceDate, HasSupplierInvoice = item.HasSupplierInvoice }
            },
            CustomerId = item.CustomerId,
            Customer = new CustomerEntity { CustomerId = item.CustomerId, Name = item.CustomerName },
            ProjectId = item.ProjectId,
            Project = item.ProjectId.HasValue ? new ProjectEntity { ProjectId = item.ProjectId.Value, Name = item.ProjectName ?? "" } : null,
            AllocatedQuantity = item.AllocatedQuantity,
            CustomerUnitPrice = item.CustomerUnitPrice,
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

    public class ProjectDto { public int ProjectId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public string Name { get; set; } = ""; public int OpenWorkItems { get; set; } public decimal LoggedHours { get; set; } }
    private class ProjectDetailsDto { public int ProjectId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public string Name { get; set; } = ""; public bool ConnectionUserSameAsCustomer { get; set; } public string ConnectionUserFirstName { get; set; } = ""; public string ConnectionUserLastName { get; set; } = ""; public string ConnectionUserStreet { get; set; } = ""; public string ConnectionUserHouseNumber { get; set; } = ""; public string ConnectionUserPostalCode { get; set; } = ""; public string ConnectionUserCity { get; set; } = ""; public string ConnectionUserParcelNumber { get; set; } = ""; public string ConnectionUserEmailAddress { get; set; } = ""; public string ConnectionUserPhoneNumber { get; set; } = ""; public bool PropertyOwnerSameAsCustomer { get; set; } public string PropertyOwnerFirstName { get; set; } = ""; public string PropertyOwnerLastName { get; set; } = ""; public string PropertyOwnerStreet { get; set; } = ""; public string PropertyOwnerHouseNumber { get; set; } = ""; public string PropertyOwnerPostalCode { get; set; } = ""; public string PropertyOwnerCity { get; set; } = ""; public string PropertyOwnerEmailAddress { get; set; } = ""; public string PropertyOwnerPhoneNumber { get; set; } = ""; }
    private class WorkTimeDto { public int WorkTimeEntryId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public DateTime WorkDate { get; set; } public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } public int BreakMinutes { get; set; } public decimal HoursWorked { get; set; } public decimal HourlyRate { get; set; } public decimal TravelKilometers { get; set; } public decimal TravelRatePerKilometer { get; set; } public string Description { get; set; } = ""; public string Comment { get; set; } = ""; public string? CustomerInvoiceNumber { get; set; } public DateTime? CustomerInvoicedAt { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public decimal LineTotal { get; set; } }
    private class TodoListDto { public int TodoListId { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public string Title { get; set; } = ""; public DateTime CreatedAt { get; set; } public DateTime UpdatedAt { get; set; } public int OpenItemCount { get; set; } public int CompletedItemCount { get; set; } public List<TodoItemDto> Items { get; set; } = []; public List<TodoAttachmentDto> Attachments { get; set; } = []; }
    private class TodoItemDto { public int TodoItemId { get; set; } public int TodoListId { get; set; } public int? ParentTodoItemId { get; set; } public string Text { get; set; } = ""; public bool IsCompleted { get; set; } public int SortOrder { get; set; } public List<TodoItemDto> Children { get; set; } = []; }
    private class TodoAttachmentDto { public int TodoAttachmentId { get; set; } public string FileName { get; set; } = ""; public string ContentType { get; set; } = ""; public string Caption { get; set; } = ""; public long FileSize { get; set; } public DateTime UploadedAt { get; set; } public string DownloadUrl { get; set; } = ""; }
    private class InvoiceLineDto { public int InvoiceLineId { get; set; } public int InvoiceId { get; set; } public string InvoiceNumber { get; set; } = ""; public DateTime InvoiceDate { get; set; } public bool HasSupplierInvoice { get; set; } public int Position { get; set; } public string ArticleNumber { get; set; } = ""; public string Ean { get; set; } = ""; public string Description { get; set; } = ""; public decimal Quantity { get; set; } public string Unit { get; set; } = ""; public decimal NetUnitPrice { get; set; } public decimal MetalSurcharge { get; set; } public decimal GrossListPrice { get; set; } public decimal PriceBasisQuantity { get; set; } public decimal LineTotal { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public List<AllocationDto> Allocations { get; set; } = []; }
    private class AllocationDto { public int LineAllocationId { get; set; } public int InvoiceLineId { get; set; } public string InvoiceNumber { get; set; } = ""; public DateTime InvoiceDate { get; set; } public bool HasSupplierInvoice { get; set; } public string ArticleNumber { get; set; } = ""; public string Description { get; set; } = ""; public string Unit { get; set; } = ""; public decimal NetUnitPrice { get; set; } public decimal MetalSurcharge { get; set; } public decimal PriceBasisQuantity { get; set; } public int CustomerId { get; set; } public string CustomerName { get; set; } = ""; public int? ProjectId { get; set; } public string? ProjectName { get; set; } public decimal AllocatedQuantity { get; set; } public decimal CustomerUnitPrice { get; set; } public bool IsSmallMaterial { get; set; } public DateTime AllocatedAt { get; set; } public string? CustomerInvoiceNumber { get; set; } public DateTime? CustomerInvoicedAt { get; set; } public bool IsPaid { get; set; } public DateTime? PaidAt { get; set; } public decimal ExportedMarkupPercent { get; set; } public decimal ExportedUnitPrice { get; set; } public decimal ExportedLineTotal { get; set; } public DateTime? LastExportedAt { get; set; } }
}

public class AnalyticsResponseDto
{
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
    public decimal Profit { get; set; }
    public decimal OpenRevenue { get; set; }
    public List<AnalyticsMonthViewModel> Monthly { get; set; } = new();
    public List<ProjectAnalyticsRow> Projects { get; set; } = new();
}






















