using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "licensing",
                table: "licenses",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE licensing.licenses SET \"TenantId\" = '11111111-0000-0000-0000-000000000001' WHERE \"TenantId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "licensing",
                table: "licenses");
        }
    }
}
