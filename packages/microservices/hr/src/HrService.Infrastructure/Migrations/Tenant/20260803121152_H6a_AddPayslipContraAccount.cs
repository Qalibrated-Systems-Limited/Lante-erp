using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H6a_AddPayslipContraAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContraGlAccountCode",
                table: "PayslipLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContraGlAccountId",
                table: "PayslipLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContraGlAccountName",
                table: "PayslipLines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContraGlAccountCode",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "ContraGlAccountId",
                table: "PayslipLines");

            migrationBuilder.DropColumn(
                name: "ContraGlAccountName",
                table: "PayslipLines");
        }
    }
}
