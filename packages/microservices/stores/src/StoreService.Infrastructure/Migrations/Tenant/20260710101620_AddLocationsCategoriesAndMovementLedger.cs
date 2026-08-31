using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddLocationsCategoriesAndMovementLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_item_masters_Category",
                table: "item_masters");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "stock_units");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "item_masters");

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "store_issue_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "stock_units",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "stock_take_reconciliations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "stock_take_reconciliations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CategoryId",
                table: "item_masters",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LowStockAcknowledgedAt",
                table: "item_masters",
                type: "TIMESTAMPTZ",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LowStockAcknowledgedBy",
                table: "item_masters",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationId",
                table: "goods_received_notes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    LocationId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_movements_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_transfers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: false),
                    FromLocationId = table.Column<string>(type: "text", nullable: false),
                    ToLocationId = table.Column<string>(type: "text", nullable: false),
                    Qty = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_store_transfers_item_masters_ItemId",
                        column: x => x.ItemId,
                        principalTable: "item_masters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_transfers_locations_FromLocationId",
                        column: x => x.FromLocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_store_transfers_locations_ToLocationId",
                        column: x => x.ToLocationId,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_store_issue_notes_LocationId",
                table: "store_issue_notes",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_units_LocationId",
                table: "stock_units",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_take_reconciliations_LocationId",
                table: "stock_take_reconciliations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_CategoryId",
                table: "item_masters",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_goods_received_notes_LocationId",
                table: "goods_received_notes",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Code",
                table: "categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_Code",
                table: "locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_ItemId_LocationId",
                table: "stock_movements",
                columns: new[] { "ItemId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_LocationId",
                table: "stock_movements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_Reference",
                table: "stock_movements",
                column: "Reference");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_FromLocationId",
                table: "store_transfers",
                column: "FromLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_ItemId",
                table: "store_transfers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_Status",
                table: "store_transfers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_store_transfers_ToLocationId",
                table: "store_transfers",
                column: "ToLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_goods_received_notes_locations_LocationId",
                table: "goods_received_notes",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_item_masters_categories_CategoryId",
                table: "item_masters",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_take_reconciliations_locations_LocationId",
                table: "stock_take_reconciliations",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_units_locations_LocationId",
                table: "stock_units",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_store_issue_notes_locations_LocationId",
                table: "store_issue_notes",
                column: "LocationId",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_goods_received_notes_locations_LocationId",
                table: "goods_received_notes");

            migrationBuilder.DropForeignKey(
                name: "FK_item_masters_categories_CategoryId",
                table: "item_masters");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_take_reconciliations_locations_LocationId",
                table: "stock_take_reconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_units_locations_LocationId",
                table: "stock_units");

            migrationBuilder.DropForeignKey(
                name: "FK_store_issue_notes_locations_LocationId",
                table: "store_issue_notes");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "store_transfers");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropIndex(
                name: "IX_store_issue_notes_LocationId",
                table: "store_issue_notes");

            migrationBuilder.DropIndex(
                name: "IX_stock_units_LocationId",
                table: "stock_units");

            migrationBuilder.DropIndex(
                name: "IX_stock_take_reconciliations_LocationId",
                table: "stock_take_reconciliations");

            migrationBuilder.DropIndex(
                name: "IX_item_masters_CategoryId",
                table: "item_masters");

            migrationBuilder.DropIndex(
                name: "IX_goods_received_notes_LocationId",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "store_issue_notes");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "stock_units");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "stock_take_reconciliations");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "stock_take_reconciliations");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "item_masters");

            migrationBuilder.DropColumn(
                name: "LowStockAcknowledgedAt",
                table: "item_masters");

            migrationBuilder.DropColumn(
                name: "LowStockAcknowledgedBy",
                table: "item_masters");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "goods_received_notes");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "stock_units",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "item_masters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_Category",
                table: "item_masters",
                column: "Category");
        }
    }
}
