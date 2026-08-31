using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: `dotnet ef migrations add` also picked up an UNRELATED pending model diff here
            // (ServiceRequests.ReferenceNumber / Quotations.QuotationNumber becoming unique indexes)
            // — that's #20260817100101_AddUniqueReferenceAndQuotationNumberIndexes never having been
            // mirrored into this Tenant migration history, a real and separate drift. Deliberately
            // NOT included in this migration; filed as its own issue (#308) rather than bundled here.
            //
            // #279: Name/ClientName/Title are searched via a leading-wildcard LIKE ('%term%', from
            // EF.Functions.ILike — see ProjectService/AssignmentService), which no ordinary b-tree
            // index can serve. pg_trgm's GIN index is built specifically to serve that pattern via
            // trigram similarity. Extensions are database-scoped, not schema-scoped — IF NOT EXISTS
            // makes this safe to also run (as a no-op) every time this migration replays for a later
            // tenant schema under the schema-per-tenant model.
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
        }
    }
}
