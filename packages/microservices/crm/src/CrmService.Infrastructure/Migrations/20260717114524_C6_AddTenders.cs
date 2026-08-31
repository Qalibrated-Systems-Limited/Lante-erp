using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class C6_AddTenders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenderNumber = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<string>(type: "text", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EstimatedValue = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SubmissionDeadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: false),
                    AssignedToName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OutcomeNotes = table.Column<string>(type: "text", nullable: true),
                    LostReason = table.Column<string>(type: "text", nullable: true),
                    LinkedOpportunityId = table.Column<string>(type: "text", nullable: true),
                    Alert14SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Alert7SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Alert3SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Alert1SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenderBidBonds",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenderId = table.Column<string>(type: "text", nullable: false),
                    GuaranteeNumber = table.Column<string>(type: "text", nullable: false),
                    IssuingBank = table.Column<string>(type: "text", nullable: false),
                    ValidityDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiryAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderBidBonds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderBidBonds_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidBonds_TenderId",
                table: "TenderBidBonds",
                column: "TenderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenderBidBonds_ValidityDate",
                table: "TenderBidBonds",
                column: "ValidityDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_Status",
                table: "Tenders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_SubmissionDeadline",
                table: "Tenders",
                column: "SubmissionDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_Tenders_TenderNumber",
                table: "Tenders",
                column: "TenderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenderBidBonds");

            migrationBuilder.DropTable(
                name: "Tenders");
        }
    }
}
