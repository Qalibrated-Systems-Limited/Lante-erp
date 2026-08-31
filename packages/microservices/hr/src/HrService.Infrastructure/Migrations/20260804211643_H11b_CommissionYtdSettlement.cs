using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H11b_CommissionYtdSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CommissionEarnedToDate",
                table: "CommissionStatements",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PriorCommissionThisYear",
                table: "CommissionStatements",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnrecoveredOverpayment",
                table: "CommissionStatements",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommissionEarnedToDate",
                table: "CommissionStatements");

            migrationBuilder.DropColumn(
                name: "PriorCommissionThisYear",
                table: "CommissionStatements");

            migrationBuilder.DropColumn(
                name: "UnrecoveredOverpayment",
                table: "CommissionStatements");
        }
    }
}
