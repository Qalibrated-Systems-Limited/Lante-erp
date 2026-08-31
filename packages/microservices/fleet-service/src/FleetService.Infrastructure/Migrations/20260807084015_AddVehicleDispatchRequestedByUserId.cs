using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleDispatchRequestedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestedByUserId",
                table: "VehicleDispatches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedByUserId",
                table: "VehicleDispatches");
        }
    }
}
