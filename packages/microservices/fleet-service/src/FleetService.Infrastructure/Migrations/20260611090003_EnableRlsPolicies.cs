using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRlsPolicies : Migration
    {
        // Tables that inherit BaseEntity and have TenantId
        private static readonly string[] TenantTables =
        [
            "WorkflowRules", "Trucks", "TripTypes", "Trips", "MaterialVariants",
            "MaterialVariantPhotos", "Materials", "MaterialPhotos", "MaterialCosts",
            "LicenseClasses", "Feedbacks", "Expenses", "DriverProfiles",
            "DriverProfileChanges", "DriverActivities"
        ];

        // Child/join tables without TenantId — isolation flows from parent table RLS
        private static readonly string[] ChildTables =
        [
            "TripMaterials", "VehicleMileages", "Receipts", "DriverProfileLicenseClasses", "Attachments"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables)
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

            foreach (var table in ChildTables)
            {
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"CREATE POLICY tenant_isolation ON public.\"{table}\" AS PERMISSIVE FOR ALL TO qalicore_app USING (true) WITH CHECK (true);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantTables.Concat(ChildTables))
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON public.\"{table}\";");
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
