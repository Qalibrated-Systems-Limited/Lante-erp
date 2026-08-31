using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubcontractsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentRetentions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AwardId = table.Column<string>(type: "text", nullable: false),
                    CertifiedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RetentionHeld = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Wht = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentRetentions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prequalifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SubcontractorId = table.Column<string>(type: "text", nullable: false),
                    Score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    DocumentUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "text", nullable: true),
                    ApprovedByName = table.Column<string>(type: "text", nullable: true),
                    ApprovedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prequalifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubconScorecards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AwardId = table.Column<string>(type: "text", nullable: false),
                    ProjectManagerUserId = table.Column<string>(type: "text", nullable: true),
                    ProjectManagerName = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubconScorecards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcontractAwards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SubcontractorId = table.Column<string>(type: "text", nullable: false),
                    SubcontractorName = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<string>(type: "text", nullable: true),
                    ProjectName = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SignedAgreementUrl = table.Column<string>(type: "text", nullable: true),
                    RamsApproved = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    MobilizationActivatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcontractAwards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subcontractors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TradeCategory = table.Column<string>(type: "text", nullable: false),
                    PqqScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    InsuranceExpiry = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TccExpiry = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    SafetyScore = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RamsSubmitted = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Prequalified = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    LatestPerformanceScore = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    WatchListed = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    IsRestricted = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    HasDeclaredRelationship = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    RelationshipDetails = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcontractors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRetentions_AwardId",
                table: "PaymentRetentions",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_Prequalifications_SubcontractorId",
                table: "Prequalifications",
                column: "SubcontractorId");

            migrationBuilder.CreateIndex(
                name: "IX_SubconScorecards_AwardId",
                table: "SubconScorecards",
                column: "AwardId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractAwards_ProjectId",
                table: "SubcontractAwards",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SubcontractAwards_SubcontractorId",
                table: "SubcontractAwards",
                column: "SubcontractorId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcontractors_TradeCategory",
                table: "Subcontractors",
                column: "TradeCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentRetentions");

            migrationBuilder.DropTable(
                name: "Prequalifications");

            migrationBuilder.DropTable(
                name: "SubconScorecards");

            migrationBuilder.DropTable(
                name: "SubcontractAwards");

            migrationBuilder.DropTable(
                name: "Subcontractors");
        }
    }
}
