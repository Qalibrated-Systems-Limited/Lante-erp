using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Infrastructure.Migrations
{
    /// <summary>
    /// Phase 3 Increment 2: the model dropped its fixed "public" default schema (tenant-plane tables
    /// are now unqualified → routed by search_path; control-plane tables are pinned to public).
    /// EF wanted to emit RenameTable ops moving the tenant-plane tables from public to the default
    /// schema — that is a no-op on disk (they stay physically in public and are reached via
    /// search_path), so it is intentionally omitted. This migration only advances the model snapshot.
    /// </summary>
    public partial class UserServiceSchemaSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) { }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
