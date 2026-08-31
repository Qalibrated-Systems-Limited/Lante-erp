using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoverAndNegligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverrunNoticedAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NegligenceIncidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IncidentNumber = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: true),
                    AssignmentId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReportedBy = table.Column<string>(type: "text", nullable: false),
                    LoggedLate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EscalatedToMdAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResponseBreachAlertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsRepeatOffense = table.Column<bool>(type: "boolean", nullable: false),
                    FinalWarningIssued = table.Column<bool>(type: "boolean", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NegligenceIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramOverrunNotices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    ExpectedEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DaysOverrun = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramOverrunNotices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHandovers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    HandoverNumber = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StepsJson = table.Column<string>(type: "text", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: true),
                    ClientRepName = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "text", nullable: true),
                    IsPermanent = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHandovers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NegligenceResponses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IncidentId = table.Column<string>(type: "text", nullable: false),
                    ResponderId = table.Column<string>(type: "text", nullable: false),
                    ResponderName = table.Column<string>(type: "text", nullable: false),
                    ResponderRole = table.Column<string>(type: "text", nullable: false),
                    ResponseText = table.Column<string>(type: "text", nullable: false),
                    ActionTaken = table.Column<string>(type: "text", nullable: true),
                    PayrollDeductionAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NegligenceResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NegligenceResponses_NegligenceIncidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "NegligenceIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectHandoverSignatures",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    HandoverId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    SignatoryName = table.Column<string>(type: "text", nullable: false),
                    SignatureData = table.Column<string>(type: "text", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectHandoverSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectHandoverSignatures_ProjectHandovers_HandoverId",
                        column: x => x.HandoverId,
                        principalTable: "ProjectHandovers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NegligenceIncidents_EmployeeId",
                table: "NegligenceIncidents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_NegligenceIncidents_IncidentNumber",
                table: "NegligenceIncidents",
                column: "IncidentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NegligenceResponses_IncidentId",
                table: "NegligenceResponses",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramOverrunNotices_ProjectId",
                table: "ProgramOverrunNotices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandovers_HandoverNumber",
                table: "ProjectHandovers",
                column: "HandoverNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandovers_ProjectId",
                table: "ProjectHandovers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectHandoverSignatures_HandoverId",
                table: "ProjectHandoverSignatures",
                column: "HandoverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NegligenceResponses");

            migrationBuilder.DropTable(
                name: "ProgramOverrunNotices");

            migrationBuilder.DropTable(
                name: "ProjectHandoverSignatures");

            migrationBuilder.DropTable(
                name: "NegligenceIncidents");

            migrationBuilder.DropTable(
                name: "ProjectHandovers");

            migrationBuilder.DropColumn(
                name: "OverrunNoticedAt",
                table: "Projects");
        }
    }
}
