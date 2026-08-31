using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H10_AddDisciplineAndSeparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExitDate",
                table: "Employees",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DisciplinaryCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CaseNumber = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    IncidentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Witnesses = table.Column<string>(type: "text", nullable: true),
                    SourceModule = table.Column<string>(type: "text", nullable: true),
                    SourceReference = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ShowCauseIssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ShowCauseLetter = table.Column<string>(type: "text", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EmployeeRespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EmployeeResponse = table.Column<string>(type: "text", nullable: true),
                    ResponseOverdueAlertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HearingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HearingPanel = table.Column<string>(type: "text", nullable: true),
                    HearingNotes = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    OutcomeNotes = table.Column<string>(type: "text", nullable: true),
                    OutcomeRecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OutcomeRecordedBy = table.Column<string>(type: "text", nullable: true),
                    RightOfAppealDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AppealSubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AppealGrounds = table.Column<string>(type: "text", nullable: true),
                    AppealOutcome = table.Column<string>(type: "text", nullable: true),
                    AppealDecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true),
                    WarningRecordId = table.Column<string>(type: "text", nullable: true),
                    SeparationId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisciplinaryCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisciplinaryCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GrievanceCases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CaseNumber = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AcknowledgementDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "text", nullable: true),
                    SlaBreachAlertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AssignedToEmployeeId = table.Column<string>(type: "text", nullable: true),
                    AssignedToName = table.Column<string>(type: "text", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InvestigationFindings = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    FollowUpActions = table.Column<string>(type: "text", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrievanceCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrievanceCases_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Separations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SeparationNumber = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    HireDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SeparationType = table.Column<int>(type: "integer", nullable: false),
                    NoticeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "integer", nullable: false),
                    NoticeWaived = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    SourceModule = table.Column<string>(type: "text", nullable: true),
                    SourceReference = table.Column<string>(type: "text", nullable: true),
                    MonthlySalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DailyRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LeaveDaysBalance = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    LeavePayout = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalMonthDaysWorked = table.Column<int>(type: "integer", nullable: false),
                    ProRataSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NoticePay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherEarnings = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceRecovery = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetDues = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    DuesNotes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaidBy = table.Column<string>(type: "text", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaymentReference = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CertificateIssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExitInterviewNotes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Separations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Separations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WarningRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DisciplinaryCaseId = table.Column<string>(type: "text", nullable: true),
                    WarningType = table.Column<int>(type: "integer", nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcknowledgedByEmployee = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcknowledgementNote = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarningRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_CaseNumber",
                table: "DisciplinaryCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_EmployeeId",
                table: "DisciplinaryCases",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_IncidentDate",
                table: "DisciplinaryCases",
                column: "IncidentDate");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_ResponseDeadline",
                table: "DisciplinaryCases",
                column: "ResponseDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_DisciplinaryCases_Status",
                table: "DisciplinaryCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GrievanceCases_AcknowledgementDeadline",
                table: "GrievanceCases",
                column: "AcknowledgementDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_GrievanceCases_CaseNumber",
                table: "GrievanceCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GrievanceCases_EmployeeId",
                table: "GrievanceCases",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_GrievanceCases_Status",
                table: "GrievanceCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Separations_EffectiveDate",
                table: "Separations",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_Separations_OneLivePerEmployee",
                table: "Separations",
                column: "EmployeeId",
                unique: true,
                filter: "\"Status\" <> 4 AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Separations_SeparationNumber",
                table: "Separations",
                column: "SeparationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Separations_Status",
                table: "Separations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_DisciplinaryCaseId",
                table: "WarningRecords",
                column: "DisciplinaryCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_EmployeeId_IssuedDate",
                table: "WarningRecords",
                columns: new[] { "EmployeeId", "IssuedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_ExpiryDate",
                table: "WarningRecords",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_IsActive",
                table: "WarningRecords",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisciplinaryCases");

            migrationBuilder.DropTable(
                name: "GrievanceCases");

            migrationBuilder.DropTable(
                name: "Separations");

            migrationBuilder.DropTable(
                name: "WarningRecords");

            migrationBuilder.DropColumn(
                name: "ExitDate",
                table: "Employees");
        }
    }
}
