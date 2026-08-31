using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "WorkflowRules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketWatchers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketTags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "Tickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketHistories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketEscalations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketComments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketAttachments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "TicketAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "Tags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "SLAPolicies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "ServiceRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "ServiceRequestInstruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "SatisfactionRatings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "Quotations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "PendingVerifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "Macros",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "public",
                table: "EscalationRules",
                type: "text",
                nullable: true);

            // Backfill existing rows with the QSL tenant ID
            const string qslId = "11111111-0000-0000-0000-000000000001";
            foreach (var t in new[] {
                "WorkflowRules","TicketWatchers","TicketTags","Tickets","TicketHistories",
                "TicketEscalations","TicketComments","TicketCategories","TicketAttachments",
                "TicketAssignments","Tags","SLAPolicies","ServiceRequests",
                "ServiceRequestInstruments","SatisfactionRatings","Quotations",
                "PendingVerifications","Notifications","Macros","EscalationRules"
            })
                migrationBuilder.Sql($"UPDATE public.\"{t}\" SET \"TenantId\" = '{qslId}' WHERE \"TenantId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "WorkflowRules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketWatchers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketTags");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketHistories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketEscalations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketComments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketAttachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "TicketAssignments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "SLAPolicies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "ServiceRequestInstruments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "SatisfactionRatings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Quotations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "PendingVerifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Macros");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "EscalationRules");
        }
    }
}
