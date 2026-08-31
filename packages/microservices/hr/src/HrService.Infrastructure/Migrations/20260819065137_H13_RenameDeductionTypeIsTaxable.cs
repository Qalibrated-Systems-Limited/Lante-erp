using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H13_RenameDeductionTypeIsTaxable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsTaxable",
                table: "PayrollDeductionTypes",
                newName: "ReducesTaxableIncome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReducesTaxableIncome",
                table: "PayrollDeductionTypes",
                newName: "IsTaxable");
        }
    }
}
