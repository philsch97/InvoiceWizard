using System;
using InvoiceWizard.Backend.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceWizard.Backend.Migrations
{
    [DbContext(typeof(InvoiceWizardDbContext))]
    [Migration("20260310190000_AddTodoListsAndAttachments")]
    public partial class AddTodoListsAndAttachments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TodoLists",
                columns: table => new
                {
                    TodoListId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoLists", x => x.TodoListId);
                    table.ForeignKey(
                        name: "FK_TodoLists_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TodoLists_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TodoAttachments",
                columns: table => new
                {
                    TodoAttachmentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TodoListId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoAttachments", x => x.TodoAttachmentId);
                    table.ForeignKey(
                        name: "FK_TodoAttachments_TodoLists_TodoListId",
                        column: x => x.TodoListId,
                        principalTable: "TodoLists",
                        principalColumn: "TodoListId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TodoItems",
                columns: table => new
                {
                    TodoItemId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TodoListId = table.Column<int>(type: "integer", nullable: false),
                    ParentTodoItemId = table.Column<int>(type: "integer", nullable: true),
                    Text = table.Column<string>(type: "text", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TodoItems", x => x.TodoItemId);
                    table.ForeignKey(
                        name: "FK_TodoItems_TodoItems_ParentTodoItemId",
                        column: x => x.ParentTodoItemId,
                        principalTable: "TodoItems",
                        principalColumn: "TodoItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TodoItems_TodoLists_TodoListId",
                        column: x => x.TodoListId,
                        principalTable: "TodoLists",
                        principalColumn: "TodoListId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TodoAttachments_TodoListId",
                table: "TodoAttachments",
                column: "TodoListId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_ParentTodoItemId",
                table: "TodoItems",
                column: "ParentTodoItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TodoItems_TodoListId_ParentTodoItemId_SortOrder",
                table: "TodoItems",
                columns: new[] { "TodoListId", "ParentTodoItemId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TodoLists_CustomerId_ProjectId_UpdatedAt",
                table: "TodoLists",
                columns: new[] { "CustomerId", "ProjectId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TodoLists_ProjectId",
                table: "TodoLists",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TodoAttachments");
            migrationBuilder.DropTable(name: "TodoItems");
            migrationBuilder.DropTable(name: "TodoLists");
        }
    }
}
