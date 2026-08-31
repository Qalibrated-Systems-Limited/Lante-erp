using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class P5_AddGrnPoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedQty",
                table: "goods_received_notes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionNotes",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNoteUrl",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PartialDelivery",
                table: "goods_received_notes",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PoId",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoNumber",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RejectedQty",
                table: "goods_received_notes",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierInvoiceUrl",
                table: "goods_received_notes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoodsRejectionNotes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    GrnId = table.Column<string>(type: "text", nullable: true),
                    PoId = table.Column<string>(type: "text", nullable: true),
                    PoNumber = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<string>(type: "text", nullable: false),
                    ItemId = table.Column<string>(type: "text", nullable: true),
                    RejectedQty = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    PhotoUrls = table.Column<string>(type: "text", nullable: true),
                    RejectedItems = table.Column<string>(type: "text", nullable: true),
                    NotifiedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoodsRejectionNotes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoodsRejectionNotes");

            migrationBuilder.DropColumn(
                name: "AcceptedQty",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "ConditionNotes",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "DeliveryNoteUrl",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "PartialDelivery",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "PoId",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "PoNumber",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "RejectedQty",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "goods_received_notes");

            migrationBuilder.DropColumn(
                name: "SupplierInvoiceUrl",
                table: "goods_received_notes");
        }
    }
}
