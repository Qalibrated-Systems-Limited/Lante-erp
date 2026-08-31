using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// #229 — flipping an Approved/PartPaid bill's 3-way match to Exception now flags it instead of
    /// silently rewriting the record while the GL still carries the liability. Additive and nullable,
    /// safe on a populated table: no existing bill is flagged, and null is correct for all of them.
    /// </remarks>
    public partial class AddSupplierInvoiceMatchExceptionFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MatchExceptionFlaggedAt",
                table: "SupplierInvoices",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MatchExceptionFlaggedAt",
                table: "SupplierInvoices");
        }
    }
}
