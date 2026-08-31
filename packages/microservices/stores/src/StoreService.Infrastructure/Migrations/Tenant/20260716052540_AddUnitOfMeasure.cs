using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StoreService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddUnitOfMeasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "units_of_measure",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units_of_measure", x => x.Id);
                });

            migrationBuilder.AddColumn<string>(
                name: "UomId",
                table: "item_masters",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill: see the equivalent non-tenant migration for why this runs here.
            migrationBuilder.Sql(@"
                INSERT INTO units_of_measure (""Id"", ""Name"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"", ""IsDeleted"")
                SELECT gen_random_uuid()::text, v.""Uom"", true, now(), now(), false
                FROM (SELECT DISTINCT ""Uom"" FROM item_masters WHERE ""Uom"" IS NOT NULL AND ""Uom"" <> '') v;

                UPDATE item_masters t
                SET ""UomId"" = vc.""Id""
                FROM units_of_measure vc
                WHERE vc.""Name"" = t.""Uom"";
            ");

            migrationBuilder.DropColumn(
                name: "Uom",
                table: "item_masters");

            migrationBuilder.CreateIndex(
                name: "IX_item_masters_UomId",
                table: "item_masters",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_units_of_measure_Name",
                table: "units_of_measure",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_item_masters_units_of_measure_UomId",
                table: "item_masters",
                column: "UomId",
                principalTable: "units_of_measure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_item_masters_units_of_measure_UomId",
                table: "item_masters");

            migrationBuilder.DropTable(
                name: "units_of_measure");

            migrationBuilder.DropIndex(
                name: "IX_item_masters_UomId",
                table: "item_masters");

            migrationBuilder.DropColumn(
                name: "UomId",
                table: "item_masters");

            migrationBuilder.AddColumn<string>(
                name: "Uom",
                table: "item_masters",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
