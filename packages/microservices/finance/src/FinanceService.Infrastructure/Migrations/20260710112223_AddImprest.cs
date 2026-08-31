using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImprest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImprestRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RefNo = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    DisbursedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnretiredBalance = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ImprestAccountCode = table.Column<string>(type: "text", nullable: false),
                    BankAccountCode = table.Column<string>(type: "text", nullable: false),
                    DisburseJournalEntryId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprestRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalAdvances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ImprestRequestId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeductionMonth = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAdvances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImprestRetirementLines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ImprestRequestId = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceiptUrl = table.Column<string>(type: "text", nullable: true),
                    ExpenseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpenseAccountCode = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_ImprestRetirementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprestRetirementLines_ImprestRequests_ImprestRequestId",
                        column: x => x.ImprestRequestId,
                        principalTable: "ImprestRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprestRequests_RefNo",
                table: "ImprestRequests",
                column: "RefNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImprestRequests_Status",
                table: "ImprestRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImprestRetirementLines_ImprestRequestId",
                table: "ImprestRetirementLines",
                column: "ImprestRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAdvances_ImprestRequestId",
                table: "PersonalAdvances",
                column: "ImprestRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImprestRetirementLines");

            migrationBuilder.DropTable(
                name: "PersonalAdvances");

            migrationBuilder.DropTable(
                name: "ImprestRequests");
        }
    }
}
