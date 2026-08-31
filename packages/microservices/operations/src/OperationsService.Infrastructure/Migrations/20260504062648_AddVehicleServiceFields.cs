using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleServiceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InsuranceExpiry",
                table: "FieldVehicles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastServiceDate",
                table: "FieldVehicles",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastServiceOdometer",
                table: "FieldVehicles",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextServiceDate",
                table: "FieldVehicles",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceExpiry",
                table: "FieldVehicles");

            migrationBuilder.DropColumn(
                name: "LastServiceDate",
                table: "FieldVehicles");

            migrationBuilder.DropColumn(
                name: "LastServiceOdometer",
                table: "FieldVehicles");

            migrationBuilder.DropColumn(
                name: "NextServiceDate",
                table: "FieldVehicles");
        }
    }
}
