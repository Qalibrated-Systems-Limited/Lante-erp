using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P8_AddEmergencyProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmergencyApprovedAt",
                table: "PurchaseOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyApprovedBy",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmergencyProcurementLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<string>(type: "text", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    DeclaredBy = table.Column<string>(type: "text", nullable: false),
                    DeclaredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EmergencyReason = table.Column<string>(type: "text", nullable: false),
                    MdApprovalRef = table.Column<string>(type: "text", nullable: true),
                    MdApprovedBy = table.Column<string>(type: "text", nullable: true),
                    MdApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QuotationWaiverUrl = table.Column<string>(type: "text", nullable: false),
                    WaiverReason = table.Column<string>(type: "text", nullable: false),
                    PostHocDueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PostHocPrId = table.Column<string>(type: "text", nullable: true),
                    PostHocPrNumber = table.Column<string>(type: "text", nullable: true),
                    PostHocRaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BoardPackPeriod = table.Column<string>(type: "text", nullable: true),
                    BoardPackMarkedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmergencyProcurementLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementLogs_BoardPackPeriod",
                table: "EmergencyProcurementLogs",
                column: "BoardPackPeriod");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementLogs_DeclaredAt",
                table: "EmergencyProcurementLogs",
                column: "DeclaredAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyProcurementLogs_PoId",
                table: "EmergencyProcurementLogs",
                column: "PoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmergencyProcurementLogs");

            migrationBuilder.DropColumn(
                name: "EmergencyApprovedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "EmergencyApprovedBy",
                table: "PurchaseOrders");
        }
    }
}
