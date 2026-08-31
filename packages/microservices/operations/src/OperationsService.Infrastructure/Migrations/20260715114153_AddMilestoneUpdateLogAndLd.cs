using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneUpdateLogAndLd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LdCapPct",
                table: "Projects",
                type: "numeric(6,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LdRatePerDay",
                table: "Projects",
                type: "numeric(6,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "BreachAlertedOn",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LdAmount",
                table: "Milestones",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPct",
                table: "Milestones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MilestoneUpdateLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MilestoneId = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    StatusAtUpdate = table.Column<string>(type: "text", nullable: false),
                    ProgressPct = table.Column<int>(type: "integer", nullable: true),
                    IsBreachAlert = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneUpdateLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilestoneUpdateLogs_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalTable: "Milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneUpdateLogs_MilestoneId",
                table: "MilestoneUpdateLogs",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneUpdateLogs_ProjectId",
                table: "MilestoneUpdateLogs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MilestoneUpdateLogs");

            migrationBuilder.DropColumn(
                name: "LdCapPct",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LdRatePerDay",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BreachAlertedOn",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "LdAmount",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "ProgressPct",
                table: "Milestones");
        }
    }
}
