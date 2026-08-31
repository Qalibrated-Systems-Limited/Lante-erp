using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddBudgetBurnTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Alert100SentAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Alert80SentAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Alert90SentAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Alert95SentAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BudgetLocked",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Committed",
                table: "Projects",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FmApprovalRef",
                table: "CostEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBackdated",
                table: "CostEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProjectAlertLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    ThresholdPct = table.Column<int>(type: "integer", nullable: false),
                    SpentAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CommittedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BurnPct = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAlertLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAlertLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAlertLogs_ProjectId",
                table: "ProjectAlertLogs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAlertLogs");

            migrationBuilder.DropColumn(
                name: "Alert100SentAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Alert80SentAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Alert90SentAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Alert95SentAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BudgetLocked",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Committed",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "FmApprovalRef",
                table: "CostEntries");

            migrationBuilder.DropColumn(
                name: "IsBackdated",
                table: "CostEntries");
        }
    }
}
