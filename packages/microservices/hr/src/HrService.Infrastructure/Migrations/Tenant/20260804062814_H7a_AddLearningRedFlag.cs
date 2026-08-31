using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H7a_AddLearningRedFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningRedFlags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FlagType = table.Column<string>(type: "text", nullable: false),
                    FlagKey = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: true),
                    Detail = table.Column<string>(type: "text", nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRedFlags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningRedFlags_EmployeeId",
                table: "LearningRedFlags",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRedFlags_FlagType_FlagKey",
                table: "LearningRedFlags",
                columns: new[] { "FlagType", "FlagKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningRedFlags");
        }
    }
}
