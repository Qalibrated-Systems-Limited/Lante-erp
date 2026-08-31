using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddTicketNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TicketNumber",
                table: "Tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // D1-4 — backfill existing rows with per-tenant sequential numbers (oldest first) so their
            // references become TKT-000001… instead of falling back to the legacy GUID prefix, and so
            // MAX+1 continues from the right place. Partition by TenantId for the shared/public plane;
            // in a single-tenant schema all rows fall into one partition.
            migrationBuilder.Sql(@"
                WITH ordered AS (
                    SELECT ""Id"", ROW_NUMBER() OVER (PARTITION BY ""TenantId"" ORDER BY ""CreatedAt"", ""Id"") AS rn
                    FROM ""Tickets""
                )
                UPDATE ""Tickets"" t SET ""TicketNumber"" = o.rn
                FROM ordered o WHERE t.""Id"" = o.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TenantId_TicketNumber",
                table: "Tickets",
                columns: new[] { "TenantId", "TicketNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_TenantId_TicketNumber",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TicketNumber",
                table: "Tickets");
        }
    }
}
