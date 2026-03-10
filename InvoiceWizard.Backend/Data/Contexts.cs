using InvoiceWizard.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Data;

public class InvoiceWizardDbContext(DbContextOptions<InvoiceWizardDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<LineAllocation> LineAllocations => Set<LineAllocation>();
    public DbSet<WorkTimeEntry> WorkTimeEntries => Set<WorkTimeEntry>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<TodoAttachment> TodoAttachments => Set<TodoAttachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(x => x.CustomerId);
        modelBuilder.Entity<Project>().HasKey(x => x.ProjectId);
        modelBuilder.Entity<Invoice>().HasKey(x => x.InvoiceId);
        modelBuilder.Entity<InvoiceLine>().HasKey(x => x.InvoiceLineId);
        modelBuilder.Entity<LineAllocation>().HasKey(x => x.LineAllocationId);
        modelBuilder.Entity<WorkTimeEntry>().HasKey(x => x.WorkTimeEntryId);
        modelBuilder.Entity<TodoList>().HasKey(x => x.TodoListId);
        modelBuilder.Entity<TodoItem>().HasKey(x => x.TodoItemId);
        modelBuilder.Entity<TodoAttachment>().HasKey(x => x.TodoAttachmentId);

        modelBuilder.Entity<Customer>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Project>().HasIndex(x => new { x.CustomerId, x.Name }).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(x => x.ContentHash).IsUnique();

        modelBuilder.Entity<Project>().HasOne(x => x.Customer).WithMany(x => x.Projects).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Invoice).WithMany(x => x.Lines).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LineAllocation>().HasOne(x => x.InvoiceLine).WithMany(x => x.Allocations).HasForeignKey(x => x.InvoiceLineId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<LineAllocation>().HasOne(x => x.Customer).WithMany(x => x.Allocations).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LineAllocation>().HasOne(x => x.Project).WithMany(x => x.Allocations).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkTimeEntry>().HasOne(x => x.Customer).WithMany(x => x.WorkTimeEntries).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkTimeEntry>().HasOne(x => x.Project).WithMany(x => x.WorkTimeEntries).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoList>().HasOne(x => x.Customer).WithMany(x => x.TodoLists).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoList>().HasOne(x => x.Project).WithMany(x => x.TodoLists).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoItem>().HasOne(x => x.TodoList).WithMany(x => x.Items).HasForeignKey(x => x.TodoListId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TodoItem>().HasOne(x => x.ParentTodoItem).WithMany(x => x.Children).HasForeignKey(x => x.ParentTodoItemId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoAttachment>().HasOne(x => x.TodoList).WithMany(x => x.Attachments).HasForeignKey(x => x.TodoListId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>().Property(x => x.InvoiceDate).HasColumnType("date");
        modelBuilder.Entity<WorkTimeEntry>().Property(x => x.WorkDate).HasColumnType("date");

        modelBuilder.Entity<LineAllocation>().HasIndex(x => new { x.InvoiceLineId, x.CustomerId, x.ProjectId, x.AllocatedQuantity });
        modelBuilder.Entity<WorkTimeEntry>().HasIndex(x => new { x.CustomerId, x.ProjectId, x.WorkDate, x.StartTime, x.EndTime });
        modelBuilder.Entity<TodoList>().HasIndex(x => new { x.CustomerId, x.ProjectId, x.UpdatedAt });
        modelBuilder.Entity<TodoItem>().HasIndex(x => new { x.TodoListId, x.ParentTodoItemId, x.SortOrder });
    }
}
