using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseService.Infrastructure.Migrations
{
    /// <summary>Phase 3E: drop RLS tenant isolation — tenants are now served from their own schemas
    /// (search_path), removing the dependency on app.current_tenant.</summary>
    public partial class DropRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE r record;
                BEGIN
                  FOR r IN SELECT schemaname, tablename FROM pg_policies WHERE policyname = 'tenant_isolation'
                  LOOP
                    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.schemaname, r.tablename);
                    EXECUTE format('ALTER TABLE %I.%I DISABLE ROW LEVEL SECURITY', r.schemaname, r.tablename);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT 1;"); // one-way cutover
        }
    }
}
