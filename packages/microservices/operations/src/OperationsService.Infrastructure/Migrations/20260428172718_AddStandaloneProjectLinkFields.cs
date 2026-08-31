using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStandaloneProjectLinkFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LinkedAt",
                table: "Assignments",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedBy",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedMilestoneId",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedProjectId",
                table: "Assignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "LinkedAt",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "LinkedBy",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "LinkedMilestoneId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "LinkedProjectId",
                table: "Assignments");
        }
    }
}
