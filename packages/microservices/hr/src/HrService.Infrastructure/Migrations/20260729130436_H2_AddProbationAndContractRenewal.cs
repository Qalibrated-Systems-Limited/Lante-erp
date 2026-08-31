using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class H2_AddProbationAndContractRenewal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractRenewalAlerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    ContractEndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Alert30SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Alert7SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExpiredAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ActionedBy = table.Column<string>(type: "text", nullable: true),
                    ActionedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_ContractRenewalAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractRenewalAlerts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProbationReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    ReviewType = table.Column<int>(type: "integer", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ReviewerId = table.Column<string>(type: "text", nullable: true),
                    ReviewerName = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ExtendedToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FollowUpReviewId = table.Column<string>(type: "text", nullable: true),
                    AlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbationReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationReviews_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalAlerts_ContractEndDate",
                table: "ContractRenewalAlerts",
                column: "ContractEndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalAlerts_EmployeeId_ContractEndDate",
                table: "ContractRenewalAlerts",
                columns: new[] { "EmployeeId", "ContractEndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractRenewalAlerts_Outcome",
                table: "ContractRenewalAlerts",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationReviews_EmployeeId_ReviewType_ScheduledDate",
                table: "ProbationReviews",
                columns: new[] { "EmployeeId", "ReviewType", "ScheduledDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProbationReviews_Outcome",
                table: "ProbationReviews",
                column: "Outcome");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationReviews_ScheduledDate",
                table: "ProbationReviews",
                column: "ScheduledDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractRenewalAlerts");

            migrationBuilder.DropTable(
                name: "ProbationReviews");
        }
    }
}
