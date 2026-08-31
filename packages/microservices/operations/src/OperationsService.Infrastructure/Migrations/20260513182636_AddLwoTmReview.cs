using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLwoTmReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BenchChecklistJson",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BenchSubmittedAt",
                table: "LabWorkOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmApprovalNotes",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmRejectionReason",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TmReviewedAt",
                table: "LabWorkOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmReviewedById",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmReviewedByName",
                table: "LabWorkOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BenchChecklistJson",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "BenchSubmittedAt",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TmApprovalNotes",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TmRejectionReason",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TmReviewedAt",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TmReviewedById",
                table: "LabWorkOrders");

            migrationBuilder.DropColumn(
                name: "TmReviewedByName",
                table: "LabWorkOrders");
        }
    }
}
