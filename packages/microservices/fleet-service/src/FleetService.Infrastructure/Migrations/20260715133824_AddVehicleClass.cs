using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VehicleClass",
                table: "Vehicles",
                newName: "VehicleClassId");

            migrationBuilder.CreateTable(
                name: "VehicleClasses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleClasses", x => x.Id);
                });

            // Backfill: the renamed VehicleClassId column still holds the old free-text class
            // names (e.g. "Truck", "Pickup") at this point — turn each distinct value into a real
            // VehicleClasses row, then repoint Vehicles at the new row before the FK is added below
            // (a raw string value like "Truck" would otherwise violate the FK constraint).
            migrationBuilder.Sql(@"
                INSERT INTO ""VehicleClasses"" (""Id"", ""Name"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid()::text, v.""VehicleClassId"", now(), now(), false
                FROM (SELECT DISTINCT ""VehicleClassId"" FROM ""Vehicles"" WHERE ""VehicleClassId"" IS NOT NULL AND ""VehicleClassId"" <> '') v;

                UPDATE ""Vehicles"" t
                SET ""VehicleClassId"" = vc.""Id""
                FROM ""VehicleClasses"" vc
                WHERE vc.""Name"" = t.""VehicleClassId"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_VehicleClassId",
                table: "Vehicles",
                column: "VehicleClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_VehicleClasses_VehicleClassId",
                table: "Vehicles",
                column: "VehicleClassId",
                principalTable: "VehicleClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_VehicleClasses_VehicleClassId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "VehicleClasses");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_VehicleClassId",
                table: "Vehicles");

            migrationBuilder.RenameColumn(
                name: "VehicleClassId",
                table: "Vehicles",
                newName: "VehicleClass");
        }
    }
}
