using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class FixEmptyVehicleStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Vehicles that existed before the Status column was added were backfilled with ''
            // by the previous migration's column default, which the string-backed TruckStatus
            // enum converter cannot parse, causing 500s on every read of those rows.
            migrationBuilder.Sql("UPDATE \"Vehicles\" SET \"Status\" = 'Active' WHERE \"Status\" = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data fix — not reversible.
        }
    }
}
