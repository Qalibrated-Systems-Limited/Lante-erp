using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P6_AddThreeWayMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchingExceptions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ThreeWayMatchId = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<string>(type: "text", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: true),
                    ExceptionType = table.Column<int>(type: "integer", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RaisedBy = table.Column<string>(type: "text", nullable: false),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Resolution = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchingExceptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThreeWayMatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<string>(type: "text", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierInvoiceId = table.Column<string>(type: "text", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    PoTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InvoiceTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceivedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReceivedOk = table.Column<bool>(type: "boolean", nullable: false),
                    PriceOk = table.Column<bool>(type: "boolean", nullable: false),
                    TotalOk = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaymentVoucherRef = table.Column<string>(type: "text", nullable: true),
                    PaymentVoucherNo = table.Column<string>(type: "text", nullable: true),
                    MatchedBy = table.Column<string>(type: "text", nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreeWayMatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchingExceptions_PoId",
                table: "MatchingExceptions",
                column: "PoId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchingExceptions_Status",
                table: "MatchingExceptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThreeWayMatches_PoId",
                table: "ThreeWayMatches",
                column: "PoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThreeWayMatches_Status",
                table: "ThreeWayMatches",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchingExceptions");

            migrationBuilder.DropTable(
                name: "ThreeWayMatches");
        }
    }
}
