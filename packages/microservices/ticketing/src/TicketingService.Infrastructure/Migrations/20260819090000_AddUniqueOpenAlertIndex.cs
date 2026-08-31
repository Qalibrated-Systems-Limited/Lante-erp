using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <summary>
    /// Closes the alert-dedup race described in #219. AlertService.CreateAsync checks
    /// ExistsOpenAsync() and then inserts, with nothing between — so at two replicas both see no open
    /// alert and both insert, and one SLA breach raises two alerts and notifies twice.
    ///
    /// Partial, matching the ExistsOpenAsync predicate exactly. A plain unique index would be wrong:
    /// once an alert is acknowledged the same condition should be able to raise a fresh one.
    /// </summary>
    public partial class AddUniqueOpenAlertIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-existing duplicates would make the index creation fail, and there is no reason to
            // believe there are none — the race has been live for as long as the workers have. Keep the
            // most recent of each duplicate group and acknowledge the rest, which is what an operator
            // would have done by hand and leaves the audit trail intact rather than deleting rows.
            migrationBuilder.Sql("""
                UPDATE "Alerts" a
                SET "IsAcknowledged" = TRUE,
                    "AcknowledgedAt" = NOW(),
                    "AcknowledgedBy" = 'system:AddUniqueOpenAlertIndex'
                WHERE NOT a."IsAcknowledged"
                  AND NOT a."IsDeleted"
                  AND a."Id" <> (
                      SELECT b."Id" FROM "Alerts" b
                      WHERE b."TenantId" = a."TenantId"
                        AND b."Source"   = a."Source"
                        AND b."Title"    = a."Title"
                        AND NOT b."IsAcknowledged"
                        AND NOT b."IsDeleted"
                      ORDER BY b."CreatedAt" DESC, b."Id" DESC
                      LIMIT 1
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_TenantId_Source_Title",
                table: "Alerts",
                columns: new[] { "TenantId", "Source", "Title" },
                unique: true,
                filter: "NOT \"IsAcknowledged\" AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_TenantId_Source_Title",
                table: "Alerts");
        }
    }
}
