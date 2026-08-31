using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddStockUnitAndSoldItemQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Qty",
                table: "stock_units",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Qty",
                table: "sold_items",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Qty",
                table: "stock_units");

            migrationBuilder.DropColumn(
                name: "Qty",
                table: "sold_items");
        }
    }
}
