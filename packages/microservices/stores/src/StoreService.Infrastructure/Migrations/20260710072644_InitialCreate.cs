using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    KraPin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Rating = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "item_masters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Uom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinSellingPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MinStockLevel = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MaxStockLevel = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ReorderQty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    AvgWeightedCost = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    QtyOnHand = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_masters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_masters_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "goods_received_notes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    QtyReceived = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LandedCost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InspectionStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InspectedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    InspectedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goods_received_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_goods_received_notes_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_goods_received_notes_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_price_histories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PurchasedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    VariancePct = table.Column<decimal>(type: "numeric(9,4)", nullable: false),
                    AlertLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceGrnId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_price_histories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_price_histories_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_price_histories_suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_units",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    GrnId = table.Column<string>(type: "text", nullable: false),
                    SerialNo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_units_goods_received_notes_GrnId",
                        column: x => x.GrnId,
                        principalTable: "goods_received_notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_units_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sold_items",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    StockUnitId = table.Column<string>(type: "text", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SerialNo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SalePrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SoldOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CostAtSale = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sold_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sold_items_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sold_items_stock_units_StockUnitId",
                        column: x => x.StockUnitId,
                        principalTable: "stock_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_take_reconciliations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    StockUnitId = table.Column<string>(type: "text", nullable: true),
                    PhysicalCount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SystemCount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_take_reconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_take_reconciliations_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_take_reconciliations_stock_units_StockUnitId",
                        column: x => x.StockUnitId,
                        principalTable: "stock_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_issue_notes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    StockUnitId = table.Column<string>(type: "text", nullable: true),
                    QtyIssued = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostCenter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssueType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IssuedTo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_issue_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_issue_notes_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_issue_notes_stock_units_StockUnitId",
                        column: x => x.StockUnitId,
                        principalTable: "stock_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_goods_received_notes_InspectionStatus",
                table: "goods_received_notes",
                column: "InspectionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_goods_received_notes_ItemId",
                table: "goods_received_notes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_received_notes_SupplierId",
                table: "goods_received_notes",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_Category",
                table: "item_masters",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_ItemCode",
                table: "item_masters",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_SupplierId",
                table: "item_masters",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_price_histories_ItemId_SupplierId_PurchasedOn",
                table: "purchase_price_histories",
                columns: new[] { "ItemId", "SupplierId", "PurchasedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_price_histories_SupplierId",
                table: "purchase_price_histories",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_sold_items_ClientId",
                table: "sold_items",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_sold_items_InvoiceNo",
                table: "sold_items",
                column: "InvoiceNo");

            migrationBuilder.CreateIndex(
                name: "IX_sold_items_ItemId",
                table: "sold_items",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_sold_items_StockUnitId",
                table: "sold_items",
                column: "StockUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_take_reconciliations_ItemId",
                table: "stock_take_reconciliations",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_take_reconciliations_Status",
                table: "stock_take_reconciliations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_stock_take_reconciliations_StockUnitId",
                table: "stock_take_reconciliations",
                column: "StockUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_GrnId",
                table: "stock_units",
                column: "GrnId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_ItemId",
                table: "stock_units",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_SerialNo",
                table: "stock_units",
                column: "SerialNo",
                unique: true,
                filter: "\"SerialNo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_Status",
                table: "stock_units",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_store_issue_notes_CostCenter",
                table: "store_issue_notes",
                column: "CostCenter");

            migrationBuilder.CreateIndex(
                name: "IX_store_issue_notes_IssueType",
                table: "store_issue_notes",
                column: "IssueType");

            migrationBuilder.CreateIndex(
                name: "IX_store_issue_notes_ItemId",
                table: "store_issue_notes",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_store_issue_notes_StockUnitId",
                table: "store_issue_notes",
                column: "StockUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_KraPin",
                table: "suppliers",
                column: "KraPin");

            migrationBuilder.CreateIndex(
                name: "IX_suppliers_Name",
                table: "suppliers",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_price_histories");

            migrationBuilder.DropTable(
                name: "sold_items");

            migrationBuilder.DropTable(
                name: "stock_take_reconciliations");

            migrationBuilder.DropTable(
                name: "store_issue_notes");

            migrationBuilder.DropTable(
                name: "stock_units");

            migrationBuilder.DropTable(
                name: "goods_received_notes");

            migrationBuilder.DropTable(
                name: "item_masters");

            migrationBuilder.DropTable(
                name: "suppliers");
        }
    }
}
