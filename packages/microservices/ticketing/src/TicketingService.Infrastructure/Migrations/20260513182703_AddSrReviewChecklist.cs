using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSrReviewChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizingName",
                schema: "public",
                table: "ServiceRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificateIssuedAt",
                schema: "public",
                table: "ServiceRequests",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateNumber",
                schema: "public",
                table: "ServiceRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedServiceDate",
                schema: "public",
                table: "ServiceRequests",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewChecklistJson",
                schema: "public",
                table: "ServiceRequests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizingName",
                schema: "public",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CertificateIssuedAt",
                schema: "public",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "CertificateNumber",
                schema: "public",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "PlannedServiceDate",
                schema: "public",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "ReviewChecklistJson",
                schema: "public",
                table: "ServiceRequests");
        }
    }
}
