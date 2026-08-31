using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRlsPolicies : Migration
    {
        private static readonly string[] Tables =
        [
            "WorkflowRules", "TicketWatchers", "TicketTags", "Tickets", "TicketHistories",
            "TicketEscalations", "TicketComments", "TicketCategories", "TicketAttachments",
            "TicketAssignments", "Tags", "SLAPolicies", "ServiceRequests",
            "ServiceRequestInstruments", "SatisfactionRatings", "Quotations",
            "PendingVerifications", "Notifications", "Macros", "EscalationRules"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                // Enable RLS — lante_user is superuser/BYPASSRLS so migrations are unaffected
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" ENABLE ROW LEVEL SECURITY;");

                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON public."{table}"
                      AS PERMISSIVE FOR ALL TO qalicore_app
                      USING (
                        "TenantId" IS NULL
                        OR "TenantId" = current_setting('app.current_tenant', true)
                      )
                      WITH CHECK (
                        "TenantId" IS NULL
                        OR "TenantId" = current_setting('app.current_tenant', true)
                      );
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON public.\"{table}\";");
                migrationBuilder.Sql($"ALTER TABLE public.\"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
