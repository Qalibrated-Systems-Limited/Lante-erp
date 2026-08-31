using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H9_AddKpiAndAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppraisalCycles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CycleType = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OpenedBy = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    AppraisalCount = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Appraisals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AppraisalCycleId = table.Column<string>(type: "text", nullable: false),
                    CycleName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    KpiScorecardId = table.Column<string>(type: "text", nullable: true),
                    ScorecardName = table.Column<string>(type: "text", nullable: true),
                    PipThreshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SelfAssessmentSubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SelfAssessmentBy = table.Column<string>(type: "text", nullable: true),
                    LineManagerReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LineManagerBy = table.Column<string>(type: "text", nullable: true),
                    MdSignedOffAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MdSignedOffBy = table.Column<string>(type: "text", nullable: true),
                    HrRecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    HrRecordedBy = table.Column<string>(type: "text", nullable: true),
                    SelfScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    LineManagerScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Feedback360Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    SelfComments = table.Column<string>(type: "text", nullable: true),
                    LineManagerComments = table.Column<string>(type: "text", nullable: true),
                    MdComments = table.Column<string>(type: "text", nullable: true),
                    HrComments = table.Column<string>(type: "text", nullable: true),
                    TrainingNeeds = table.Column<string>(type: "text", nullable: true),
                    PipTriggered = table.Column<bool>(type: "boolean", nullable: false),
                    PipId = table.Column<string>(type: "text", nullable: true),
                    ParentId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appraisals_Appraisals_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Appraisals",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Feedback360s",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AppraisalId = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    ReviewerEmployeeId = table.Column<string>(type: "text", nullable: false),
                    ReviewerName = table.Column<string>(type: "text", nullable: true),
                    ReviewerType = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    OverallScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ScoresJson = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedback360s", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KpiScorecards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PositionId = table.Column<string>(type: "text", nullable: true),
                    PositionTitle = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    PipThreshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_KpiScorecards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KpiTargets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    KpiScorecardId = table.Column<string>(type: "text", nullable: false),
                    KpiScorecardItemId = table.Column<string>(type: "text", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    TargetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualSource = table.Column<int>(type: "integer", nullable: false),
                    ActualUpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_KpiTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiTargets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceImprovementPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    AppraisalId = table.Column<string>(type: "text", nullable: true),
                    TriggerScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Threshold = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Objectives = table.Column<string>(type: "text", nullable: true),
                    SupportProvided = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SecondReviewDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedBy = table.Column<string>(type: "text", nullable: true),
                    LinkedLdpObjectiveId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceImprovementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PerformanceImprovementPlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppraisalItemScores",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AppraisalId = table.Column<string>(type: "text", nullable: false),
                    KpiTargetId = table.Column<string>(type: "text", nullable: true),
                    KpiScorecardItemId = table.Column<string>(type: "text", nullable: true),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    WeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MeasurementType = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    TargetValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ActualValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SelfScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ManagerScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_AppraisalItemScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppraisalItemScores_Appraisals_AppraisalId",
                        column: x => x.AppraisalId,
                        principalTable: "Appraisals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KpiScorecardItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    KpiScorecardId = table.Column<string>(type: "text", nullable: false),
                    ItemName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    WeightPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    MeasurementType = table.Column<int>(type: "integer", nullable: false),
                    TargetSource = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_KpiScorecardItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KpiScorecardItems_KpiScorecards_KpiScorecardId",
                        column: x => x.KpiScorecardId,
                        principalTable: "KpiScorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_Status",
                table: "AppraisalCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalCycles_Year_CycleType",
                table: "AppraisalCycles",
                columns: new[] { "Year", "CycleType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalItemScores_AppraisalId_DisplayOrder",
                table: "AppraisalItemScores",
                columns: new[] { "AppraisalId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_AppraisalCycleId_EmployeeId",
                table: "Appraisals",
                columns: new[] { "AppraisalCycleId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_EmployeeId",
                table: "Appraisals",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_ParentId",
                table: "Appraisals",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Appraisals_Status",
                table: "Appraisals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Feedback360s_AppraisalId_ReviewerEmployeeId",
                table: "Feedback360s",
                columns: new[] { "AppraisalId", "ReviewerEmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedback360s_EmployeeId",
                table: "Feedback360s",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiScorecardItems_KpiScorecardId",
                table: "KpiScorecardItems",
                column: "KpiScorecardId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiScorecards_PositionId_Year",
                table: "KpiScorecards",
                columns: new[] { "PositionId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiScorecards_Year",
                table: "KpiScorecards",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_KpiTargets_EmployeeId_KpiScorecardItemId_Year",
                table: "KpiTargets",
                columns: new[] { "EmployeeId", "KpiScorecardItemId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiTargets_EmployeeId_Year",
                table: "KpiTargets",
                columns: new[] { "EmployeeId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_AppraisalId",
                table: "PerformanceImprovementPlans",
                column: "AppraisalId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_EmployeeId",
                table: "PerformanceImprovementPlans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceImprovementPlans_Status",
                table: "PerformanceImprovementPlans",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalCycles");

            migrationBuilder.DropTable(
                name: "AppraisalItemScores");

            migrationBuilder.DropTable(
                name: "Feedback360s");

            migrationBuilder.DropTable(
                name: "KpiScorecardItems");

            migrationBuilder.DropTable(
                name: "KpiTargets");

            migrationBuilder.DropTable(
                name: "PerformanceImprovementPlans");

            migrationBuilder.DropTable(
                name: "Appraisals");

            migrationBuilder.DropTable(
                name: "KpiScorecards");
        }
    }
}
