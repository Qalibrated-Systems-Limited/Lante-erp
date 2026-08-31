using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class P5_AddPoReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastGrnRef",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceiptStatus",
                table: "PurchaseOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                table: "PurchaseOrders",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQty",
                table: "PurchaseOrders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastGrnRef",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceiptStatus",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedQty",
                table: "PurchaseOrders");
        }
    }
}
