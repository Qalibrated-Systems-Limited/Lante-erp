using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProcurementService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DECC_AddBoardResolutionVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardResolutionId",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BoardResolutionVerified",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardResolutionId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "BoardResolutionVerified",
                table: "PurchaseOrders");
        }
    }
}
