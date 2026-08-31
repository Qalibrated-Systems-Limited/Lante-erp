using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTruckServiceMileageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastServiceDate",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastServiceOdometer",
                table: "Vehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceIntervalKm",
                table: "Vehicles",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastServiceDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LastServiceOdometer",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ServiceIntervalKm",
                table: "Vehicles");
        }
    }
}
