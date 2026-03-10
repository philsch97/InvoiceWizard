namespace InvoiceWizard.Data.Entities;

public class TodoItemEntity
{
    public int TodoItemId { get; set; }
    public int TodoListId { get; set; }
    public int? ParentTodoItemId { get; set; }
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public List<TodoItemEntity> Children { get; set; } = new();
}
