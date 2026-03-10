namespace InvoiceWizard.Data.Entities;

public class TodoListEntity
{
    public int TodoListId { get; set; }
    public int CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public int? ProjectId { get; set; }
    public ProjectEntity? Project { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int OpenItemCount { get; set; }
    public int CompletedItemCount { get; set; }
    public List<TodoItemEntity> Items { get; set; } = new();
    public List<TodoAttachmentEntity> Attachments { get; set; } = new();
    public string ScopeLabel => Project?.Name ?? "Nur Kunde";
}
