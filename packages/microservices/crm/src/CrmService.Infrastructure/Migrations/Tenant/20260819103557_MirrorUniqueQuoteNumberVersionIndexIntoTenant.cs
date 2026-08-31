using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class MirrorUniqueQuoteNumberVersionIndexIntoTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuoteNumber_Version",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuoteNumber_Version",
                table: "Quotations",
                columns: new[] { "QuoteNumber", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuoteNumber_Version",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuoteNumber_Version",
                table: "Quotations",
                columns: new[] { "QuoteNumber", "Version" });
        }
    }
}
