using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRedFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedFlagRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DataSourceId = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    ThresholdValue = table.Column<decimal>(type: "numeric", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedFlagRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RedFlagRules_DataSources_DataSourceId",
                        column: x => x.DataSourceId,
                        principalTable: "DataSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RedFlagEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RedFlagRuleId = table.Column<string>(type: "text", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    DaysOpen = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedFlagEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RedFlagEvents_RedFlagRules_RedFlagRuleId",
                        column: x => x.RedFlagRuleId,
                        principalTable: "RedFlagRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RedFlagEvents_RedFlagRuleId",
                table: "RedFlagEvents",
                column: "RedFlagRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_RedFlagRules_DataSourceId",
                table: "RedFlagRules",
                column: "DataSourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedFlagEvents");

            migrationBuilder.DropTable(
                name: "RedFlagRules");
        }
    }
}
