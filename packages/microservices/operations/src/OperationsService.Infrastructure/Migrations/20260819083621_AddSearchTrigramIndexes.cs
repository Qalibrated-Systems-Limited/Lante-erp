using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #279: Name/ClientName/Title are searched via a leading-wildcard LIKE
            // ('%term%', from EF.Functions.ILike — see ProjectService/AssignmentService), which no
            // ordinary b-tree index can serve. pg_trgm's GIN index is built specifically to serve
            // that pattern via trigram similarity. Extensions are database-scoped, not
            // schema-scoped — IF NOT EXISTS makes this safe to also run (as a no-op) every time
            // this migration replays for a later tenant schema under the schema-per-tenant model.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Projects_Name_Trgm\" ON \"Projects\" USING gin (\"Name\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Projects_ClientName_Trgm\" ON \"Projects\" USING gin (\"ClientName\" gin_trgm_ops);");
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Assignments_Title_Trgm\" ON \"Assignments\" USING gin (\"Title\" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Assignments_Title_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Projects_ClientName_Trgm\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Projects_Name_Trgm\";");
            // pg_trgm is deliberately NOT dropped here — it's database-scoped, other schemas'
            // copies of this same migration (or anything else) may still depend on it, and
            // DROP EXTENSION would fail loudly if any index built on it still exists anyway.
        }
    }
}
