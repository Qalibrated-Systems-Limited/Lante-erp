using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectOwnershipAndMilestoneBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessOwnerId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                table: "Milestones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LdApplies",
                table: "Milestones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignOffAt",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignOffBy",
                table: "Milestones",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProcessOwnerId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "LdApplies",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "SignOffAt",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "SignOffBy",
                table: "Milestones");
        }
    }
}
