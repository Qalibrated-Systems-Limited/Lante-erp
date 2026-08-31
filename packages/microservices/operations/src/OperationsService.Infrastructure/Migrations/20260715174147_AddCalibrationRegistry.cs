using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalibrationRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferenceStandardId",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceabilityRefFallback",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalibrationAuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    LabWorkOrderId = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    PerformedById = table.Column<string>(type: "text", nullable: false),
                    PerformedByName = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationCertificates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    LabWorkOrderId = table.Column<string>(type: "text", nullable: false),
                    AssignmentId = table.Column<string>(type: "text", nullable: true),
                    ServiceRequestId = table.Column<string>(type: "text", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IssuedById = table.Column<string>(type: "text", nullable: false),
                    SignatoryId = table.Column<string>(type: "text", nullable: false),
                    SignatoryName = table.Column<string>(type: "text", nullable: false),
                    CertJson = table.Column<string>(type: "text", nullable: true),
                    EnvConditionsJson = table.Column<string>(type: "text", nullable: true),
                    ReferenceStandardIds = table.Column<string>(type: "text", nullable: true),
                    TraceabilityRef = table.Column<string>(type: "text", nullable: true),
                    NextCalibrationDue = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferenceStandards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AssetId = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    NominalValue = table.Column<string>(type: "text", nullable: true),
                    AccuracyClass = table.Column<string>(type: "text", nullable: true),
                    TraceabilityCertNo = table.Column<string>(type: "text", nullable: true),
                    IssuingBody = table.Column<string>(type: "text", nullable: true),
                    CertUncertainty = table.Column<double>(type: "double precision", nullable: true),
                    CoverageFactor = table.Column<int>(type: "integer", nullable: false),
                    CalibrationDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Alert60SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Alert30SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceStandards", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationAuditLogs_LabWorkOrderId",
                table: "CalibrationAuditLogs",
                column: "LabWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_LabWorkOrderId",
                table: "CalibrationCertificates",
                column: "LabWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_Number",
                table: "CalibrationCertificates",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceStandards_AssetId",
                table: "ReferenceStandards",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceStandards_NextDueDate",
                table: "ReferenceStandards",
                column: "NextDueDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalibrationAuditLogs");

            migrationBuilder.DropTable(
                name: "CalibrationCertificates");

            migrationBuilder.DropTable(
                name: "ReferenceStandards");

            migrationBuilder.DropColumn(
                name: "ReferenceStandardId",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TraceabilityRefFallback",
                table: "LabWorkOrders");
        }
    }
}
