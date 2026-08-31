using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE licensing.licenses ENABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql("""
                CREATE POLICY tenant_isolation ON licensing.licenses
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation ON licensing.licenses;");
            migrationBuilder.Sql("ALTER TABLE licensing.licenses DISABLE ROW LEVEL SECURITY;");
        }
    }
}
