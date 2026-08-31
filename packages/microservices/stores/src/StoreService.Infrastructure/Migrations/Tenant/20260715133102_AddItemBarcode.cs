using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddItemBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "item_masters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "item_masters");
        }
    }
}
