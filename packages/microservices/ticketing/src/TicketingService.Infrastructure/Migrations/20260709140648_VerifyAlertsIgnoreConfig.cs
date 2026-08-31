using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class VerifyAlertsIgnoreConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scaffolded after adding modelBuilder.Entity<Alert>().Ignore(a => a.Ticket): the model
            // snapshot still declared the convention FK (the hand-written 20260709135512_DropAlertsTicketFK
            // migration dropped the physical constraint but couldn't update the snapshot), so EF diffed
            // both the FK and its by-convention index away. The scaffolded DropForeignKey was removed
            // here — the constraint is already gone (dropped by 20260709135512) and dropping it again
            // would fail. Only the orphaned index remains to clean up.
            migrationBuilder.DropIndex(
                name: "IX_Alerts_TicketId",
                table: "Alerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // FK re-creation intentionally omitted — 20260709135512_DropAlertsTicketFK owns that.
            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TicketId",
                table: "Alerts",
                column: "TicketId");
        }
    }
}
