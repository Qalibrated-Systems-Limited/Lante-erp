using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRlsPolicies : Migration
    {
        private static readonly string[] Tables =
        [
            "VehicleDispatches", "Requisitions", "Refunds", "RiskEntries", "ServiceReports",
            "Projects", "ProjectTasks", "ProjectResources", "ProjectHistories", "ProjectApprovals",
            "PreDeploymentChecklists", "Photos", "PerformanceMetrics", "PerDiemReturnForms",
            "PettyCashForms", "NonConformanceReports", "Milestones", "LabWorkOrders", "LabDataSheets",
            "FuelLogs", "FieldVehicles", "DailySummaries", "CustomerFeedbacks",
            "CostEntries", "Claims", "CheckIns", "BudgetLines", "Attachments",
            "Assignments", "AssignedTechnicians", "AdvanceReturnLineItems", "AdvanceReturnForms"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY;");

                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON public."{table}"
                      AS PERMISSIVE FOR ALL TO qalicore_app
                      USING (
                        "TenantId" IS NULL
                        OR "TenantId" = current_setting('app.current_tenant', true)
                      )
                      WITH CHECK (
                        "TenantId" IS NULL
                        OR "TenantId" = current_setting('app.current_tenant', true)
                      );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON public.\"{table}\";");
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
