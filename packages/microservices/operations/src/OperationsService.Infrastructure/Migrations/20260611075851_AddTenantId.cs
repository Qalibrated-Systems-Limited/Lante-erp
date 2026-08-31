using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "VehicleDispatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "RiskEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Requisitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Refunds",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ProjectTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ProjectResources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ProjectHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "ProjectApprovals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "PreDeploymentChecklists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Photos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "PettyCashForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "PerformanceMetrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "NonConformanceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Milestones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "LabDataSheets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "FuelLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "FieldVehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "DailySummaries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "CustomerFeedbacks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "CostEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Claims",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "CheckIns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "BudgetLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AssignedTechnicians",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AdvanceReturnLineItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: true);

            // Backfill existing rows with the QSL tenant ID
            const string qslId = "11111111-0000-0000-0000-000000000001";
            foreach (var t in new[] {
                "VehicleDispatches","Requisitions","Refunds","Projects","ProjectTasks",
                "ProjectResources","ProjectHistories","ProjectApprovals","Photos",
                "PerformanceMetrics","PerDiemReturnForms","PettyCashForms",
                "NonConformanceReports","Milestones","LabWorkOrders","LabDataSheets",
                "FuelLogs","FieldVehicles","DailySummaries","CustomerFeedbacks",
                "CostEntries","Claims","CheckIns","BudgetLines","Attachments",
                "Assignments","AssignedTechnicians","AdvanceReturnLineItems","AdvanceReturnForms"
            })
                migrationBuilder.Sql($"UPDATE \"{t}\" SET \"TenantId\" = '{qslId}' WHERE \"TenantId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "VehicleDispatches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Requisitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProjectResources");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProjectHistories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProjectApprovals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PreDeploymentChecklists");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PerformanceMetrics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NonConformanceReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LabDataSheets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FuelLogs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FieldVehicles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DailySummaries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CustomerFeedbacks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CostEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AssignedTechnicians");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AdvanceReturnLineItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AdvanceReturnForms");
        }
    }
}
