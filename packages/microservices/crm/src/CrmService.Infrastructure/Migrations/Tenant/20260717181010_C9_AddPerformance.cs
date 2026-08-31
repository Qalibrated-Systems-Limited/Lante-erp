using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class C9_AddPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalPipelineValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WeightedPipelineValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OpenCount = table.Column<int>(type: "integer", nullable: false),
                    ByStageJson = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RevenueSnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DailyRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    WtdRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MtdRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    YtdRevenue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TargetMtd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevenueSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesTargets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    PeriodType = table.Column<int>(type: "integer", nullable: false),
                    PeriodLabel = table.Column<string>(type: "text", nullable: false),
                    RevenueTarget = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineSnapshots_SnapshotDate",
                table: "PipelineSnapshots",
                column: "SnapshotDate");

            migrationBuilder.CreateIndex(
                name: "IX_RevenueSnapshots_EmployeeId_SnapshotDate",
                table: "RevenueSnapshots",
                columns: new[] { "EmployeeId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesTargets_EmployeeId_PeriodType_PeriodLabel",
                table: "SalesTargets",
                columns: new[] { "EmployeeId", "PeriodType", "PeriodLabel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineSnapshots");

            migrationBuilder.DropTable(
                name: "RevenueSnapshots");

            migrationBuilder.DropTable(
                name: "SalesTargets");
        }
    }
}
