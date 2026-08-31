using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H12_AddRecruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobRequisitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RequisitionNumber = table.Column<string>(type: "text", nullable: false),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    JobGrade = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<string>(type: "text", nullable: true),
                    RequisitionType = table.Column<int>(type: "integer", nullable: false),
                    HeadcountRequested = table.Column<int>(type: "integer", nullable: false),
                    ReplacingEmployeeId = table.Column<string>(type: "text", nullable: true),
                    ReplacingEmployeeName = table.Column<string>(type: "text", nullable: true),
                    EmploymentType = table.Column<int>(type: "integer", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Justification = table.Column<string>(type: "text", nullable: true),
                    RequiredBy = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedHeadcount = table.Column<int>(type: "integer", nullable: true),
                    CurrentHeadcount = table.Column<int>(type: "integer", nullable: false),
                    CommittedHeadcount = table.Column<int>(type: "integer", nullable: false),
                    EstablishmentNotes = table.Column<string>(type: "text", nullable: true),
                    ExceedsEstablishment = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RaisedBy = table.Column<string>(type: "text", nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "text", nullable: true),
                    DecidedByName = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "text", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VacancyNumber = table.Column<string>(type: "text", nullable: false),
                    JobRequisitionId = table.Column<string>(type: "text", nullable: false),
                    RequisitionNumber = table.Column<string>(type: "text", nullable: true),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    JobGrade = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<string>(type: "text", nullable: true),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    HiredCount = table.Column<int>(type: "integer", nullable: false),
                    PostingChannel = table.Column<int>(type: "integer", nullable: false),
                    JobDescription = table.Column<string>(type: "text", nullable: true),
                    MinimumQualifications = table.Column<string>(type: "text", nullable: true),
                    Responsibilities = table.Column<string>(type: "text", nullable: true),
                    SalaryRangeMin = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryRangeMax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true),
                    ClosureReason = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacancies_JobRequisitions_JobRequisitionId",
                        column: x => x.JobRequisitionId,
                        principalTable: "JobRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Applicants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicantNumber = table.Column<string>(type: "text", nullable: false),
                    VacancyId = table.Column<string>(type: "text", nullable: false),
                    VacancyNumber = table.Column<string>(type: "text", nullable: true),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    NationalId = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    ReferredByEmployeeId = table.Column<string>(type: "text", nullable: true),
                    ReferredByName = table.Column<string>(type: "text", nullable: true),
                    InternalEmployeeId = table.Column<string>(type: "text", nullable: true),
                    CvDocumentPath = table.Column<string>(type: "text", nullable: true),
                    CoverNote = table.Column<string>(type: "text", nullable: true),
                    YearsExperience = table.Column<int>(type: "integer", nullable: true),
                    HighestQualification = table.Column<string>(type: "text", nullable: true),
                    CurrentEmployer = table.Column<string>(type: "text", nullable: true),
                    ExpectedSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ScreenedBy = table.Column<string>(type: "text", nullable: true),
                    ScreenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScreeningNotes = table.Column<string>(type: "text", nullable: true),
                    AverageInterviewScore = table.Column<decimal>(type: "numeric", nullable: true),
                    InterviewsHeld = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    RejectedAtStage = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResultingEmployeeId = table.Column<string>(type: "text", nullable: true),
                    ResultingEmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    HiredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applicants_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Interviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicantId = table.Column<string>(type: "text", nullable: false),
                    ApplicantName = table.Column<string>(type: "text", nullable: true),
                    VacancyId = table.Column<string>(type: "text", nullable: false),
                    Stage = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    PanelMembers = table.Column<string>(type: "text", nullable: true),
                    Held = table.Column<bool>(type: "boolean", nullable: false),
                    HeldAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TechnicalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ExperienceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CommunicationScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CulturalFitScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Recommendation = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ScoredBy = table.Column<string>(type: "text", nullable: true),
                    ScoredByName = table.Column<string>(type: "text", nullable: true),
                    ScoredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interviews_Applicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Applicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobOffers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OfferNumber = table.Column<string>(type: "text", nullable: false),
                    ApplicantId = table.Column<string>(type: "text", nullable: false),
                    ApplicantName = table.Column<string>(type: "text", nullable: true),
                    VacancyId = table.Column<string>(type: "text", nullable: false),
                    VacancyNumber = table.Column<string>(type: "text", nullable: true),
                    PositionId = table.Column<string>(type: "text", nullable: false),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    JobGrade = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    BranchId = table.Column<string>(type: "text", nullable: true),
                    OfferedSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    SalaryStructureId = table.Column<string>(type: "text", nullable: true),
                    EmploymentType = table.Column<int>(type: "integer", nullable: false),
                    ProposedStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ProbationMonths = table.Column<int>(type: "integer", nullable: false),
                    Terms = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreparedBy = table.Column<string>(type: "text", nullable: true),
                    PreparedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedByName = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IssuedBy = table.Column<string>(type: "text", nullable: true),
                    ResponseDays = table.Column<int>(type: "integer", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LapseAlertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeclineReason = table.Column<string>(type: "text", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "text", nullable: true),
                    ResultingEmployeeId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobOffers_Applicants_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Applicants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobOffers_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_ApplicantNumber",
                table: "Applicants",
                column: "ApplicantNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_InternalEmployeeId",
                table: "Applicants",
                column: "InternalEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_OnePerEmailPerVacancy",
                table: "Applicants",
                columns: new[] { "VacancyId", "Email" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_Source",
                table: "Applicants",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_Status",
                table: "Applicants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_VacancyId",
                table: "Applicants",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ApplicantId",
                table: "Interviews",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ScheduledAt",
                table: "Interviews",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_VacancyId",
                table: "Interviews",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_OfferNumber",
                table: "JobOffers",
                column: "OfferNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_OneLivePerApplicant",
                table: "JobOffers",
                column: "ApplicantId",
                unique: true,
                filter: "\"Status\" NOT IN (5, 6, 7) AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_ResponseDeadline",
                table: "JobOffers",
                column: "ResponseDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_Status",
                table: "JobOffers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_VacancyId",
                table: "JobOffers",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_DepartmentId",
                table: "JobRequisitions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_PositionId_Status",
                table: "JobRequisitions",
                columns: new[] { "PositionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_RequisitionNumber",
                table: "JobRequisitions",
                column: "RequisitionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_Status",
                table: "JobRequisitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_ClosingDate",
                table: "Vacancies",
                column: "ClosingDate");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DepartmentId",
                table: "Vacancies",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_JobRequisitionId",
                table: "Vacancies",
                column: "JobRequisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_PositionId",
                table: "Vacancies",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status",
                table: "Vacancies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_VacancyNumber",
                table: "Vacancies",
                column: "VacancyNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Interviews");

            migrationBuilder.DropTable(
                name: "JobOffers");

            migrationBuilder.DropTable(
                name: "Applicants");

            migrationBuilder.DropTable(
                name: "Vacancies");

            migrationBuilder.DropTable(
                name: "JobRequisitions");
        }
    }
}
