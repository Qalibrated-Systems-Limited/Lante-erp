using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCustomerSurveyResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerSurveyResponses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RespondentName = table.Column<string>(type: "text", nullable: false),
                    CorporationName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    CounsellorName = table.Column<string>(type: "text", nullable: false),
                    FeedbackType = table.Column<int>(type: "integer", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSurveyResponses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSurveyResponses_SubmittedAt",
                table: "CustomerSurveyResponses",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerSurveyResponses");
        }
    }
}
