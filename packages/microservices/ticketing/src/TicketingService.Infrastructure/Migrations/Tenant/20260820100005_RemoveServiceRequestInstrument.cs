using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    /// <remarks>
    /// #330 -- schema left behind after instrument-handling moved to operations-service: the public
    /// portal intake flow (PublicServiceRequestController) parses instruments as DTOs and forwards
    /// them via IOperationsServiceClient, where operations' OWN (separate, live) ServiceRequestInstrument
    /// table is the one actually written. Nothing here ever inserts into this copy. Hand-authored
    /// rather than `dotnet ef migrations add` because the live schema has an unrelated pre-existing
    /// timestamptz/timestamp drift (see #337) that would otherwise get bundled into this cleanup.
    /// </remarks>
    public partial class RemoveServiceRequestInstrument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceRequestInstruments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceRequestInstruments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ServiceRequestId = table.Column<string>(type: "text", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Manufacturer = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    TagNumber = table.Column<string>(type: "text", nullable: true),
                    Range = table.Column<string>(type: "text", nullable: true),
                    RangeUnit = table.Column<string>(type: "text", nullable: true),
                    Condition = table.Column<string>(type: "text", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    LastCalibrationDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    CertificateNumber = table.Column<string>(type: "text", nullable: true),
                    NawiInstrumentType = table.Column<string>(type: "text", nullable: true),
                    NawiCapacity = table.Column<string>(type: "text", nullable: true),
                    NawiScaleInterval = table.Column<string>(type: "text", nullable: true),
                    NawiAccuracyClass = table.Column<string>(type: "text", nullable: true),
                    MassNominalValue = table.Column<string>(type: "text", nullable: true),
                    MassAccuracyClass = table.Column<string>(type: "text", nullable: true),
                    ServiceType = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRequestInstruments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceRequestInstruments_ServiceRequests_ServiceRequestId",
                        column: x => x.ServiceRequestId,
                        principalTable: "ServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequestInstruments_ServiceRequestId",
                table: "ServiceRequestInstruments",
                column: "ServiceRequestId");
        }
    }
}
