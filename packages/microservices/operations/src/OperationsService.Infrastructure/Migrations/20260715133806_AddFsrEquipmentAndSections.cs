using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFsrEquipmentAndSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientRating",
                table: "ServiceReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpNotes",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FollowUpRequired",
                table: "ServiceReports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MaterialsUsed",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recommendations",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "ServiceReports",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkSummary",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FsrOverdueAlertedAt",
                table: "Assignments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FsrEquipment",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ServiceReportId = table.Column<string>(type: "text", nullable: false),
                    ServiceRequestInstrumentId = table.Column<string>(type: "text", nullable: true),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    Manufacturer = table.Column<string>(type: "text", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    TagNumber = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConditionBefore = table.Column<string>(type: "text", nullable: true),
                    ConditionAfter = table.Column<string>(type: "text", nullable: true),
                    WorkDone = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FsrEquipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FsrEquipment_ServiceReports_ServiceReportId",
                        column: x => x.ServiceReportId,
                        principalTable: "ServiceReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FsrEquipment_SerialNumber",
                table: "FsrEquipment",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_FsrEquipment_ServiceReportId",
                table: "FsrEquipment",
                column: "ServiceReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FsrEquipment");

            migrationBuilder.DropColumn(
                name: "ClientRating",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "FollowUpNotes",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "FollowUpRequired",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "MaterialsUsed",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "Recommendations",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "WorkSummary",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "FsrOverdueAlertedAt",
                table: "Assignments");
        }
    }
}
