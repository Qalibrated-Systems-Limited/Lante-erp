using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StatutoryRemittances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RefNo = table.Column<string>(type: "text", nullable: false),
                    ObligationCode = table.Column<string>(type: "text", nullable: false),
                    ObligationName = table.Column<string>(type: "text", nullable: false),
                    Period = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BankAccountCode = table.Column<string>(type: "text", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RemittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    JournalEntryId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryRemittances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryRemittances_ObligationCode_Period",
                table: "StatutoryRemittances",
                columns: new[] { "ObligationCode", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryRemittances_RefNo",
                table: "StatutoryRemittances",
                column: "RefNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StatutoryRemittances");
        }
    }
}
