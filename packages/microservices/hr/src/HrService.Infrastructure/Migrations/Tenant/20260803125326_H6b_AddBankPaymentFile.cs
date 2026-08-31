using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H6b_AddBankPaymentFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankPaymentFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PayrollRunId = table.Column<string>(type: "text", nullable: false),
                    PayrollPeriodCode = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EmployeeCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MissingBankDetails = table.Column<string>(type: "text", nullable: true),
                    MissingCount = table.Column<int>(type: "integer", nullable: false),
                    NeedsFormatConfirmation = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedBy = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DownloadedBy = table.Column<string>(type: "text", nullable: true),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedBy = table.Column<string>(type: "text", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BankGlAccountId = table.Column<string>(type: "text", nullable: true),
                    BankGlAccountCode = table.Column<string>(type: "text", nullable: true),
                    JournalEntryId = table.Column<string>(type: "text", nullable: true),
                    JournalEntryNo = table.Column<string>(type: "text", nullable: true),
                    JournalError = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankPaymentFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankPaymentFiles_PayrollRuns_PayrollRunId",
                        column: x => x.PayrollRunId,
                        principalTable: "PayrollRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankPaymentFiles_Format",
                table: "BankPaymentFiles",
                column: "Format");

            migrationBuilder.CreateIndex(
                name: "IX_BankPaymentFiles_PayrollRunId",
                table: "BankPaymentFiles",
                column: "PayrollRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankPaymentFiles");
        }
    }
}
