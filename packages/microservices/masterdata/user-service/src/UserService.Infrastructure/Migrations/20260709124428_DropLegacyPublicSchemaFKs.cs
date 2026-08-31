using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyPublicSchemaFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These three FKs check a tenant-schema-scoped id (Department/Role/Branch, chosen from the
            // caller's own tenant_<slug> schema) against this service's public-schema catalog tables —
            // a leftover from before schema-per-tenant existed. Public's catalog no longer matches any
            // tenant's actual data (different ids, and tenants freely edit their own roles/departments/
            // branches), so the checks only ever produce false-positive FK violations, most visibly on
            // user creation (UserDirectory.CreateInvitedUserAsync writes here on a public-pinned
            // connection). Each tenant schema keeps its own equivalent FK (via TenantDbContext's
            // migrations), so per-tenant referential integrity is unaffected.
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                schema: "public",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "public",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Branches_BranchId",
                schema: "public",
                table: "UserTenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                schema: "public",
                table: "Users",
                column: "DepartmentId",
                principalSchema: "public",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                schema: "public",
                table: "UserRoles",
                column: "RoleId",
                principalSchema: "public",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Branches_BranchId",
                schema: "public",
                table: "UserTenants",
                column: "BranchId",
                principalSchema: "public",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
