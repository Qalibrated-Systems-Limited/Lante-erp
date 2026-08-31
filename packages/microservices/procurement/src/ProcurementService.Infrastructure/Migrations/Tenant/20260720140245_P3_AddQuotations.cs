using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class P3_AddQuotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuotationComparisons",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PrId = table.Column<string>(type: "text", nullable: false),
                    Band = table.Column<int>(type: "integer", nullable: false),
                    RequiredQuotes = table.Column<int>(type: "integer", nullable: false),
                    RecommendedSupplierId = table.Column<string>(type: "text", nullable: true),
                    RecommendedSupplierName = table.Column<string>(type: "text", nullable: true),
                    RecommendedQuotationId = table.Column<string>(type: "text", nullable: true),
                    SelectionReason = table.Column<string>(type: "text", nullable: true),
                    PriceScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationComparisons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    QuoteNumber = table.Column<string>(type: "text", nullable: false),
                    PrId = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: true),
                    QuoteDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidityDays = table.Column<int>(type: "integer", nullable: false),
                    TotalQuoted = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    QuotePdfUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PriceScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    QualityScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    TotalScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuotationLines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    QuotationId = table.Column<string>(type: "text", nullable: false),
                    ItemDescription = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationLines_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationComparisons_PrId",
                table: "QuotationComparisons",
                column: "PrId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuotationLines_QuotationId",
                table: "QuotationLines",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_PrId",
                table: "Quotations",
                column: "PrId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuoteNumber",
                table: "Quotations",
                column: "QuoteNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_SupplierId",
                table: "Quotations",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationComparisons");

            migrationBuilder.DropTable(
                name: "QuotationLines");

            migrationBuilder.DropTable(
                name: "Quotations");
        }
    }
}
