using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    public partial class AddTenantIsolationFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkTimeEntries_CustomerId_ProjectId_WorkDate_StartTime_End~",
                table: "WorkTimeEntries");

            migrationBuilder.DropIndex(
                name: "IX_TodoLists_CustomerId_ProjectId_UpdatedAt",
                table: "TodoLists");

            migrationBuilder.DropIndex(
                name: "IX_TodoItems_TodoListId_ParentTodoItemId_SortOrder",
                table: "TodoItems");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CustomerId_Name",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_LineAllocations_InvoiceLineId_CustomerId_ProjectId_Allocate~",
                table: "LineAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ContentHash",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Name",
                table: "Customers");

            migrationBuilder.AddColumn<int>(name: "TenantId", table: "WorkTimeEntries", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "TodoLists", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "TodoItems", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "TodoAttachments", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "Projects", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "LineAllocations", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "Invoices", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "InvoiceLines", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "TenantId", table: "Customers", type: "integer", nullable: true);

            migrationBuilder.Sql(@"
INSERT INTO ""Tenants"" (""Name"", ""Slug"", ""IsActive"", ""CreatedAt"")
SELECT 'Lokaler Standardmandant', 'local-default', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM ""Tenants"");");

            migrationBuilder.Sql(@"
UPDATE ""Customers""
SET ""TenantId"" = (SELECT ""TenantId"" FROM ""Tenants"" ORDER BY ""TenantId"" LIMIT 1)
WHERE ""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""Projects"" p
SET ""TenantId"" = c.""TenantId""
FROM ""Customers"" c
WHERE p.""CustomerId"" = c.""CustomerId"" AND p.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""Invoices""
SET ""TenantId"" = (SELECT ""TenantId"" FROM ""Tenants"" ORDER BY ""TenantId"" LIMIT 1)
WHERE ""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""InvoiceLines"" l
SET ""TenantId"" = i.""TenantId""
FROM ""Invoices"" i
WHERE l.""InvoiceId"" = i.""InvoiceId"" AND l.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""LineAllocations"" a
SET ""TenantId"" = COALESCE(
    (SELECT c.""TenantId"" FROM ""Customers"" c WHERE c.""CustomerId"" = a.""CustomerId""),
    (SELECT l.""TenantId"" FROM ""InvoiceLines"" l WHERE l.""InvoiceLineId"" = a.""InvoiceLineId"")
)
WHERE a.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""WorkTimeEntries"" w
SET ""TenantId"" = COALESCE(
    (SELECT c.""TenantId"" FROM ""Customers"" c WHERE c.""CustomerId"" = w.""CustomerId""),
    (SELECT p.""TenantId"" FROM ""Projects"" p WHERE p.""ProjectId"" = w.""ProjectId"")
)
WHERE w.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""TodoLists"" t
SET ""TenantId"" = COALESCE(
    (SELECT c.""TenantId"" FROM ""Customers"" c WHERE c.""CustomerId"" = t.""CustomerId""),
    (SELECT p.""TenantId"" FROM ""Projects"" p WHERE p.""ProjectId"" = t.""ProjectId"")
)
WHERE t.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""TodoItems"" i
SET ""TenantId"" = l.""TenantId""
FROM ""TodoLists"" l
WHERE i.""TodoListId"" = l.""TodoListId"" AND i.""TenantId"" IS NULL;");

            migrationBuilder.Sql(@"
UPDATE ""TodoAttachments"" a
SET ""TenantId"" = l.""TenantId""
FROM ""TodoLists"" l
WHERE a.""TodoListId"" = l.""TodoListId"" AND a.""TenantId"" IS NULL;");

            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "WorkTimeEntries", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "TodoLists", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "TodoItems", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "TodoAttachments", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "Projects", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "LineAllocations", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "Invoices", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "InvoiceLines", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "TenantId", table: "Customers", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.CreateIndex(name: "IX_WorkTimeEntries_CustomerId", table: "WorkTimeEntries", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_WorkTimeEntries_TenantId", table: "WorkTimeEntries", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_WorkTimeEntries_TenantId_CustomerId_ProjectId_WorkDate_Star~", table: "WorkTimeEntries", columns: new[] { "TenantId", "CustomerId", "ProjectId", "WorkDate", "StartTime", "EndTime" });
            migrationBuilder.CreateIndex(name: "IX_TodoLists_CustomerId", table: "TodoLists", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_TodoLists_TenantId", table: "TodoLists", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_TodoLists_TenantId_CustomerId_ProjectId_UpdatedAt", table: "TodoLists", columns: new[] { "TenantId", "CustomerId", "ProjectId", "UpdatedAt" });
            migrationBuilder.CreateIndex(name: "IX_TodoItems_TenantId", table: "TodoItems", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_TodoItems_TenantId_TodoListId_ParentTodoItemId_SortOrder", table: "TodoItems", columns: new[] { "TenantId", "TodoListId", "ParentTodoItemId", "SortOrder" });
            migrationBuilder.CreateIndex(name: "IX_TodoItems_TodoListId", table: "TodoItems", column: "TodoListId");
            migrationBuilder.CreateIndex(name: "IX_TodoAttachments_TenantId", table: "TodoAttachments", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Projects_CustomerId", table: "Projects", column: "CustomerId");
            migrationBuilder.CreateIndex(name: "IX_Projects_TenantId", table: "Projects", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Projects_TenantId_CustomerId_Name", table: "Projects", columns: new[] { "TenantId", "CustomerId", "Name" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_LineAllocations_InvoiceLineId", table: "LineAllocations", column: "InvoiceLineId");
            migrationBuilder.CreateIndex(name: "IX_LineAllocations_TenantId", table: "LineAllocations", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_LineAllocations_TenantId_InvoiceLineId_CustomerId_ProjectId~", table: "LineAllocations", columns: new[] { "TenantId", "InvoiceLineId", "CustomerId", "ProjectId", "AllocatedQuantity" });
            migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId", table: "Invoices", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_ContentHash", table: "Invoices", columns: new[] { "TenantId", "ContentHash" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_InvoiceLines_TenantId", table: "InvoiceLines", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Customers_TenantId", table: "Customers", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_Customers_TenantId_Name", table: "Customers", columns: new[] { "TenantId", "Name" }, unique: true);

            migrationBuilder.AddForeignKey(name: "FK_Customers_Tenants_TenantId", table: "Customers", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_InvoiceLines_Tenants_TenantId", table: "InvoiceLines", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Invoices_Tenants_TenantId", table: "Invoices", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_LineAllocations_Tenants_TenantId", table: "LineAllocations", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Projects_Tenants_TenantId", table: "Projects", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_TodoAttachments_Tenants_TenantId", table: "TodoAttachments", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_TodoItems_Tenants_TenantId", table: "TodoItems", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_TodoLists_Tenants_TenantId", table: "TodoLists", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_WorkTimeEntries_Tenants_TenantId", table: "WorkTimeEntries", column: "TenantId", principalTable: "Tenants", principalColumn: "TenantId", onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Customers_Tenants_TenantId", table: "Customers");
            migrationBuilder.DropForeignKey(name: "FK_InvoiceLines_Tenants_TenantId", table: "InvoiceLines");
            migrationBuilder.DropForeignKey(name: "FK_Invoices_Tenants_TenantId", table: "Invoices");
            migrationBuilder.DropForeignKey(name: "FK_LineAllocations_Tenants_TenantId", table: "LineAllocations");
            migrationBuilder.DropForeignKey(name: "FK_Projects_Tenants_TenantId", table: "Projects");
            migrationBuilder.DropForeignKey(name: "FK_TodoAttachments_Tenants_TenantId", table: "TodoAttachments");
            migrationBuilder.DropForeignKey(name: "FK_TodoItems_Tenants_TenantId", table: "TodoItems");
            migrationBuilder.DropForeignKey(name: "FK_TodoLists_Tenants_TenantId", table: "TodoLists");
            migrationBuilder.DropForeignKey(name: "FK_WorkTimeEntries_Tenants_TenantId", table: "WorkTimeEntries");

            migrationBuilder.DropIndex(name: "IX_WorkTimeEntries_CustomerId", table: "WorkTimeEntries");
            migrationBuilder.DropIndex(name: "IX_WorkTimeEntries_TenantId", table: "WorkTimeEntries");
            migrationBuilder.DropIndex(name: "IX_WorkTimeEntries_TenantId_CustomerId_ProjectId_WorkDate_Star~", table: "WorkTimeEntries");
            migrationBuilder.DropIndex(name: "IX_TodoLists_CustomerId", table: "TodoLists");
            migrationBuilder.DropIndex(name: "IX_TodoLists_TenantId", table: "TodoLists");
            migrationBuilder.DropIndex(name: "IX_TodoLists_TenantId_CustomerId_ProjectId_UpdatedAt", table: "TodoLists");
            migrationBuilder.DropIndex(name: "IX_TodoItems_TenantId", table: "TodoItems");
            migrationBuilder.DropIndex(name: "IX_TodoItems_TenantId_TodoListId_ParentTodoItemId_SortOrder", table: "TodoItems");
            migrationBuilder.DropIndex(name: "IX_TodoItems_TodoListId", table: "TodoItems");
            migrationBuilder.DropIndex(name: "IX_TodoAttachments_TenantId", table: "TodoAttachments");
            migrationBuilder.DropIndex(name: "IX_Projects_CustomerId", table: "Projects");
            migrationBuilder.DropIndex(name: "IX_Projects_TenantId", table: "Projects");
            migrationBuilder.DropIndex(name: "IX_Projects_TenantId_CustomerId_Name", table: "Projects");
            migrationBuilder.DropIndex(name: "IX_LineAllocations_InvoiceLineId", table: "LineAllocations");
            migrationBuilder.DropIndex(name: "IX_LineAllocations_TenantId", table: "LineAllocations");
            migrationBuilder.DropIndex(name: "IX_LineAllocations_TenantId_InvoiceLineId_CustomerId_ProjectId~", table: "LineAllocations");
            migrationBuilder.DropIndex(name: "IX_Invoices_TenantId", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_Invoices_TenantId_ContentHash", table: "Invoices");
            migrationBuilder.DropIndex(name: "IX_InvoiceLines_TenantId", table: "InvoiceLines");
            migrationBuilder.DropIndex(name: "IX_Customers_TenantId", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Customers_TenantId_Name", table: "Customers");

            migrationBuilder.DropColumn(name: "TenantId", table: "WorkTimeEntries");
            migrationBuilder.DropColumn(name: "TenantId", table: "TodoLists");
            migrationBuilder.DropColumn(name: "TenantId", table: "TodoItems");
            migrationBuilder.DropColumn(name: "TenantId", table: "TodoAttachments");
            migrationBuilder.DropColumn(name: "TenantId", table: "Projects");
            migrationBuilder.DropColumn(name: "TenantId", table: "LineAllocations");
            migrationBuilder.DropColumn(name: "TenantId", table: "Invoices");
            migrationBuilder.DropColumn(name: "TenantId", table: "InvoiceLines");
            migrationBuilder.DropColumn(name: "TenantId", table: "Customers");

            migrationBuilder.CreateIndex(name: "IX_WorkTimeEntries_CustomerId_ProjectId_WorkDate_StartTime_End~", table: "WorkTimeEntries", columns: new[] { "CustomerId", "ProjectId", "WorkDate", "StartTime", "EndTime" });
            migrationBuilder.CreateIndex(name: "IX_TodoLists_CustomerId_ProjectId_UpdatedAt", table: "TodoLists", columns: new[] { "CustomerId", "ProjectId", "UpdatedAt" });
            migrationBuilder.CreateIndex(name: "IX_TodoItems_TodoListId_ParentTodoItemId_SortOrder", table: "TodoItems", columns: new[] { "TodoListId", "ParentTodoItemId", "SortOrder" });
            migrationBuilder.CreateIndex(name: "IX_Projects_CustomerId_Name", table: "Projects", columns: new[] { "CustomerId", "Name" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_LineAllocations_InvoiceLineId_CustomerId_ProjectId_Allocate~", table: "LineAllocations", columns: new[] { "InvoiceLineId", "CustomerId", "ProjectId", "AllocatedQuantity" });
            migrationBuilder.CreateIndex(name: "IX_Invoices_ContentHash", table: "Invoices", column: "ContentHash", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Customers_Name", table: "Customers", column: "Name", unique: true);
        }
    }
}

