using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <summary>
    /// Phase 3E: drop the row-level-security tenant isolation now that tenants are served from
    /// their own schemas (search_path), not the shared public schema. Removes the dependency on
    /// app.current_tenant. Tables physically remain in public (the model default schema is simply
    /// no longer pinned to "public"); the RenameTable metadata churn EF wanted is intentionally
    /// omitted since it is a no-op on disk.
    /// </summary>
    public partial class DropRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                DECLARE t text;
                BEGIN
                  FOR t IN SELECT tablename FROM pg_policies
                           WHERE schemaname = 'public' AND policyname = 'tenant_isolation'
                  LOOP
                    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON public.%I', t);
                    EXECUTE format('ALTER TABLE public.%I DISABLE ROW LEVEL SECURITY', t);
                  END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way cutover. To restore RLS, re-run the EnableRlsPolicies migration logic.
            migrationBuilder.Sql("SELECT 1;");
        }
    }
}
