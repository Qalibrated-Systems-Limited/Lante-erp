using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddUniqueReferenceAndQuotationNumberIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #308: mirrors 20260817100101_AddUniqueReferenceAndQuotationNumberIndexes (the public
            // OperationsDbContext migration) into the Tenant migration history, which never got it.
            // The TenantOperationsDbContextModelSnapshot already declares these indexes unique — a
            // prior `dotnet ef migrations add` picked the model diff up incidentally alongside
            // unrelated work and its snapshot update was kept while its Up()/Down() operations were
            // deliberately stripped out (see 20260819083912_AddSearchTrigramIndexes) — so the actual
            // schema was never brought in line with the snapshot. `migrations add` sees no pending
            // diff for this reason; this migration is hand-written to close that gap.
            // Confirmed live against the one existing tenant schema (tenant_qsl) on 2026-08-19: zero
            // duplicate ReferenceNumber/QuotationNumber values, so this applies cleanly with no data
            // cleanup needed.
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber");
        }
    }
}
