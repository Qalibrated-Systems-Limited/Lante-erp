using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaPolicyAmberAndBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmberThresholdPct",
                table: "SLAPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 75);

            migrationBuilder.AddColumn<string>(
                name: "BranchId",
                table: "SLAPolicies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmberThresholdPct",
                table: "SLAPolicies");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "SLAPolicies");
        }
    }
}
