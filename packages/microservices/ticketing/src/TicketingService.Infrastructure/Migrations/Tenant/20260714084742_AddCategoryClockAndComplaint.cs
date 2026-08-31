using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCategoryClockAndComplaint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BusinessHoursOnly",
                table: "TicketCategories",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplaint",
                table: "TicketCategories",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessHoursOnly",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "IsComplaint",
                table: "TicketCategories");
        }
    }
}
