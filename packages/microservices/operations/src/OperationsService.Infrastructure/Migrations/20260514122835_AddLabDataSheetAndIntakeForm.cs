using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabDataSheetAndIntakeForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalibrationSubType",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntakeFormJson",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabDataSheets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    LabWorkOrderId = table.Column<string>(type: "text", nullable: false),
                    SheetType = table.Column<string>(type: "text", nullable: false),
                    RawDataJson = table.Column<string>(type: "text", nullable: true),
                    CalculatedResultsJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedById = table.Column<string>(type: "text", nullable: true),
                    SubmittedByName = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabDataSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabDataSheets_LabWorkOrders_LabWorkOrderId",
                        column: x => x.LabWorkOrderId,
                        principalTable: "LabWorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabDataSheets_LabWorkOrderId",
                table: "LabDataSheets",
                column: "LabWorkOrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabDataSheets");

            migrationBuilder.DropColumn(
                name: "CalibrationSubType",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "IntakeFormJson",
                table: "LabWorkOrders");
        }
    }
}
