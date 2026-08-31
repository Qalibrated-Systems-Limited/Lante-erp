using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GrantLicensingSchemaUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "licenses",
                schema: "licensing",
                newName: "licenses");

            // EnsureSchema (InitialCreate) created "licensing" owned by the migration role (postgres)
            // with no grants to anything else. The app connects as the low-privilege qalicore_app role
            // (see postgres init script), which was never given USAGE on this schema — Postgres hides
            // schema contents from unqualified name resolution without it, so every query failed with
            // "relation licenses does not exist" even though the table existed and search_path was correct.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'qalicore_app') THEN
                        GRANT USAGE ON SCHEMA licensing TO qalicore_app;
                        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA licensing TO qalicore_app;
                        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA licensing TO qalicore_app;
                        ALTER DEFAULT PRIVILEGES IN SCHEMA licensing GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO qalicore_app;
                        ALTER DEFAULT PRIVILEGES IN SCHEMA licensing GRANT USAGE, SELECT ON SEQUENCES TO qalicore_app;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'qalicore_app') THEN
                        REVOKE ALL ON ALL TABLES IN SCHEMA licensing FROM qalicore_app;
                        REVOKE ALL ON ALL SEQUENCES IN SCHEMA licensing FROM qalicore_app;
                        REVOKE USAGE ON SCHEMA licensing FROM qalicore_app;
                    END IF;
                END $$;
            ");

            migrationBuilder.EnsureSchema(
                name: "licensing");

            migrationBuilder.RenameTable(
                name: "licenses",
                newName: "licenses",
                newSchema: "licensing");
        }
    }
}
