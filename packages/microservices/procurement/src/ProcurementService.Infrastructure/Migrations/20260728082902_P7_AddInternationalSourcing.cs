using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class P7_AddInternationalSourcing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomsDeclarations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IntlPoId = table.Column<string>(type: "text", nullable: false),
                    IdfNumber = table.Column<string>(type: "text", nullable: false),
                    EntryNumber = table.Column<string>(type: "text", nullable: true),
                    ImportDutyKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ClearingAgentFeeKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PortChargesKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DeclaredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomsDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternationalPos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PoId = table.Column<string>(type: "text", nullable: false),
                    PoNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    PurchasePriceFx = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ExchangeRateAtOrder = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    PurchasePriceKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ProformaInvoiceUrl = table.Column<string>(type: "text", nullable: true),
                    TtAmountFx = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TtRequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TtApprovedBy = table.Column<string>(type: "text", nullable: true),
                    TtApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TtSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TtVoucherRef = table.Column<string>(type: "text", nullable: true),
                    TtVoucherNo = table.Column<string>(type: "text", nullable: true),
                    BlNumber = table.Column<string>(type: "text", nullable: true),
                    Eta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalLandedCostKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LandedCostPerUnitKes = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    QuantityBasis = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternationalPos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandedCostComponents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IntlPoId = table.Column<string>(type: "text", nullable: false),
                    ComponentType = table.Column<int>(type: "integer", nullable: false),
                    AmountFx = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    KesAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IncurredOn = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    BlNumber = table.Column<string>(type: "text", nullable: true),
                    Eta = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    FromCustoms = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandedCostComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandedCostComponents_InternationalPos_IntlPoId",
                        column: x => x.IntlPoId,
                        principalTable: "InternationalPos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomsDeclarations_IntlPoId",
                table: "CustomsDeclarations",
                column: "IntlPoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternationalPos_PoId",
                table: "InternationalPos",
                column: "PoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternationalPos_Status",
                table: "InternationalPos",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LandedCostComponents_IntlPoId",
                table: "LandedCostComponents",
                column: "IntlPoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomsDeclarations");

            migrationBuilder.DropTable(
                name: "LandedCostComponents");

            migrationBuilder.DropTable(
                name: "InternationalPos");
        }
    }
}
