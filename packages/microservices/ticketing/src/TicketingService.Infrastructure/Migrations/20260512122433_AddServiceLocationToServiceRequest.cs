using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceLocationToServiceRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceLocation",
                schema: "public",
                table: "ServiceRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceLocation",
                schema: "public",
                table: "ServiceRequests");
        }
    }
}
