using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatutoryCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertDays",
                table: "RegulatoryLicences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RenewalRequirements",
                table: "RegulatoryLicences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "RegulatoryLicences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AnnualReturns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FiledDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualReturns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CosecTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ObligationId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ResponsiblePersonUserId = table.Column<string>(type: "text", nullable: true),
                    ResponsiblePersonName = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
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
                    table.PrimaryKey("PK_CosecTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryObligations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Authority = table.Column<string>(type: "text", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    StatutoryDay = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_StatutoryObligations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxComplianceCerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ItaxRef = table.Column<string>(type: "text", nullable: true),
                    AlertDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxComplianceCerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryDeadlines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ObligationId = table.Column<string>(type: "text", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FiledOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    OwnerUserId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutoryDeadlines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StatutoryDeadlines_StatutoryObligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "StatutoryObligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryLicences_Type",
                table: "RegulatoryLicences",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AnnualReturns_Year",
                table: "AnnualReturns",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CosecTasks_DueDate",
                table: "CosecTasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDeadlines_DueDate",
                table: "StatutoryDeadlines",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryDeadlines_ObligationId_DueDate",
                table: "StatutoryDeadlines",
                columns: new[] { "ObligationId", "DueDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxComplianceCerts_ExpiryDate",
                table: "TaxComplianceCerts",
                column: "ExpiryDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualReturns");

            migrationBuilder.DropTable(
                name: "CosecTasks");

            migrationBuilder.DropTable(
                name: "StatutoryDeadlines");

            migrationBuilder.DropTable(
                name: "TaxComplianceCerts");

            migrationBuilder.DropTable(
                name: "StatutoryObligations");

            migrationBuilder.DropIndex(
                name: "IX_RegulatoryLicences_Type",
                table: "RegulatoryLicences");

            migrationBuilder.DropColumn(
                name: "AlertDays",
                table: "RegulatoryLicences");

            migrationBuilder.DropColumn(
                name: "RenewalRequirements",
                table: "RegulatoryLicences");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "RegulatoryLicences");
        }
    }
}
