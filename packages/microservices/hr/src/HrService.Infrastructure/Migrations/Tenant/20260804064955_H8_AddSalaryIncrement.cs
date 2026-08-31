using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H8_AddSalaryIncrement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalaryIncrements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    CurrentSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    CurrentEmployeeSalaryId = table.Column<string>(type: "text", nullable: true),
                    SalaryStructureId = table.Column<string>(type: "text", nullable: false),
                    SalaryStructureName = table.Column<string>(type: "text", nullable: true),
                    EffectivePeriodId = table.Column<string>(type: "text", nullable: false),
                    EffectivePeriodCode = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: true),
                    ProposedBy = table.Column<string>(type: "text", nullable: true),
                    ProposedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecidedBy = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "text", nullable: true),
                    EligibilityNotes = table.Column<string>(type: "text", nullable: true),
                    ResultingEmployeeSalaryId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryIncrements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryIncrements_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryIncrements_EffectivePeriodId",
                table: "SalaryIncrements",
                column: "EffectivePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryIncrements_OnePendingPerEmployee",
                table: "SalaryIncrements",
                column: "EmployeeId",
                unique: true,
                filter: "\"Status\" = 0 AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryIncrements_Status",
                table: "SalaryIncrements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryIncrements");
        }
    }
}
