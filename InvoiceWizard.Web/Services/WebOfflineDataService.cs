using System.Text.Json;
using InvoiceWizard.Web.Models;
using Microsoft.JSInterop;

namespace InvoiceWizard.Web.Services;

public class WebOfflineDataService(BackendApiClient api, WebAuthSession auth, IJSRuntime js)
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> IsOnlineAsync()
        => await js.InvokeAsync<bool>("invoiceWizardStorage.isOnline");

    public async Task SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!auth.IsAuthenticated || !await IsOnlineAsync())
        {
            return;
        }

        var noteQueue = await LoadValueAsync<List<PendingNoteOperation>>(StorageKeys.PendingNoteOperations, cancellationToken) ?? [];
        if (noteQueue.Count > 0)
        {
            await SyncNoteOperationsAsync(noteQueue, cancellationToken);
        }

        var workQueue = await LoadValueAsync<List<PendingWorkTimeOperation>>(StorageKeys.PendingWorkTimeOperations, cancellationToken) ?? [];
        if (workQueue.Count > 0)
        {
            await SyncWorkTimeOperationsAsync(workQueue, cancellationToken);
        }
    }

    public async Task<List<CustomerItem>> GetCustomersAsync(bool activeProjectsOnly, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{StorageKeys.Customers}_{activeProjectsOnly}";
        if (await IsOnlineAsync())
        {
            var customers = await api.GetCustomersAsync(activeProjectsOnly, cancellationToken);
            await SaveValueAsync(cacheKey, customers, cancellationToken);
            return customers;
        }

        return await LoadValueAsync<List<CustomerItem>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<List<ProjectItem>> GetProjectsAsync(int? customerId = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{StorageKeys.Projects}_{customerId?.ToString() ?? "all"}_{includeInactive}";
        if (await IsOnlineAsync())
        {
            var projects = await api.GetProjectsAsync(customerId, includeInactive, cancellationToken);
            await SaveValueAsync(cacheKey, projects, cancellationToken);
            return projects;
        }

        return await LoadValueAsync<List<ProjectItem>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<ProjectDetailsItem?> GetProjectDetailsAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{StorageKeys.ProjectDetails}_{projectId}";
        if (await IsOnlineAsync())
        {
            var details = await api.GetProjectDetailsAsync(projectId, cancellationToken);
            await SaveValueAsync(cacheKey, details, cancellationToken);
            return details;
        }

        return await LoadValueAsync<ProjectDetailsItem>(cacheKey, cancellationToken);
    }

    public async Task<List<CalendarUserItem>> GetCalendarUsersAsync(CancellationToken cancellationToken = default)
    {
        if (await IsOnlineAsync())
        {
            var users = await api.GetCalendarUsersAsync(cancellationToken);
            await SaveValueAsync(StorageKeys.CalendarUsers, users, cancellationToken);
            return users;
        }

        return await LoadValueAsync<List<CalendarUserItem>>(StorageKeys.CalendarUsers, cancellationToken) ?? [];
    }

    public async Task<List<CalendarEntryItem>> GetCalendarEntriesAsync(int? appUserId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{StorageKeys.CalendarEntries}_{appUserId?.ToString() ?? "all"}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}";
        if (await IsOnlineAsync())
        {
            var entries = await api.GetCalendarEntriesAsync(appUserId, fromDate, toDate, cancellationToken);
            await SaveValueAsync(cacheKey, entries, cancellationToken);
            return entries;
        }

        return await LoadValueAsync<List<CalendarEntryItem>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<List<TodoListItem>> GetTodoListsAsync(int customerId, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await SyncPendingAsync(cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        return await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<List<TodoListItem>> CreateTodoListAsync(int customerId, int? projectId, string title, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            var created = await api.CreateTodoListAsync(customerId, projectId, title, cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        var todoLists = await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
        var tempId = NextTempId();
        todoLists.Insert(0, new TodoListItem
        {
            TodoListId = tempId,
            CustomerId = customerId,
            CustomerName = string.Empty,
            ProjectId = projectId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OpenItemCount = 0,
            CompletedItemCount = 0,
            Items = [],
            Attachments = []
        });
        await SaveValueAsync(cacheKey, todoLists, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingNoteOperations, new PendingNoteOperation
        {
            Operation = "CreateList",
            CustomerId = customerId,
            ProjectId = projectId,
            LocalListId = tempId,
            Title = title
        }, cancellationToken);
        return todoLists;
    }

    public async Task<List<TodoListItem>> CreateTodoItemAsync(int customerId, int? projectId, int todoListId, string text, int? parentTodoItemId = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await api.CreateTodoItemAsync(todoListId, text, parentTodoItemId, cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        var todoLists = await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
        var list = todoLists.FirstOrDefault(x => x.TodoListId == todoListId);
        if (list is null)
        {
            return todoLists;
        }

        var item = new TodoItem
        {
            TodoItemId = NextTempId(),
            TodoListId = todoListId,
            ParentTodoItemId = parentTodoItemId,
            Text = text,
            IsCompleted = false,
            SortOrder = GetNextSortOrder(list.Items, parentTodoItemId)
        };

        if (parentTodoItemId.HasValue)
        {
            var parent = FindTodoItem(list.Items, parentTodoItemId.Value);
            parent?.Children.Add(item);
        }
        else
        {
            list.Items.Add(item);
        }

        RecountList(list);
        list.UpdatedAt = DateTime.UtcNow;
        await SaveValueAsync(cacheKey, todoLists, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingNoteOperations, new PendingNoteOperation
        {
            Operation = "CreateItem",
            CustomerId = customerId,
            ProjectId = projectId,
            LocalListId = todoListId,
            LocalItemId = item.TodoItemId,
            ParentLocalItemId = parentTodoItemId,
            Text = text
        }, cancellationToken);
        return todoLists;
    }

    public async Task<List<TodoListItem>> UpdateTodoItemStateAsync(int customerId, int? projectId, int todoItemId, bool isCompleted, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await api.UpdateTodoItemStateAsync(todoItemId, isCompleted, cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        var todoLists = await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
        var (list, item) = FindTodoItem(todoLists, todoItemId);
        if (list is null || item is null)
        {
            return todoLists;
        }

        item.IsCompleted = isCompleted;
        RecountList(list);
        list.UpdatedAt = DateTime.UtcNow;
        await SaveValueAsync(cacheKey, todoLists, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingNoteOperations, new PendingNoteOperation
        {
            Operation = "ToggleItem",
            CustomerId = customerId,
            ProjectId = projectId,
            LocalItemId = todoItemId,
            IsCompleted = isCompleted
        }, cancellationToken);
        return todoLists;
    }

    public async Task<List<TodoListItem>> DeleteTodoItemAsync(int customerId, int? projectId, int todoItemId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await api.DeleteTodoItemAsync(todoItemId, cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        var todoLists = await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
        var (list, item) = FindTodoItem(todoLists, todoItemId);
        if (list is null || item is null)
        {
            return todoLists;
        }

        RemoveTodoItem(list.Items, todoItemId);
        RecountList(list);
        list.UpdatedAt = DateTime.UtcNow;
        await SaveValueAsync(cacheKey, todoLists, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingNoteOperations, new PendingNoteOperation
        {
            Operation = "DeleteItem",
            CustomerId = customerId,
            ProjectId = projectId,
            LocalItemId = todoItemId
        }, cancellationToken);
        return todoLists;
    }

    public async Task<List<TodoListItem>> DeleteTodoListAsync(int customerId, int? projectId, int todoListId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetTodoCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await api.DeleteTodoListAsync(todoListId, cancellationToken);
            var lists = await api.GetTodoListsAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, lists, cancellationToken);
            return lists;
        }

        var todoLists = await LoadValueAsync<List<TodoListItem>>(cacheKey, cancellationToken) ?? [];
        todoLists.RemoveAll(x => x.TodoListId == todoListId);
        await SaveValueAsync(cacheKey, todoLists, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingNoteOperations, new PendingNoteOperation
        {
            Operation = "DeleteList",
            CustomerId = customerId,
            ProjectId = projectId,
            LocalListId = todoListId
        }, cancellationToken);
        return todoLists;
    }

    public async Task<List<WorkTimeItem>> GetWorkTimesAsync(int? customerId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetWorkTimeCacheKey(customerId, projectId);
        if (await IsOnlineAsync())
        {
            await SyncPendingAsync(cancellationToken);
            var entries = await api.GetWorkTimesAsync(customerId, projectId, cancellationToken);
            await SaveValueAsync(cacheKey, entries, cancellationToken);
            return entries;
        }

        return await LoadValueAsync<List<WorkTimeItem>>(cacheKey, cancellationToken) ?? [];
    }

    public async Task<List<WorkTimeItem>> SaveWorkTimeAsync(SaveWorkTimeModel model, int? entryId = null, int? customerIdFilter = null, int? projectIdFilter = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetWorkTimeCacheKey(customerIdFilter, projectIdFilter);
        if (await IsOnlineAsync())
        {
            if (entryId.HasValue)
            {
                await api.UpdateWorkTimeAsync(entryId.Value, model, cancellationToken);
            }
            else
            {
                await api.CreateWorkTimeAsync(model, cancellationToken);
            }

            var entries = await api.GetWorkTimesAsync(customerIdFilter, projectIdFilter, cancellationToken);
            await SaveValueAsync(cacheKey, entries, cancellationToken);
            return entries;
        }

        var entriesOffline = await LoadValueAsync<List<WorkTimeItem>>(cacheKey, cancellationToken) ?? [];
        if (entryId.HasValue)
        {
            var existing = entriesOffline.FirstOrDefault(x => x.WorkTimeEntryId == entryId.Value);
            if (existing is not null)
            {
                ApplyWorkTime(existing, model);
            }

            await EnqueueAsync(StorageKeys.PendingWorkTimeOperations, new PendingWorkTimeOperation
            {
                Operation = "Update",
                LocalEntryId = entryId.Value,
                Model = CloneWorkTimeModel(model)
            }, cancellationToken);
        }
        else
        {
            var tempId = NextTempId();
            entriesOffline.Insert(0, CreateOfflineWorkTime(tempId, model, auth.CurrentSession?.User.DisplayName ?? string.Empty));
            await EnqueueAsync(StorageKeys.PendingWorkTimeOperations, new PendingWorkTimeOperation
            {
                Operation = "Create",
                LocalEntryId = tempId,
                Model = CloneWorkTimeModel(model)
            }, cancellationToken);
        }

        await SaveValueAsync(cacheKey, entriesOffline, cancellationToken);
        return entriesOffline;
    }

    public async Task<List<WorkTimeItem>> DeleteWorkTimeAsync(int entryId, int? customerIdFilter = null, int? projectIdFilter = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetWorkTimeCacheKey(customerIdFilter, projectIdFilter);
        if (await IsOnlineAsync())
        {
            await api.DeleteWorkTimeAsync(entryId, cancellationToken);
            var entries = await api.GetWorkTimesAsync(customerIdFilter, projectIdFilter, cancellationToken);
            await SaveValueAsync(cacheKey, entries, cancellationToken);
            return entries;
        }

        var entriesOffline = await LoadValueAsync<List<WorkTimeItem>>(cacheKey, cancellationToken) ?? [];
        entriesOffline.RemoveAll(x => x.WorkTimeEntryId == entryId);
        await SaveValueAsync(cacheKey, entriesOffline, cancellationToken);
        await EnqueueAsync(StorageKeys.PendingWorkTimeOperations, new PendingWorkTimeOperation
        {
            Operation = "Delete",
            LocalEntryId = entryId
        }, cancellationToken);
        return entriesOffline;
    }

    private async Task SyncNoteOperationsAsync(List<PendingNoteOperation> operations, CancellationToken cancellationToken)
    {
        var listIdMap = new Dictionary<int, int>();
        var itemIdMap = new Dictionary<int, int>();

        foreach (var operation in operations)
        {
            switch (operation.Operation)
            {
                case "CreateList":
                    var createdList = await api.CreateTodoListAsync(operation.CustomerId, operation.ProjectId, operation.Title ?? string.Empty, cancellationToken);
                    listIdMap[operation.LocalListId] = createdList.TodoListId;
                    break;
                case "CreateItem":
                    var targetListId = ResolveMappedId(operation.LocalListId, listIdMap);
                    int? parentId = operation.ParentLocalItemId.HasValue ? ResolveMappedId(operation.ParentLocalItemId.Value, itemIdMap) : null;
                    var refreshed = await api.CreateTodoItemAsync(targetListId, operation.Text ?? string.Empty, parentId, cancellationToken);
                    if (operation.LocalItemId != 0)
                    {
                        var latestItemId = FindLastTodoItemId(refreshed, operation.Text ?? string.Empty);
                        if (latestItemId.HasValue)
                        {
                            itemIdMap[operation.LocalItemId] = latestItemId.Value;
                        }
                    }
                    break;
                case "ToggleItem":
                    await api.UpdateTodoItemStateAsync(ResolveMappedId(operation.LocalItemId, itemIdMap), operation.IsCompleted, cancellationToken);
                    break;
                case "DeleteItem":
                    await api.DeleteTodoItemAsync(ResolveMappedId(operation.LocalItemId, itemIdMap), cancellationToken);
                    break;
                case "DeleteList":
                    await api.DeleteTodoListAsync(ResolveMappedId(operation.LocalListId, listIdMap), cancellationToken);
                    break;
            }
        }

        var touchedGroups = operations
            .Select(x => (x.CustomerId, x.ProjectId))
            .Distinct()
            .ToList();
        foreach (var group in touchedGroups)
        {
            var lists = await api.GetTodoListsAsync(group.CustomerId, group.ProjectId, cancellationToken);
            await SaveValueAsync(GetTodoCacheKey(group.CustomerId, group.ProjectId), lists, cancellationToken);
        }

        await RemoveValueAsync(StorageKeys.PendingNoteOperations, cancellationToken);
    }

    private async Task SyncWorkTimeOperationsAsync(List<PendingWorkTimeOperation> operations, CancellationToken cancellationToken)
    {
        var entryIdMap = new Dictionary<int, int>();

        foreach (var operation in operations)
        {
            switch (operation.Operation)
            {
                case "Create":
                    if (operation.Model is null) break;
                    var created = await api.CreateWorkTimeAsync(operation.Model, cancellationToken);
                    entryIdMap[operation.LocalEntryId] = created.WorkTimeEntryId;
                    break;
                case "Update":
                    if (operation.Model is null) break;
                    await api.UpdateWorkTimeAsync(ResolveMappedId(operation.LocalEntryId, entryIdMap), operation.Model, cancellationToken);
                    break;
                case "Delete":
                    await api.DeleteWorkTimeAsync(ResolveMappedId(operation.LocalEntryId, entryIdMap), cancellationToken);
                    break;
            }
        }

        var allEntries = await api.GetWorkTimesAsync(null, null, cancellationToken);
        await SaveValueAsync(GetWorkTimeCacheKey(null, null), allEntries, cancellationToken);

        var grouped = allEntries.GroupBy(x => (x.CustomerId, x.ProjectId));
        foreach (var group in grouped)
        {
            await SaveValueAsync(GetWorkTimeCacheKey(group.Key.CustomerId, group.Key.ProjectId), group.ToList(), cancellationToken);
        }

        await RemoveValueAsync(StorageKeys.PendingWorkTimeOperations, cancellationToken);
    }

    private async Task<T?> LoadValueAsync<T>(string key, CancellationToken cancellationToken)
    {
        var json = await js.InvokeAsync<string?>("invoiceWizardStorage.getItem", cancellationToken, key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    private Task SaveValueAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        return js.InvokeVoidAsync("invoiceWizardStorage.setItem", cancellationToken, key, json).AsTask();
    }

    private Task RemoveValueAsync(string key, CancellationToken cancellationToken)
        => js.InvokeVoidAsync("invoiceWizardStorage.removeItem", cancellationToken, key).AsTask();

    private async Task EnqueueAsync<T>(string key, T operation, CancellationToken cancellationToken)
    {
        var items = await LoadValueAsync<List<T>>(key, cancellationToken) ?? [];
        items.Add(operation);
        await SaveValueAsync(key, items, cancellationToken);
    }

    private static int NextTempId() => -Math.Abs((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue));
    private static int ResolveMappedId(int localId, IReadOnlyDictionary<int, int> map) => map.TryGetValue(localId, out var resolved) ? resolved : localId;
    private static string GetTodoCacheKey(int customerId, int? projectId) => $"{StorageKeys.TodoLists}_{customerId}_{projectId?.ToString() ?? "all"}";
    private static string GetWorkTimeCacheKey(int? customerId, int? projectId) => $"{StorageKeys.WorkTimes}_{customerId?.ToString() ?? "all"}_{projectId?.ToString() ?? "all"}";

    private static (TodoListItem? List, TodoItem? Item) FindTodoItem(IEnumerable<TodoListItem> lists, int itemId)
    {
        foreach (var list in lists)
        {
            var item = FindTodoItem(list.Items, itemId);
            if (item is not null)
            {
                return (list, item);
            }
        }

        return (null, null);
    }

    private static TodoItem? FindTodoItem(IEnumerable<TodoItem> items, int itemId)
    {
        foreach (var item in items)
        {
            if (item.TodoItemId == itemId)
            {
                return item;
            }

            var child = FindTodoItem(item.Children, itemId);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static bool RemoveTodoItem(List<TodoItem> items, int itemId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index].TodoItemId == itemId)
            {
                items.RemoveAt(index);
                return true;
            }

            if (RemoveTodoItem(items[index].Children, itemId))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetNextSortOrder(IEnumerable<TodoItem> rootItems, int? parentTodoItemId)
    {
        var siblings = parentTodoItemId.HasValue
            ? FindTodoItem(rootItems, parentTodoItemId.Value)?.Children ?? []
            : rootItems.ToList();
        return siblings.Count == 0 ? 0 : siblings.Max(x => x.SortOrder) + 1;
    }

    private static void RecountList(TodoListItem list)
    {
        var items = Flatten(list.Items).ToList();
        list.OpenItemCount = items.Count(x => !x.IsCompleted);
        list.CompletedItemCount = items.Count(x => x.IsCompleted);
    }

    private static IEnumerable<TodoItem> Flatten(IEnumerable<TodoItem> items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.Children))
            {
                yield return child;
            }
        }
    }

    private static int? FindLastTodoItemId(TodoListItem list, string text)
        => Flatten(list.Items).Where(x => string.Equals(x.Text, text, StringComparison.Ordinal)).OrderByDescending(x => x.TodoItemId).Select(x => (int?)x.TodoItemId).FirstOrDefault();

    private static WorkTimeItem CreateOfflineWorkTime(int tempId, SaveWorkTimeModel model, string userDisplayName)
    {
        var duration = model.EndTime - model.StartTime - TimeSpan.FromMinutes(model.BreakMinutes);
        var hours = duration <= TimeSpan.Zero ? 0m : Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
        return new WorkTimeItem
        {
            WorkTimeEntryId = tempId,
            AppUserId = null,
            UserDisplayName = userDisplayName,
            CustomerId = model.CustomerId,
            ProjectId = model.ProjectId,
            WorkDate = model.WorkDate,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            BreakMinutes = model.BreakMinutes,
            HoursWorked = hours,
            HourlyRate = model.HourlyRate,
            TravelKilometers = model.TravelKilometers,
            TravelRatePerKilometer = model.TravelRatePerKilometer,
            Description = model.Description,
            Comment = model.Comment,
            LineTotal = (hours * model.HourlyRate) + (model.TravelKilometers * model.TravelRatePerKilometer)
        };
    }

    private static void ApplyWorkTime(WorkTimeItem target, SaveWorkTimeModel model)
    {
        var duration = model.EndTime - model.StartTime - TimeSpan.FromMinutes(model.BreakMinutes);
        var hours = duration <= TimeSpan.Zero ? 0m : Math.Round((decimal)duration.TotalHours, 2, MidpointRounding.AwayFromZero);
        target.CustomerId = model.CustomerId;
        target.ProjectId = model.ProjectId;
        target.WorkDate = model.WorkDate;
        target.StartTime = model.StartTime;
        target.EndTime = model.EndTime;
        target.BreakMinutes = model.BreakMinutes;
        target.HoursWorked = hours;
        target.HourlyRate = model.HourlyRate;
        target.TravelKilometers = model.TravelKilometers;
        target.TravelRatePerKilometer = model.TravelRatePerKilometer;
        target.Description = model.Description;
        target.Comment = model.Comment;
        target.LineTotal = (hours * model.HourlyRate) + (model.TravelKilometers * model.TravelRatePerKilometer);
    }

    private static SaveWorkTimeModel CloneWorkTimeModel(SaveWorkTimeModel model) => new()
    {
        CustomerId = model.CustomerId,
        ProjectId = model.ProjectId,
        WorkDate = model.WorkDate,
        StartTime = model.StartTime,
        EndTime = model.EndTime,
        BreakMinutes = model.BreakMinutes,
        HourlyRate = model.HourlyRate,
        TravelKilometers = model.TravelKilometers,
        TravelRatePerKilometer = model.TravelRatePerKilometer,
        Description = model.Description,
        Comment = model.Comment
    };

    private static class StorageKeys
    {
        public const string Customers = "invoicewizard.offline.customers";
        public const string Projects = "invoicewizard.offline.projects";
        public const string ProjectDetails = "invoicewizard.offline.projectdetails";
        public const string CalendarUsers = "invoicewizard.offline.calendar.users";
        public const string CalendarEntries = "invoicewizard.offline.calendar.entries";
        public const string TodoLists = "invoicewizard.offline.todolists";
        public const string WorkTimes = "invoicewizard.offline.worktimes";
        public const string PendingNoteOperations = "invoicewizard.offline.pending.notes";
        public const string PendingWorkTimeOperations = "invoicewizard.offline.pending.worktimes";
    }

    private sealed class PendingNoteOperation
    {
        public string Operation { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public int? ProjectId { get; set; }
        public int LocalListId { get; set; }
        public int LocalItemId { get; set; }
        public int? ParentLocalItemId { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
        public bool IsCompleted { get; set; }
    }

    private sealed class PendingWorkTimeOperation
    {
        public string Operation { get; set; } = string.Empty;
        public int LocalEntryId { get; set; }
        public SaveWorkTimeModel? Model { get; set; }
    }
}
