using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H7_AddLearningDevelopment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnnualTrainingHoursTarget",
                table: "Positions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnowledgeSharingSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Topic = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    FacilitatorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    FacilitatorName = table.Column<string>(type: "text", nullable: true),
                    SessionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    TrainingEventId = table.Column<string>(type: "text", nullable: true),
                    AttendeeCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeSharingSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LdBudgets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: false),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    BudgetedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualSpend = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Warning80SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Exhausted100SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LdBudgets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningDevelopmentPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    PlanYear = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EscalatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningDevelopmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningDevelopmentPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MandatoryTrainingRequirements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EvidenceSource = table.Column<int>(type: "integer", nullable: false),
                    ValidityMonths = table.Column<int>(type: "integer", nullable: true),
                    BlocksIncrement = table.Column<bool>(type: "boolean", nullable: false),
                    GraceDays = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandatoryTrainingRequirements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    TrainingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DurationHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    MandatoryTrainingCode = table.Column<string>(type: "text", nullable: true),
                    CertificateUrl = table.Column<string>(type: "text", nullable: true),
                    KnowledgeSharingSessionId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeSharingAttendance",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    KnowledgeSharingSessionId = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    AttendedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeSharingAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeSharingAttendance_KnowledgeSharingSessions_Knowled~",
                        column: x => x.KnowledgeSharingSessionId,
                        principalTable: "KnowledgeSharingSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LdpObjectives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    LearningDevelopmentPlanId = table.Column<string>(type: "text", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: false),
                    Activity = table.Column<string>(type: "text", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedByTrainingId = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LdpObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LdpObjectives_LearningDevelopmentPlans_LearningDevelopmentP~",
                        column: x => x.LearningDevelopmentPlanId,
                        principalTable: "LearningDevelopmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrainingHoursLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    TrainingEventId = table.Column<string>(type: "text", nullable: false),
                    TrainingTitle = table.Column<string>(type: "text", nullable: true),
                    TrainingDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    VerifiedBy = table.Column<string>(type: "text", nullable: true),
                    LoggedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LdpObjectiveId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingHoursLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrainingHoursLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingHoursLogs_TrainingEvents_TrainingEventId",
                        column: x => x.TrainingEventId,
                        principalTable: "TrainingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeSharingAttendance_EmployeeId",
                table: "KnowledgeSharingAttendance",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeSharingAttendance_KnowledgeSharingSessionId_Employ~",
                table: "KnowledgeSharingAttendance",
                columns: new[] { "KnowledgeSharingSessionId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeSharingSessions_FacilitatorEmployeeId",
                table: "KnowledgeSharingSessions",
                column: "FacilitatorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeSharingSessions_SessionDate",
                table: "KnowledgeSharingSessions",
                column: "SessionDate");

            migrationBuilder.CreateIndex(
                name: "IX_LdBudgets_DepartmentId_Year",
                table: "LdBudgets",
                columns: new[] { "DepartmentId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LdBudgets_Year",
                table: "LdBudgets",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_LdpObjectives_LearningDevelopmentPlanId",
                table: "LdpObjectives",
                column: "LearningDevelopmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_LdpObjectives_Status",
                table: "LdpObjectives",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LearningDevelopmentPlans_EmployeeId_PlanYear",
                table: "LearningDevelopmentPlans",
                columns: new[] { "EmployeeId", "PlanYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningDevelopmentPlans_PlanYear",
                table: "LearningDevelopmentPlans",
                column: "PlanYear");

            migrationBuilder.CreateIndex(
                name: "IX_LearningDevelopmentPlans_Status",
                table: "LearningDevelopmentPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryTrainingRequirements_Code",
                table: "MandatoryTrainingRequirements",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryTrainingRequirements_IsActive",
                table: "MandatoryTrainingRequirements",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_DepartmentId",
                table: "TrainingEvents",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_KnowledgeSharingSessionId",
                table: "TrainingEvents",
                column: "KnowledgeSharingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_MandatoryTrainingCode",
                table: "TrainingEvents",
                column: "MandatoryTrainingCode");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingEvents_TrainingDate",
                table: "TrainingEvents",
                column: "TrainingDate");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingHoursLogs_EmployeeId_TrainingDate",
                table: "TrainingHoursLogs",
                columns: new[] { "EmployeeId", "TrainingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TrainingHoursLogs_LdpObjectiveId",
                table: "TrainingHoursLogs",
                column: "LdpObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingHoursLogs_TrainingEventId_EmployeeId",
                table: "TrainingHoursLogs",
                columns: new[] { "TrainingEventId", "EmployeeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeSharingAttendance");

            migrationBuilder.DropTable(
                name: "LdBudgets");

            migrationBuilder.DropTable(
                name: "LdpObjectives");

            migrationBuilder.DropTable(
                name: "MandatoryTrainingRequirements");

            migrationBuilder.DropTable(
                name: "TrainingHoursLogs");

            migrationBuilder.DropTable(
                name: "KnowledgeSharingSessions");

            migrationBuilder.DropTable(
                name: "LearningDevelopmentPlans");

            migrationBuilder.DropTable(
                name: "TrainingEvents");

            migrationBuilder.DropColumn(
                name: "AnnualTrainingHoursTarget",
                table: "Positions");
        }
    }
}
