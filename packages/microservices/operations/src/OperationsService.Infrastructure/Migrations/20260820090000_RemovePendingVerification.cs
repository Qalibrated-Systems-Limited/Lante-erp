using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// #330 -- speculative future-proofing (per the entity's own doc comment: "backs the operations-side
    /// SR pipeline if/when intake also runs here") that never materialized. The real, live OTP-intake
    /// PendingVerification lives in ticketing-service and is unaffected. Zero writer or reader of this
    /// copy anywhere in operations. Hand-authored rather than `dotnet ef migrations add` because the
    /// live schema here has an unrelated pre-existing timestamptz/timestamp drift (see #337) that would
    /// otherwise get bundled into this unrelated cleanup.
    /// </remarks>
    public partial class RemovePendingVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingVerifications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingVerifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    OtpCode = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FormType = table.Column<string>(type: "text", nullable: false),
                    FormDataJson = table.Column<string>(type: "text", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingVerifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingVerifications_Email",
                table: "PendingVerifications",
                column: "Email");
        }
    }
}
