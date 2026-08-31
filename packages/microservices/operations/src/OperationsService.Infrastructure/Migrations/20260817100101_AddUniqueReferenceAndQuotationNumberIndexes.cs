using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueReferenceAndQuotationNumberIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceRequests_ReferenceNumber",
                table: "ServiceRequests",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_QuotationNumber",
                table: "Quotations",
                column: "QuotationNumber");
        }
    }
}
