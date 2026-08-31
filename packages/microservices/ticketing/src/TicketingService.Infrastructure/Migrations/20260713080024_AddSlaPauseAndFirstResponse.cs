using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaPauseAndFirstResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseAt",
                table: "Tickets",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaPausedAt",
                table: "Tickets",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SlaPausedHours",
                table: "Tickets",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstResponseAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPausedHours",
                table: "Tickets");
        }
    }
}
