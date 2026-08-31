using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PR3_RaidAndChangeControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskEntries_Assignments_AssignmentId",
                table: "RiskEntries");

            // PR3 — the risk scales become ordered enums. Postgres will not cast text to integer on
            // its own, and EF's scaffolded AlterColumn emits no USING clause, so these three are
            // hand-written. The CASE mapping preserves existing rows rather than dropping them; any
            // value outside the documented Low|Medium|High vocabulary falls back to the lowest rung,
            // which is the only safe default when we cannot tell what was meant.
            migrationBuilder.Sql(@"
                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Likelihood"" TYPE integer
                USING CASE lower(""Likelihood"") WHEN 'high' THEN 3 WHEN 'medium' THEN 2 ELSE 1 END;
                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Likelihood"" SET DEFAULT 1;

                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Impact"" TYPE integer
                USING CASE lower(""Impact"") WHEN 'high' THEN 3 WHEN 'medium' THEN 2 ELSE 1 END;
                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Impact"" SET DEFAULT 1;

                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Status"" TYPE integer
                USING CASE lower(""Status"")
                        WHEN 'mitigating' THEN 1 WHEN 'mitigated' THEN 2
                        WHEN 'closed' THEN 3 WHEN 'realised' THEN 4 ELSE 0 END;
                ALTER TABLE ""RiskEntries"" ALTER COLUMN ""Status"" SET DEFAULT 0;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "AssignmentId",
                table: "RiskEntries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "RiskEntries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "RiskEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectId",
                table: "RiskEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RealisedAsIssueId",
                table: "RiskEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDate",
                table: "RiskEntries",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "RiskEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "RiskEntries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    ScheduleImpactDays = table.Column<int>(type: "integer", nullable: false),
                    BudgetVersionId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedBy = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "text", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "text", nullable: true),
                    PreviousBaselineBudget = table.Column<decimal>(type: "numeric", nullable: true),
                    PreviousBaselineSetAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PreviousMilestoneBaselines = table.Column<string>(type: "text", nullable: true),
                    NewBaselineBudget = table.Column<decimal>(type: "numeric", nullable: true),
                    MilestonesShifted = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectIssues",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    MilestoneId = table.Column<string>(type: "text", nullable: true),
                    Number = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RaisedBy = table.Column<string>(type: "text", nullable: true),
                    TargetResolutionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedBy = table.Column<string>(type: "text", nullable: true),
                    Resolution = table.Column<string>(type: "text", nullable: true),
                    RaisedFromRiskId = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectIssues_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskEntries_ProjectId",
                table: "RiskEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_ProjectId",
                table: "ChangeRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectIssues_ProjectId",
                table: "ProjectIssues",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskEntries_Assignments_AssignmentId",
                table: "RiskEntries",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_RiskEntries_Projects_ProjectId",
                table: "RiskEntries",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");

            // Existing rows predate Title and Score. Title falls back to the description they were
            // created with (the old register had no title field at all), and Score is derived so the
            // register can be ranked immediately instead of only after each row is next edited.
            migrationBuilder.Sql(@"
                UPDATE ""RiskEntries"" SET ""Title"" = left(""Description"", 200)
                WHERE ""Title"" = '' AND ""Description"" <> '';
                UPDATE ""RiskEntries"" SET ""Score"" = ""Likelihood"" * ""Impact"" WHERE ""Score"" = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskEntries_Assignments_AssignmentId",
                table: "RiskEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_RiskEntries_Projects_ProjectId",
                table: "RiskEntries");

            migrationBuilder.DropTable(
                name: "ChangeRequests");

            migrationBuilder.DropTable(
                name: "ProjectIssues");

            migrationBuilder.DropIndex(
                name: "IX_RiskEntries_ProjectId",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "RealisedAsIssueId",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "ReviewDate",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "RiskEntries");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "RiskEntries");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RiskEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Likelihood",
                table: "RiskEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Impact",
                table: "RiskEntries",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "AssignmentId",
                table: "RiskEntries",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RiskEntries_Assignments_AssignmentId",
                table: "RiskEntries",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
