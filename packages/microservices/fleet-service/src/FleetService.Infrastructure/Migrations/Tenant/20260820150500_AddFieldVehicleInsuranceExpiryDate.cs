using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FleetService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFieldVehicleInsuranceExpiryDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InsuranceExpiryDate",
                table: "FieldVehicles",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill from the existing free-text InsuranceExpiry column (issue #375: nothing
            // could check this for expiry because a string can't be compared in SQL, and
            // production values are unvalidated — ISO, dd/mm/yyyy, dd.mm.yyyy, or unparseable
            // garbage like "expired"). Tries each known format in turn; anything that doesn't
            // match any of them, or matches a format but isn't a real calendar date (e.g. a
            // regex-valid "31/02/2020"), is left NULL rather than failing the migration — the
            // same "unreadable, not a hard error" treatment #376 established client-side via
            // expiry.js's expiryState(), which this backfill deliberately mirrors so a record
            // that reads as "unknown" in the UI doesn't quietly read as "compliant" here. The
            // per-row EXCEPTION block is what makes this safe against exactly that kind of
            // regex-matches-but-invalid-date value, which a plain to_date() UPDATE would throw on
            // and abort the whole migration for.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    rec RECORD;
                    parsed DATE;
                BEGIN
                    FOR rec IN
                        SELECT ""Id"", ""InsuranceExpiry"" FROM ""FieldVehicles""
                        WHERE ""InsuranceExpiry"" IS NOT NULL AND trim(""InsuranceExpiry"") != ''
                    LOOP
                        parsed := NULL;
                        BEGIN
                            IF rec.""InsuranceExpiry"" ~ '^\d{4}-\d{2}-\d{2}$' THEN
                                parsed := to_date(rec.""InsuranceExpiry"", 'YYYY-MM-DD');
                            ELSIF rec.""InsuranceExpiry"" ~ '^\d{2}/\d{2}/\d{4}$' THEN
                                parsed := to_date(rec.""InsuranceExpiry"", 'DD/MM/YYYY');
                            ELSIF rec.""InsuranceExpiry"" ~ '^\d{2}\.\d{2}\.\d{4}$' THEN
                                parsed := to_date(rec.""InsuranceExpiry"", 'DD.MM.YYYY');
                            END IF;
                        EXCEPTION WHEN OTHERS THEN
                            parsed := NULL;
                        END;
                        UPDATE ""FieldVehicles"" SET ""InsuranceExpiryDate"" = parsed WHERE ""Id"" = rec.""Id"";
                    END LOOP;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InsuranceExpiryDate",
                table: "FieldVehicles");
        }
    }
}
