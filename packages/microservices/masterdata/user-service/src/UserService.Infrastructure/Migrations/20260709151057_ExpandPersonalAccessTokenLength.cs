using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Infrastructure.Migrations
{
    /// <summary>
    /// Control-plane twin of the Tenant migration of the same name. Google login runs pre-auth
    /// (no schema claim → search_path=public), so its PersonalAccessToken insert lands in
    /// public."PersonalAccessTokens" — where Token was still varchar(2048). The JWT grows with the
    /// user's role/permission claim count (RoleAdmin ≈ 35 permission claims) and exceeds 2048,
    /// so the insert threw Postgres 22001 and google-login surfaced "A database error occurred".
    /// Widening only the tenant schemas would not fix that path; this widens public too. The
    /// unqualified table name resolves to public at startup-migration time (no HttpContext →
    /// the connection interceptor pins search_path to public).
    /// </summary>
    public partial class ExpandPersonalAccessTokenLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "PersonalAccessTokens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "PersonalAccessTokens",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
