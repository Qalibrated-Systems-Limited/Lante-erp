using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// #332 — the AP mirror of AddInvoiceCancellation. <c>SupplierInvoiceStatus.Cancelled</c> was read
    /// at three sites and assigned by nothing, so a bill entered in error stayed an expense and a
    /// payable for ever.
    ///
    /// <para>Additive and nullable, so it is safe on a populated table: no existing bill is cancelled,
    /// and null is the right value for every one of them. <c>timestamp without time zone</c>, matching
    /// the table's other DateTime columns now that #337's reconciliation has merged — see
    /// AddInvoiceCancellation for the full history. Hand-authored for the same reason.</para>
    /// </remarks>
    public partial class AddSupplierInvoiceCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "SupplierInvoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "SupplierInvoices",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "SupplierInvoices");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "SupplierInvoices");
        }
    }
}
