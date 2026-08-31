using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLabWorkOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabWorkOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AssignmentId = table.Column<string>(type: "text", nullable: false),
                    ServiceRequestId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IntakeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IntakeTechnicianId = table.Column<string>(type: "text", nullable: true),
                    IntakeTechnicianName = table.Column<string>(type: "text", nullable: true),
                    IntakeConditionNotes = table.Column<string>(type: "text", nullable: true),
                    BenchTechnicianId = table.Column<string>(type: "text", nullable: true),
                    BenchTechnicianName = table.Column<string>(type: "text", nullable: true),
                    BenchStartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BenchCompletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    BenchNotes = table.Column<string>(type: "text", nullable: true),
                    JobNumber = table.Column<string>(type: "text", nullable: true),
                    CertificateNumber = table.Column<string>(type: "text", nullable: true),
                    CertificateIssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CertificateNotes = table.Column<string>(type: "text", nullable: true),
                    DispatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DispatchMethod = table.Column<string>(type: "text", nullable: true),
                    DispatchNotes = table.Column<string>(type: "text", nullable: true),
                    ReceivedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabWorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabWorkOrders_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabWorkOrders_AssignmentId",
                table: "LabWorkOrders",
                column: "AssignmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabWorkOrders");
        }
    }
}
