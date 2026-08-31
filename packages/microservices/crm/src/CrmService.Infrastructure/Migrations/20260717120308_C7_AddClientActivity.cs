using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class C7_AddClientActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DormantSince",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInteractionAt",
                table: "Customers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: true),
                    OpportunityId = table.Column<string>(type: "text", nullable: true),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    TaskType = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsAutoCreated = table.Column<bool>(type: "boolean", nullable: false),
                    OverdueAlertedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientVisits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    VisitDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    NextAction = table.Column<string>(type: "text", nullable: true),
                    GpsLat = table.Column<double>(type: "double precision", nullable: true),
                    GpsLng = table.Column<double>(type: "double precision", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientVisits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerInteractions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    ContactId = table.Column<string>(type: "text", nullable: true),
                    InteractionType = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "text", nullable: true),
                    PerformedBy = table.Column<string>(type: "text", nullable: false),
                    InteractionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NextActionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SalesActivityLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    LogDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CallsMade = table.Column<int>(type: "integer", nullable: false),
                    MeetingsHeld = table.Column<int>(type: "integer", nullable: false),
                    ProposalsSent = table.Column<int>(type: "integer", nullable: false),
                    Visits = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_SalesActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VisitTargets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    MinVisitsPerWeek = table.Column<int>(type: "integer", nullable: false),
                    MinVisitsPerMonth = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_AssignedTo",
                table: "ActivityTasks",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_CustomerId",
                table: "ActivityTasks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_DueDate",
                table: "ActivityTasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityTasks_Status",
                table: "ActivityTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClientVisits_CustomerId",
                table: "ClientVisits",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientVisits_EmployeeId",
                table: "ClientVisits",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_CustomerId",
                table: "CustomerInteractions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInteractions_InteractionDate",
                table: "CustomerInteractions",
                column: "InteractionDate");

            migrationBuilder.CreateIndex(
                name: "IX_SalesActivityLogs_EmployeeId_LogDate",
                table: "SalesActivityLogs",
                columns: new[] { "EmployeeId", "LogDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VisitTargets_EmployeeId",
                table: "VisitTargets",
                column: "EmployeeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityTasks");

            migrationBuilder.DropTable(
                name: "ClientVisits");

            migrationBuilder.DropTable(
                name: "CustomerInteractions");

            migrationBuilder.DropTable(
                name: "SalesActivityLogs");

            migrationBuilder.DropTable(
                name: "VisitTargets");

            migrationBuilder.DropColumn(
                name: "DormantSince",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastInteractionAt",
                table: "Customers");
        }
    }
}
