using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAckAndRepeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AckSentAt",
                table: "Tickets",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRepeat",
                table: "Tickets",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AckSentAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsRepeat",
                table: "Tickets");
        }
    }
}
