using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// #332 — <c>InvoiceStatus.Cancelled</c> was read by seven filters in this service and assigned by
    /// nothing at all; making it reachable needs somewhere to record WHY an invoice was cancelled.
    ///
    /// <para>Both columns are nullable with no default, so this is additive and safe on a populated
    /// table: every existing row is not cancelled, and null is exactly the right value for it.</para>
    ///
    /// <para><c>timestamp without time zone</c>, matching every other DateTime column in this table.
    /// #337 (finance's 234-column timestamptz reconciliation) has merged since this migration was
    /// first authored, so that is now the type every sibling actually is, not a divergence from
    /// them.</para>
    ///
    /// <para>Hand-authored for the same reason as RemoveJournalEntryAttachment: running
    /// <c>dotnet ef migrations add</c> against an intermediate main would have bundled #337's
    /// then-unmerged drift into an unrelated two-column change.</para>
    /// </remarks>
    public partial class AddInvoiceCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "Invoices",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Invoices");
        }
    }
}
