using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class TenantInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "licenses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AppId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Features = table.Column<string>(type: "TEXT", nullable: false),
                    MachineId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Revoked = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    RevokeReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastSeen = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    LastMachineId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licenses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_licenses_AppId",
                table: "licenses",
                column: "AppId");

            migrationBuilder.CreateIndex(
                name: "IX_licenses_CustomerId",
                table: "licenses",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_licenses_CustomerId_AppId",
                table: "licenses",
                columns: new[] { "CustomerId", "AppId" });

            migrationBuilder.CreateIndex(
                name: "IX_licenses_ExpiresAt",
                table: "licenses",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_licenses_Token",
                table: "licenses",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "licenses");
        }
    }
}
