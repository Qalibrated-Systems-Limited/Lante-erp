using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialFieldsAndDualSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerSignatureData",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerSignatureName",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianSignatureData",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicianSignatureName",
                table: "ServiceReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Requisitions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineItemsJson",
                table: "Requisitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByName",
                table: "Requisitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "PettyCashForms",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfoComments",
                table: "PettyCashForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CfoReviewedAt",
                table: "PettyCashForms",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfoReviewedBy",
                table: "PettyCashForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedByName",
                table: "PettyCashForms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CfoComments",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CfoReviewedAt",
                table: "PerDiemReturnForms",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfoReviewedBy",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineItemsJson",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerComments",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByName",
                table: "PerDiemReturnForms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalSpent",
                table: "PerDiemReturnForms",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Claims",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountReturned",
                table: "AdvanceReturnForms",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CfoComments",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CfoReviewedAt",
                table: "AdvanceReturnForms",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfoReviewedBy",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerComments",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByName",
                table: "AdvanceReturnForms",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAccountedFor",
                table: "AdvanceReturnForms",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerSignatureData",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "CustomerSignatureName",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "TechnicianSignatureData",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "TechnicianSignatureName",
                table: "ServiceReports");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Requisitions");

            migrationBuilder.DropColumn(
                name: "LineItemsJson",
                table: "Requisitions");

            migrationBuilder.DropColumn(
                name: "RequestedByName",
                table: "Requisitions");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "CfoComments",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedAt",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedBy",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "RequestedByName",
                table: "PettyCashForms");

            migrationBuilder.DropColumn(
                name: "CfoComments",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedAt",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedBy",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "LineItemsJson",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "ManagerComments",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "SubmittedByName",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "TotalSpent",
                table: "PerDiemReturnForms");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "AmountReturned",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "CfoComments",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedAt",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "CfoReviewedBy",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "ManagerComments",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "SubmittedByName",
                table: "AdvanceReturnForms");

            migrationBuilder.DropColumn(
                name: "TotalAccountedFor",
                table: "AdvanceReturnForms");
        }
    }
}
