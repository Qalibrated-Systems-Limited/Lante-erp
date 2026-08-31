using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    /// <summary>
    /// Creates FieldVehicles/VehicleDispatches/DispatchFuelLogs in the tenant schema lineage.
    /// These tables were added to the "public" schema outside of the tenant migration history
    /// (see the non-tenant AddFieldVehicleTables migration for the guarded, idempotent version
    /// that reconciles "public" without erroring on the tables that already exist there) — no
    /// tenant schema ever got them, so every tenant's Field Vehicles feature was silently backed
    /// by a nonexistent table. Column shapes copied verbatim from the live "public" tables.
    /// </summary>
    public partial class AddFieldVehicleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldVehicles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "text", nullable: false),
                    Make = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: true),
                    CurrentOdometer = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastServiceOdometer = table.Column<decimal>(type: "numeric", nullable: true),
                    InsuranceExpiry = table.Column<string>(type: "text", nullable: true),
                    InspectionExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldVehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleDispatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AssignmentId = table.Column<string>(type: "text", nullable: false),
                    FieldVehicleId = table.Column<string>(type: "text", nullable: false),
                    DriverName = table.Column<string>(type: "text", nullable: false),
                    DepartureDatetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DepartureOdometer = table.Column<decimal>(type: "numeric", nullable: false),
                    FuelLevelOut = table.Column<string>(type: "text", nullable: false),
                    ReturnDatetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnOdometer = table.Column<decimal>(type: "numeric", nullable: true),
                    FuelLevelIn = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleDispatches_FieldVehicles_FieldVehicleId",
                        column: x => x.FieldVehicleId,
                        principalTable: "FieldVehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DispatchFuelLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DispatchId = table.Column<string>(type: "text", nullable: false),
                    AmountLitres = table.Column<decimal>(type: "numeric", nullable: false),
                    CostKes = table.Column<decimal>(type: "numeric", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispatchFuelLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispatchFuelLogs_VehicleDispatches_DispatchId",
                        column: x => x.DispatchId,
                        principalTable: "VehicleDispatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleDispatches_FieldVehicleId",
                table: "VehicleDispatches",
                column: "FieldVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_DispatchFuelLogs_DispatchId",
                table: "DispatchFuelLogs",
                column: "DispatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DispatchFuelLogs");
            migrationBuilder.DropTable(name: "VehicleDispatches");
            migrationBuilder.DropTable(name: "FieldVehicles");
        }
    }
}
