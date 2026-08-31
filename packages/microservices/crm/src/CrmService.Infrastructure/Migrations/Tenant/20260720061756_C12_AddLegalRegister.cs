using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class C12_AddLegalRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarrierAgreements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AgreementNumber = table.Column<string>(type: "text", nullable: false),
                    CarrierName = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: true),
                    NtsaLicenceNumber = table.Column<string>(type: "text", nullable: true),
                    NtsaLicenceExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GoodsInTransitInsuranceExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VehicleInspectionExpiry = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VettingStatus = table.Column<int>(type: "integer", nullable: false),
                    VettingNotes = table.Column<string>(type: "text", nullable: true),
                    VettedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    VettedBy = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    NtsaAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InsuranceAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InspectionAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarrierAgreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkAgreements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AgreementNumber = table.Column<string>(type: "text", nullable: false),
                    CounterpartyName = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PerformanceReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    ReviewAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RenewalAlert60SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RenewalAlert30SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkAgreements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NdaRegisters",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    NdaNumber = table.Column<string>(type: "text", nullable: false),
                    CounterpartyName = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: true),
                    Purpose = table.Column<string>(type: "text", nullable: true),
                    SignedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    ExpiryAlert60SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NdaRegisters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractorAgreements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AgreementNumber = table.Column<string>(type: "text", nullable: false),
                    SubcontractorName = table.Column<string>(type: "text", nullable: false),
                    SupplierId = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<string>(type: "text", nullable: true),
                    ScopeOfWork = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    InsuranceExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    InsuranceAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RenewalAlert60SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RenewalAlert30SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractorAgreements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarrierAgreements_AgreementNumber",
                table: "CarrierAgreements",
                column: "AgreementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CarrierAgreements_Status",
                table: "CarrierAgreements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CarrierAgreements_VettingStatus",
                table: "CarrierAgreements",
                column: "VettingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkAgreements_AgreementNumber",
                table: "FrameworkAgreements",
                column: "AgreementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkAgreements_EndDate",
                table: "FrameworkAgreements",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkAgreements_Status",
                table: "FrameworkAgreements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NdaRegisters_ExpiryDate",
                table: "NdaRegisters",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_NdaRegisters_NdaNumber",
                table: "NdaRegisters",
                column: "NdaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NdaRegisters_Status",
                table: "NdaRegisters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorAgreements_AgreementNumber",
                table: "SubcontractorAgreements",
                column: "AgreementNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorAgreements_EndDate",
                table: "SubcontractorAgreements",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractorAgreements_Status",
                table: "SubcontractorAgreements",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarrierAgreements");

            migrationBuilder.DropTable(
                name: "FrameworkAgreements");

            migrationBuilder.DropTable(
                name: "NdaRegisters");

            migrationBuilder.DropTable(
                name: "SubcontractorAgreements");
        }
    }
}
