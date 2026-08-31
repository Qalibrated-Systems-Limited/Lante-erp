using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "WorkflowRules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Trucks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "TripTypes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Trips",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MaterialVariants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MaterialVariantPhotos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Materials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MaterialPhotos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "MaterialCosts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "LicenseClasses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Feedbacks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "Expenses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "DriverProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "DriverProfileChanges",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "DriverActivities",
                type: "text",
                nullable: true);

            // Backfill existing rows with the QSL tenant ID
            const string qslId = "11111111-0000-0000-0000-000000000001";
            foreach (var t in new[] {
                "Trips","Trucks","TripTypes","WorkflowRules","Materials","MaterialVariants",
                "MaterialVariantPhotos","MaterialPhotos","MaterialCosts","LicenseClasses",
                "Feedbacks","Expenses","DriverProfiles","DriverProfileChanges","DriverActivities"
            })
                migrationBuilder.Sql($"UPDATE \"{t}\" SET \"TenantId\" = '{qslId}' WHERE \"TenantId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkflowRules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Trucks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TripTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaterialVariants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaterialVariantPhotos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaterialPhotos");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MaterialCosts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LicenseClasses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DriverProfileChanges");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DriverActivities");
        }
    }
}
