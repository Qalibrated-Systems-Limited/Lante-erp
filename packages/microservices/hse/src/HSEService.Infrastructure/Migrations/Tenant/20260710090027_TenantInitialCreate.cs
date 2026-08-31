using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HSEService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class TenantInitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HseIncidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    SiteName = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ReportedByUserId = table.Column<string>(type: "text", nullable: false),
                    ReportedByName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_HseIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HseTrainingRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    Course = table.Column<string>(type: "text", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
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
                    table.PrimaryKey("PK_HseTrainingRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PpeIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    Item = table.Column<string>(type: "text", nullable: false),
                    Condition = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    ReplacementDueAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PpeIssues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RamsRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    SiteName = table.Column<string>(type: "text", nullable: true),
                    SubcontractorId = table.Column<string>(type: "text", nullable: true),
                    SubcontractorName = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileUrl = table.Column<string>(type: "text", nullable: true),
                    UploadedByUserId = table.Column<string>(type: "text", nullable: true),
                    IssueNotes = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RamsRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<string>(type: "text", nullable: true),
                    ProjectName = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Sites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutoryInspections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    SiteName = table.Column<string>(type: "text", nullable: true),
                    Equipment = table.Column<string>(type: "text", nullable: false),
                    InspectorName = table.Column<string>(type: "text", nullable: true),
                    LastInspectedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_StatutoryInspections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subcontractors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SafetyScore = table.Column<decimal>(type: "numeric", nullable: false),
                    RamsSubmitted = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    Prequalified = table.Column<bool>(type: "BOOLEAN", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "ToolboxTalks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SiteId = table.Column<string>(type: "text", nullable: false),
                    SiteName = table.Column<string>(type: "text", nullable: true),
                    SupervisorUserId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: true),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    HeldOn = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolboxTalks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IncidentId = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: true),
                    OwnerName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveActions_HseIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "HseIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvIncidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IncidentId = table.Column<string>(type: "text", nullable: false),
                    NemaRef = table.Column<string>(type: "text", nullable: true),
                    NemaNotificationRequired = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvIncidents_HseIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "HseIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ToolboxAttendees",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TalkId = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_ToolboxAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolboxAttendees_ToolboxTalks_TalkId",
                        column: x => x.TalkId,
                        principalTable: "ToolboxTalks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActions_IncidentId",
                table: "CorrectiveActions",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvIncidents_IncidentId",
                table: "EnvIncidents",
                column: "IncidentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HseIncidents_OccurredAt",
                table: "HseIncidents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_HseIncidents_SiteId",
                table: "HseIncidents",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_HseTrainingRecords_ExpiresOn",
                table: "HseTrainingRecords",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_RamsRecords_SiteId_Title_Version",
                table: "RamsRecords",
                columns: new[] { "SiteId", "Title", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatutoryInspections_DueDate",
                table: "StatutoryInspections",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxAttendees_TalkId",
                table: "ToolboxAttendees",
                column: "TalkId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolboxTalks_SiteId",
                table: "ToolboxTalks",
                column: "SiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectiveActions");

            migrationBuilder.DropTable(
                name: "EnvIncidents");

            migrationBuilder.DropTable(
                name: "HseTrainingRecords");

            migrationBuilder.DropTable(
                name: "PpeIssues");

            migrationBuilder.DropTable(
                name: "RamsRecords");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "StatutoryInspections");

            migrationBuilder.DropTable(
                name: "Subcontractors");

            migrationBuilder.DropTable(
                name: "ToolboxAttendees");

            migrationBuilder.DropTable(
                name: "HseIncidents");

            migrationBuilder.DropTable(
                name: "ToolboxTalks");
        }
    }
}
