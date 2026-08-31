using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PR1_ScheduleBaselineAndDetailedBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedHours",
                table: "ProjectTasks",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentTaskId",
                table: "ProjectTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "ProjectTasks",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaselineBudget",
                table: "Projects",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineSetAt",
                table: "Projects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractAttachmentId",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineDue",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineStart",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Milestones",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetVersionId",
                table: "BudgetLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContractRateId",
                table: "BudgetLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "BudgetLines",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAmount",
                table: "BudgetLines",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Unit",
                table: "BudgetLines",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCostRate",
                table: "BudgetLines",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BudgetVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RevisionReason = table.Column<string>(type: "text", nullable: true),
                    TotalPlanned = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalQuoted = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SubmittedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "text", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    SupersededAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetVersions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractRates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    ClientRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CostRate = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_ContractRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MilestoneDependencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    PredecessorMilestoneId = table.Column<string>(type: "text", nullable: false),
                    SuccessorMilestoneId = table.Column<string>(type: "text", nullable: false),
                    LagDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilestoneDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilestoneDependencies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskDependencies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    PredecessorTaskId = table.Column<string>(type: "text", nullable: false),
                    SuccessorTaskId = table.Column<string>(type: "text", nullable: false),
                    LagDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskDependencies_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ParentTaskId",
                table: "ProjectTasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetVersionId",
                table: "BudgetLines",
                column: "BudgetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ContractRateId",
                table: "BudgetLines",
                column: "ContractRateId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetVersions_ProjectId_VersionNo",
                table: "BudgetVersions",
                columns: new[] { "ProjectId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRates_ProjectId",
                table: "ContractRates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneDependencies_PredecessorMilestoneId_SuccessorMiles~",
                table: "MilestoneDependencies",
                columns: new[] { "PredecessorMilestoneId", "SuccessorMilestoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MilestoneDependencies_ProjectId",
                table: "MilestoneDependencies",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_PredecessorTaskId_SuccessorTaskId",
                table: "TaskDependencies",
                columns: new[] { "PredecessorTaskId", "SuccessorTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskDependencies_ProjectId",
                table: "TaskDependencies",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLines_BudgetVersions_BudgetVersionId",
                table: "BudgetLines",
                column: "BudgetVersionId",
                principalTable: "BudgetVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BudgetLines_ContractRates_ContractRateId",
                table: "BudgetLines",
                column: "ContractRateId",
                principalTable: "ContractRates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTasks_ProjectTasks_ParentTaskId",
                table: "ProjectTasks",
                column: "ParentTaskId",
                principalTable: "ProjectTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLines_BudgetVersions_BudgetVersionId",
                table: "BudgetLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BudgetLines_ContractRates_ContractRateId",
                table: "BudgetLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTasks_ProjectTasks_ParentTaskId",
                table: "ProjectTasks");

            migrationBuilder.DropTable(
                name: "BudgetVersions");

            migrationBuilder.DropTable(
                name: "ContractRates");

            migrationBuilder.DropTable(
                name: "MilestoneDependencies");

            migrationBuilder.DropTable(
                name: "TaskDependencies");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTasks_ParentTaskId",
                table: "ProjectTasks");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLines_BudgetVersionId",
                table: "BudgetLines");

            migrationBuilder.DropIndex(
                name: "IX_BudgetLines_ContractRateId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "EstimatedHours",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "ProjectTasks");

            migrationBuilder.DropColumn(
                name: "BaselineBudget",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BaselineSetAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ContractAttachmentId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BaselineDue",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "BaselineStart",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "BudgetVersionId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "ContractRateId",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "QuotedAmount",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "BudgetLines");

            migrationBuilder.DropColumn(
                name: "UnitCostRate",
                table: "BudgetLines");
        }
    }
}
