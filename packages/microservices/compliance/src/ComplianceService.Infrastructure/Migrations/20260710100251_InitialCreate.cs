using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AntiBriberyTrainings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    CompletedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    NextDueOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CertificateUrl = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AntiBriberyTrainings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardResolutions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ReferenceNo = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ResolutionDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    ScannedCopyUrl = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardResolutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseActions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ParentType = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<string>(type: "text", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    LoggedByUserId = table.Column<string>(type: "text", nullable: true),
                    LoggedByName = table.Column<string>(type: "text", nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoiDeclarations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    DeclaredOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    HasConflict = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_CoiDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataBreaches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OdpcNotificationDueAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    OdpcNotifiedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RemediationNotes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataBreaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSubjectRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    RequestorName = table.Column<string>(type: "text", nullable: false),
                    RequestorContact = table.Column<string>(type: "text", nullable: true),
                    ReceivedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    DueBy = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
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
                    table.PrimaryKey("PK_DataSubjectRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GiftHospitalities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    CounterpartyName = table.Column<string>(type: "text", nullable: false),
                    IsGovernmentOfficial = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Date = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Flagged = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiftHospitalities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegulatoryLicences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Authority = table.Column<string>(type: "text", nullable: false),
                    LicenceNumber = table.Column<string>(type: "text", nullable: true),
                    IssuedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegulatoryLicences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RelatedPartyTransactions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PartyName = table.Column<string>(type: "text", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Flagged = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Reported = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelatedPartyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhistleblowerCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RefNo = table.Column<string>(type: "text", nullable: false),
                    Anonymous = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhistleblowerCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyAcks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PolicyId = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyAcks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PolicyAcks_Policies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "Policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AntiBriberyTrainings_NextDueOn",
                table: "AntiBriberyTrainings",
                column: "NextDueOn");

            migrationBuilder.CreateIndex(
                name: "IX_CaseActions_ParentType_ParentId",
                table: "CaseActions",
                columns: new[] { "ParentType", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_CoiDeclarations_EmployeeUserId_Year",
                table: "CoiDeclarations",
                columns: new[] { "EmployeeUserId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataBreaches_OdpcNotificationDueAt",
                table: "DataBreaches",
                column: "OdpcNotificationDueAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataSubjectRequests_DueBy",
                table: "DataSubjectRequests",
                column: "DueBy");

            migrationBuilder.CreateIndex(
                name: "IX_GiftHospitalities_Flagged",
                table: "GiftHospitalities",
                column: "Flagged");

            migrationBuilder.CreateIndex(
                name: "IX_PolicyAcks_PolicyId",
                table: "PolicyAcks",
                column: "PolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_RegulatoryLicences_ExpiryDate",
                table: "RegulatoryLicences",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_RelatedPartyTransactions_Reported",
                table: "RelatedPartyTransactions",
                column: "Reported");

            migrationBuilder.CreateIndex(
                name: "IX_WhistleblowerCases_RefNo",
                table: "WhistleblowerCases",
                column: "RefNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AntiBriberyTrainings");

            migrationBuilder.DropTable(
                name: "BoardResolutions");

            migrationBuilder.DropTable(
                name: "CaseActions");

            migrationBuilder.DropTable(
                name: "CoiDeclarations");

            migrationBuilder.DropTable(
                name: "DataBreaches");

            migrationBuilder.DropTable(
                name: "DataSubjectRequests");

            migrationBuilder.DropTable(
                name: "GiftHospitalities");

            migrationBuilder.DropTable(
                name: "PolicyAcks");

            migrationBuilder.DropTable(
                name: "RegulatoryLicences");

            migrationBuilder.DropTable(
                name: "RelatedPartyTransactions");

            migrationBuilder.DropTable(
                name: "WhistleblowerCases");

            migrationBuilder.DropTable(
                name: "Policies");
        }
    }
}
