using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRequesterEmailAndPendingWarning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingWarningSentAt",
                table: "Tickets",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterEmail",
                table: "Tickets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingWarningSentAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RequesterEmail",
                table: "Tickets");
        }
    }
}
