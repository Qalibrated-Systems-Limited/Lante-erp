using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddVehicleDispatchApprovalAndInspectionExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "VehicleDispatches",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                table: "VehicleDispatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionExpiryDate",
                table: "FieldVehicles",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "VehicleDispatches");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "VehicleDispatches");

            migrationBuilder.DropColumn(
                name: "InspectionExpiryDate",
                table: "FieldVehicles");
        }
    }
}
