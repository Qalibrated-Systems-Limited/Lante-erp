using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetMilestoneTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AttachmentPath",
                table: "CostEntries",
                newName: "Notes");

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedAmount",
                table: "Milestones",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignmentId",
                table: "CostEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetLineId",
                table: "CostEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MilestoneId",
                table: "CostEntries",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_BudgetLineId",
                table: "CostEntries",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_MilestoneId",
                table: "CostEntries",
                column: "MilestoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_CostEntries_BudgetLines_BudgetLineId",
                table: "CostEntries",
                column: "BudgetLineId",
                principalTable: "BudgetLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CostEntries_Milestones_MilestoneId",
                table: "CostEntries",
                column: "MilestoneId",
                principalTable: "Milestones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CostEntries_BudgetLines_BudgetLineId",
                table: "CostEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_CostEntries_Milestones_MilestoneId",
                table: "CostEntries");

            migrationBuilder.DropIndex(
                name: "IX_CostEntries_BudgetLineId",
                table: "CostEntries");

            migrationBuilder.DropIndex(
                name: "IX_CostEntries_MilestoneId",
                table: "CostEntries");

            migrationBuilder.DropColumn(
                name: "PlannedAmount",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "CostEntries");

            migrationBuilder.DropColumn(
                name: "BudgetLineId",
                table: "CostEntries");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "CostEntries");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "CostEntries",
                newName: "AttachmentPath");
        }
    }
}
