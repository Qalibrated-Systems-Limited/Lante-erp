using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFieldVehiclesAndTruckDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Trucks_TruckId",
                table: "Trips");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Trucks",
                table: "Trucks");

            migrationBuilder.RenameTable(
                name: "Trucks",
                newName: "Vehicles");

            migrationBuilder.RenameColumn(
                name: "TruckLocationLongitude",
                table: "Trips",
                newName: "VehicleLocationLongitude");

            migrationBuilder.RenameColumn(
                name: "TruckLocationLatitude",
                table: "Trips",
                newName: "VehicleLocationLatitude");

            migrationBuilder.RenameColumn(
                name: "TruckId",
                table: "Trips",
                newName: "VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_Trips_TruckId",
                table: "Trips",
                newName: "IX_Trips_VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_Trucks_LicensePlate",
                table: "Vehicles",
                newName: "IX_Vehicles_LicensePlate");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "Trips",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AssetId",
                table: "Vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Capacity",
                table: "Vehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FuelBenchmarkKmPerLitre",
                table: "Vehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionExpiryDate",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InsuranceExpiryDate",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Make",
                table: "Vehicles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextServiceDate",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Odometer",
                table: "Vehicles",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vehicles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VehicleClass",
                table: "Vehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Vehicles_VehicleId",
                table: "Trips",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Vehicles_VehicleId",
                table: "Trips");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "AssetId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelBenchmarkKmPerLitre",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "InspectionExpiryDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiryDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Make",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "NextServiceDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Odometer",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleClass",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Vehicles");

            migrationBuilder.RenameTable(
                name: "Vehicles",
                newName: "Trucks");

            migrationBuilder.RenameColumn(
                name: "VehicleLocationLongitude",
                table: "Trips",
                newName: "TruckLocationLongitude");

            migrationBuilder.RenameColumn(
                name: "VehicleLocationLatitude",
                table: "Trips",
                newName: "TruckLocationLatitude");

            migrationBuilder.RenameColumn(
                name: "VehicleId",
                table: "Trips",
                newName: "TruckId");

            migrationBuilder.RenameIndex(
                name: "IX_Trips_VehicleId",
                table: "Trips",
                newName: "IX_Trips_TruckId");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_LicensePlate",
                table: "Trucks",
                newName: "IX_Trucks_LicensePlate");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Trucks",
                table: "Trucks",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Trucks_TruckId",
                table: "Trips",
                column: "TruckId",
                principalTable: "Trucks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
