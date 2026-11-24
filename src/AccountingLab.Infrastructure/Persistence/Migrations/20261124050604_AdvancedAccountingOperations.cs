using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvancedAccountingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CompanyId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OperationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TargetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Kind = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AppliedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RemainingAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingOperations", x => x.Id);
                    table.UniqueConstraint("AK_AccountingOperations_CompanyId_Id", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_AccountingOperations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountingOperations_JournalEntries_CompanyId_JournalEntryId",
                        columns: x => new { x.CompanyId, x.JournalEntryId },
                        principalTable: "JournalEntries",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingOperations_CompanyId_JournalEntryId",
                table: "AccountingOperations",
                columns: new[] { "CompanyId", "JournalEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingOperations_CompanyId_OperationId",
                table: "AccountingOperations",
                columns: new[] { "CompanyId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountingOperations_CompanyId_TargetId",
                table: "AccountingOperations",
                columns: new[] { "CompanyId", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingOperations");
        }
    }
}
