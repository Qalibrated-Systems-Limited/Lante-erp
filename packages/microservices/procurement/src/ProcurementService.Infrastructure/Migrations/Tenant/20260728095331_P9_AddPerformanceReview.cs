using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class P9_AddPerformanceReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PromisedDeliveryDate",
                table: "PurchaseOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQty",
                table: "PurchaseOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "SupplierPerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    SupplierName = table.Column<string>(type: "text", nullable: true),
                    ReviewPeriod = table.Column<string>(type: "text", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    QualityScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    PricingScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ComplianceScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AssessedWeight = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    PoCount = table.Column<int>(type: "integer", nullable: false),
                    ReceivedPoCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RejectedQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RejectRatePct = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OnTimeCount = table.Column<int>(type: "integer", nullable: false),
                    LateCount = table.Column<int>(type: "integer", nullable: false),
                    AvgLeadTimeDays = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DeliveryFromPromisedDates = table.Column<bool>(type: "boolean", nullable: false),
                    MatchCount = table.Column<int>(type: "integer", nullable: false),
                    MatchCleanCount = table.Column<int>(type: "integer", nullable: false),
                    QuoteCount = table.Column<int>(type: "integer", nullable: false),
                    LowestQuoteCount = table.Column<int>(type: "integer", nullable: false),
                    CoreDocsRequired = table.Column<int>(type: "integer", nullable: false),
                    CoreDocsValid = table.Column<int>(type: "integer", nullable: false),
                    GiftCount = table.Column<int>(type: "integer", nullable: false),
                    ConflictFound = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryMinScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BelowCategoryThreshold = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendBlacklist = table.Column<bool>(type: "boolean", nullable: false),
                    MdEscalatedBy = table.Column<string>(type: "text", nullable: true),
                    MdEscalatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "text", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_SupplierPerformanceReviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceReviews_Outcome",
                table: "SupplierPerformanceReviews",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceReviews_ReviewPeriod",
                table: "SupplierPerformanceReviews",
                column: "ReviewPeriod");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPerformanceReviews_SupplierId_ReviewPeriod",
                table: "SupplierPerformanceReviews",
                columns: new[] { "SupplierId", "ReviewPeriod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierPerformanceReviews");

            migrationBuilder.DropColumn(
                name: "PromisedDeliveryDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RejectedQty",
                table: "PurchaseOrders");
        }
    }
}
