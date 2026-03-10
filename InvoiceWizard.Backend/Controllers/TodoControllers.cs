using InvoiceWizard.Backend.Contracts;
using InvoiceWizard.Backend.Data;
using InvoiceWizard.Backend.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Controllers;

[ApiController]
[Route("api/todolists")]
public class TodoListsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TodoListDto>>> GetTodoLists([FromQuery] int customerId, [FromQuery] int? projectId)
    {
        var query = db.TodoLists
            .Include(x => x.Customer)
            .Include(x => x.Project)
            .Include(x => x.Items)
            .Include(x => x.Attachments)
            .Where(x => x.CustomerId == customerId)
            .AsSplitQuery()
            .AsQueryable();

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        var lists = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Title)
            .ToListAsync();

        return Ok(lists.Select(MapTodoList).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<TodoListDto>> CreateTodoList([FromBody] SaveTodoListRequest request)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.CustomerId == request.CustomerId);
        if (customer is null)
        {
            return NotFound("Kunde nicht gefunden.");
        }

        if (request.ProjectId.HasValue)
        {
            var projectExists = await db.Projects.AnyAsync(x => x.ProjectId == request.ProjectId.Value && x.CustomerId == request.CustomerId);
            if (!projectExists)
            {
                return ValidationProblem("Das ausgewaehlte Projekt gehoert nicht zum Kunden.");
            }
        }

        var list = new TodoList
        {
            CustomerId = request.CustomerId,
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.TodoLists.Add(list);
        await db.SaveChangesAsync();
        return Ok(await LoadMappedTodoListAsync(list.TodoListId));
    }

    [HttpDelete("{todoListId:int}")]
    public async Task<IActionResult> DeleteTodoList(int todoListId)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(x => x.TodoListId == todoListId);
        if (list is null)
        {
            return NotFound();
        }

        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => x.TodoListId == todoListId).ToListAsync());
        db.TodoAttachments.RemoveRange(await db.TodoAttachments.Where(x => x.TodoListId == todoListId).ToListAsync());
        db.TodoLists.Remove(list);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{todoListId:int}/items")]
    public async Task<ActionResult<TodoListDto>> CreateTodoItem(int todoListId, [FromBody] SaveTodoItemRequest request)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(x => x.TodoListId == todoListId);
        if (list is null)
        {
            return NotFound();
        }

        if (request.ParentTodoItemId.HasValue)
        {
            var parent = await db.TodoItems.FirstOrDefaultAsync(x => x.TodoItemId == request.ParentTodoItemId.Value && x.TodoListId == todoListId);
            if (parent is null)
            {
                return ValidationProblem("Der ausgewaehlte Oberpunkt wurde nicht gefunden.");
            }
        }

        var sortOrder = await db.TodoItems
            .Where(x => x.TodoListId == todoListId && x.ParentTodoItemId == request.ParentTodoItemId)
            .Select(x => (int?)x.SortOrder)
            .MaxAsync() ?? 0;

        db.TodoItems.Add(new TodoItem
        {
            TodoListId = todoListId,
            ParentTodoItemId = request.ParentTodoItemId,
            Text = request.Text.Trim(),
            IsCompleted = false,
            SortOrder = sortOrder + 1,
            CreatedAt = DateTime.UtcNow
        });

        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(await LoadMappedTodoListAsync(todoListId));
    }

    [HttpPost("{todoListId:int}/attachments")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<TodoListDto>> UploadAttachment(int todoListId, [FromForm] IFormFile file, [FromForm] string? caption)
    {
        var list = await db.TodoLists.FirstOrDefaultAsync(x => x.TodoListId == todoListId);
        if (list is null)
        {
            return NotFound();
        }

        if (file.Length == 0)
        {
            return ValidationProblem("Bitte eine Bilddatei auswaehlen.");
        }

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationProblem("Es koennen nur Bilddateien hochgeladen werden.");
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        db.TodoAttachments.Add(new TodoAttachment
        {
            TodoListId = todoListId,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Caption = (caption ?? string.Empty).Trim(),
            FileSize = file.Length,
            Data = stream.ToArray(),
            UploadedAt = DateTime.UtcNow
        });

        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(await LoadMappedTodoListAsync(todoListId));
    }

    internal async Task<TodoListDto> LoadMappedTodoListAsync(int todoListId)
    {
        var list = await db.TodoLists
            .Include(x => x.Customer)
            .Include(x => x.Project)
            .Include(x => x.Items)
            .Include(x => x.Attachments)
            .AsSplitQuery()
            .FirstAsync(x => x.TodoListId == todoListId);

        return MapTodoList(list);
    }

    internal static TodoListDto MapTodoList(TodoList list)
    {
        var itemLookup = list.Items
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new TodoItemDto
            {
                TodoItemId = x.TodoItemId,
                TodoListId = x.TodoListId,
                ParentTodoItemId = x.ParentTodoItemId,
                Text = x.Text,
                IsCompleted = x.IsCompleted,
                SortOrder = x.SortOrder
            })
            .ToDictionary(x => x.TodoItemId);

        foreach (var item in itemLookup.Values)
        {
            if (item.ParentTodoItemId.HasValue && itemLookup.TryGetValue(item.ParentTodoItemId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
        }

        return new TodoListDto
        {
            TodoListId = list.TodoListId,
            CustomerId = list.CustomerId,
            CustomerName = list.Customer.Name,
            ProjectId = list.ProjectId,
            ProjectName = list.Project?.Name,
            Title = list.Title,
            CreatedAt = list.CreatedAt,
            UpdatedAt = list.UpdatedAt,
            OpenItemCount = list.Items.Count(x => !x.IsCompleted),
            CompletedItemCount = list.Items.Count(x => x.IsCompleted),
            Items = itemLookup.Values.Where(x => !x.ParentTodoItemId.HasValue).OrderBy(x => x.SortOrder).ToList(),
            Attachments = list.Attachments.OrderByDescending(x => x.UploadedAt).Select(x => new TodoAttachmentDto
            {
                TodoAttachmentId = x.TodoAttachmentId,
                FileName = x.FileName,
                ContentType = x.ContentType,
                Caption = x.Caption,
                FileSize = x.FileSize,
                UploadedAt = x.UploadedAt,
                DownloadUrl = $"api/todoattachments/{x.TodoAttachmentId}/content"
            }).ToList()
        };
    }
}

[ApiController]
[Route("api/todoitems")]
public class TodoItemsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpPut("{todoItemId:int}/state")]
    public async Task<ActionResult<TodoListDto>> UpdateState(int todoItemId, [FromBody] UpdateTodoItemStateRequest request)
    {
        var item = await db.TodoItems.Include(x => x.TodoList).FirstOrDefaultAsync(x => x.TodoItemId == todoItemId);
        if (item is null)
        {
            return NotFound();
        }

        item.IsCompleted = request.IsCompleted;
        await ApplyStateToChildrenAsync(todoItemId, request.IsCompleted);
        item.TodoList.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var mapper = new TodoListsController(db);
        return Ok(await mapper.LoadMappedTodoListAsync(item.TodoListId));
    }

    [HttpDelete("{todoItemId:int}")]
    public async Task<ActionResult<TodoListDto>> DeleteItem(int todoItemId)
    {
        var item = await db.TodoItems.FirstOrDefaultAsync(x => x.TodoItemId == todoItemId);
        if (item is null)
        {
            return NotFound();
        }

        var todoListId = item.TodoListId;
        var list = await db.TodoLists.FirstAsync(x => x.TodoListId == todoListId);
        var idsToRemove = await GetDescendantIdsAsync(todoItemId);
        idsToRemove.Add(todoItemId);
        db.TodoItems.RemoveRange(await db.TodoItems.Where(x => idsToRemove.Contains(x.TodoItemId)).ToListAsync());
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var mapper = new TodoListsController(db);
        return Ok(await mapper.LoadMappedTodoListAsync(todoListId));
    }

    private async Task ApplyStateToChildrenAsync(int parentId, bool isCompleted)
    {
        var pendingParentIds = new List<int> { parentId };
        while (pendingParentIds.Count > 0)
        {
            var children = await db.TodoItems
                .Where(x => x.ParentTodoItemId.HasValue && pendingParentIds.Contains(x.ParentTodoItemId.Value))
                .ToListAsync();

            if (children.Count == 0)
            {
                break;
            }

            foreach (var child in children)
            {
                child.IsCompleted = isCompleted;
            }

            pendingParentIds = children.Select(x => x.TodoItemId).ToList();
        }
    }

    private async Task<List<int>> GetDescendantIdsAsync(int parentId)
    {
        var directChildren = await db.TodoItems.Where(x => x.ParentTodoItemId == parentId).Select(x => x.TodoItemId).ToListAsync();
        var all = new List<int>();
        foreach (var childId in directChildren)
        {
            all.Add(childId);
            all.AddRange(await GetDescendantIdsAsync(childId));
        }

        return all;
    }
}

[ApiController]
[Route("api/todoattachments")]
public class TodoAttachmentsController(InvoiceWizardDbContext db) : ControllerBase
{
    [HttpGet("{todoAttachmentId:int}/content")]
    public async Task<IActionResult> GetContent(int todoAttachmentId)
    {
        var attachment = await db.TodoAttachments.FirstOrDefaultAsync(x => x.TodoAttachmentId == todoAttachmentId);
        if (attachment is null)
        {
            return NotFound();
        }

        return File(attachment.Data, attachment.ContentType, attachment.FileName);
    }

    [HttpDelete("{todoAttachmentId:int}")]
    public async Task<ActionResult<TodoListDto>> DeleteAttachment(int todoAttachmentId)
    {
        var attachment = await db.TodoAttachments.FirstOrDefaultAsync(x => x.TodoAttachmentId == todoAttachmentId);
        if (attachment is null)
        {
            return NotFound();
        }

        var list = await db.TodoLists.FirstAsync(x => x.TodoListId == attachment.TodoListId);
        db.TodoAttachments.Remove(attachment);
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var mapper = new TodoListsController(db);
        return Ok(await mapper.LoadMappedTodoListAsync(attachment.TodoListId));
    }
}
