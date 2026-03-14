using InvoiceWizard.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Data;

public class InvoiceWizardDbContext(DbContextOptions<InvoiceWizardDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserTenantMembership> UserTenantMemberships => Set<UserTenantMembership>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantLicense> TenantLicenses => Set<TenantLicense>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<LineAllocation> LineAllocations => Set<LineAllocation>();
    public DbSet<WorkTimeEntry> WorkTimeEntries => Set<WorkTimeEntry>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<TodoAttachment> TodoAttachments => Set<TodoAttachment>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasKey(x => x.TenantId);
        modelBuilder.Entity<AppUser>().HasKey(x => x.AppUserId);
        modelBuilder.Entity<UserTenantMembership>().HasKey(x => x.UserTenantMembershipId);
        modelBuilder.Entity<SubscriptionPlan>().HasKey(x => x.SubscriptionPlanId);
        modelBuilder.Entity<TenantLicense>().HasKey(x => x.TenantLicenseId);
        modelBuilder.Entity<Customer>().HasKey(x => x.CustomerId);
        modelBuilder.Entity<Project>().HasKey(x => x.ProjectId);
        modelBuilder.Entity<Invoice>().HasKey(x => x.InvoiceId);
        modelBuilder.Entity<InvoiceLine>().HasKey(x => x.InvoiceLineId);
        modelBuilder.Entity<LineAllocation>().HasKey(x => x.LineAllocationId);
        modelBuilder.Entity<WorkTimeEntry>().HasKey(x => x.WorkTimeEntryId);
        modelBuilder.Entity<TodoList>().HasKey(x => x.TodoListId);
        modelBuilder.Entity<TodoItem>().HasKey(x => x.TodoItemId);
        modelBuilder.Entity<TodoAttachment>().HasKey(x => x.TodoAttachmentId);
        modelBuilder.Entity<CalendarEntry>().HasKey(x => x.CalendarEntryId);

        modelBuilder.Entity<Tenant>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Tenant>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<UserTenantMembership>().HasIndex(x => new { x.AppUserId, x.TenantId }).IsUnique();
        modelBuilder.Entity<SubscriptionPlan>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        modelBuilder.Entity<Project>().HasIndex(x => new { x.TenantId, x.CustomerId, x.Name }).IsUnique();
        modelBuilder.Entity<Invoice>().HasIndex(x => new { x.TenantId, x.ContentHash }).IsUnique();

        modelBuilder.Entity<UserTenantMembership>().HasOne(x => x.AppUser).WithMany(x => x.Memberships).HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserTenantMembership>().HasOne(x => x.Tenant).WithMany(x => x.Memberships).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TenantLicense>().HasOne(x => x.Tenant).WithMany(x => x.Licenses).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TenantLicense>().HasOne(x => x.SubscriptionPlan).WithMany(x => x.Licenses).HasForeignKey(x => x.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Customer>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Project>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Invoice>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InvoiceLine>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LineAllocation>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkTimeEntry>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoList>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoItem>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TodoAttachment>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<CalendarEntry>().HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);

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
        modelBuilder.Entity<CalendarEntry>().HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tenant>().Property(x => x.Name).HasMaxLength(200);
        modelBuilder.Entity<Tenant>().Property(x => x.Slug).HasMaxLength(200);
        modelBuilder.Entity<AppUser>().Property(x => x.Email).HasMaxLength(320);
        modelBuilder.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(200);
        modelBuilder.Entity<UserTenantMembership>().Property(x => x.Role).HasMaxLength(50);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.Code).HasMaxLength(100);
        modelBuilder.Entity<SubscriptionPlan>().Property(x => x.Name).HasMaxLength(200);
        modelBuilder.Entity<CalendarEntry>().Property(x => x.Title).HasMaxLength(200);
        modelBuilder.Entity<CalendarEntry>().Property(x => x.Location).HasMaxLength(200);

        modelBuilder.Entity<Invoice>().Property(x => x.InvoiceDate).HasColumnType("date");
        modelBuilder.Entity<WorkTimeEntry>().Property(x => x.WorkDate).HasColumnType("date");
        modelBuilder.Entity<CalendarEntry>().Property(x => x.EntryDate).HasColumnType("date");

        modelBuilder.Entity<Customer>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<Project>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<Invoice>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<InvoiceLine>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<LineAllocation>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<WorkTimeEntry>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<TodoList>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<TodoItem>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<TodoAttachment>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<CalendarEntry>().HasIndex(x => x.TenantId);

        modelBuilder.Entity<LineAllocation>().HasIndex(x => new { x.TenantId, x.InvoiceLineId, x.CustomerId, x.ProjectId, x.AllocatedQuantity });
        modelBuilder.Entity<WorkTimeEntry>().HasIndex(x => new { x.TenantId, x.CustomerId, x.ProjectId, x.WorkDate, x.StartTime, x.EndTime });
        modelBuilder.Entity<TodoList>().HasIndex(x => new { x.TenantId, x.CustomerId, x.ProjectId, x.UpdatedAt });
        modelBuilder.Entity<TodoItem>().HasIndex(x => new { x.TenantId, x.TodoListId, x.ParentTodoItemId, x.SortOrder });
        modelBuilder.Entity<TenantLicense>().HasIndex(x => new { x.TenantId, x.IsActive });
        modelBuilder.Entity<CalendarEntry>().HasIndex(x => new { x.TenantId, x.AppUserId, x.EntryDate, x.StartTime });
    }
}
