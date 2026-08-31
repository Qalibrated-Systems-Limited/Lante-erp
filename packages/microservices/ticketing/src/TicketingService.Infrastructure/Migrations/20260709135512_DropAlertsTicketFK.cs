using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropAlertsTicketFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alert.TicketId was never given an explicit schema, so EF's convention-based FK ended up
            // bound to public.Tickets — but Alert (see its TenantId column) is a shared, platform-wide
            // table spanning every tenant, while Tickets is schema-per-tenant (each tenant's real
            // tickets live in tenant_<slug>.Tickets, not public.Tickets). The FK therefore rejects every
            // alert for a real ticket, most visibly the recurring SLA/escalation background check
            // (SLABackgroundService) failing for every tenant schema. Drop it; TicketId stays a plain,
            // unconstrained column, same treatment as user-service's public-schema FKs into tenant data.
            migrationBuilder.DropForeignKey(
                name: "FK_Alerts_Tickets_TicketId",
                schema: "public",
                table: "Alerts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Alerts_Tickets_TicketId",
                schema: "public",
                table: "Alerts",
                column: "TicketId",
                principalSchema: "public",
                principalTable: "Tickets",
                principalColumn: "Id");
        }
    }
}
