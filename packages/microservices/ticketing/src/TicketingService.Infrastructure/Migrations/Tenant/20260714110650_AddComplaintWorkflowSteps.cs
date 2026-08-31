using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketingService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddComplaintWorkflowSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplaintWorkflowSteps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<string>(type: "text", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CompletedByUserId = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SatisfactionMet = table.Column<bool>(type: "boolean", nullable: true),
                    SignOffByUserId = table.Column<string>(type: "text", nullable: true),
                    SignOffAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    PreventiveAction = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintWorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplaintWorkflowSteps_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintWorkflowSteps_TicketId",
                table: "ComplaintWorkflowSteps",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplaintWorkflowSteps");
        }
    }
}
