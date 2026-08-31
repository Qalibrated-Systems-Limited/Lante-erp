using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PR2_CheckInTimeCaptureAndBillable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "TimesheetEntries",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectTaskId",
                table: "TimesheetEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "CheckIns",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectId",
                table: "CheckIns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectTaskId",
                table: "CheckIns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimesheetEntryId",
                table: "CheckIns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "TimesheetEntries");

            migrationBuilder.DropColumn(
                name: "ProjectTaskId",
                table: "TimesheetEntries");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "ProjectTaskId",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "TimesheetEntryId",
                table: "CheckIns");
        }
    }
}
