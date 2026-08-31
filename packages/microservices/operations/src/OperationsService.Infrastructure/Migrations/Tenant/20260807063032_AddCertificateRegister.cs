using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OperationsService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddCertificateRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientName",
                table: "CalibrationCertificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Recall30SentAt",
                table: "CalibrationCertificates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Recall60SentAt",
                table: "CalibrationCertificates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Recall7SentAt",
                table: "CalibrationCertificates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetType",
                table: "CalibrationCertificates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAt",
                table: "CalibrationCertificates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WithdrawnReason",
                table: "CalibrationCertificates",
                type: "text",
                nullable: true);

            // Backfill the denormalised columns for certificates issued before this migration, so the
            // register isn't blank for existing records. Each row is converted inside its own block:
            // a certificate whose snapshot isn't valid JSON must not fail the migration for every
            // other tenant — the register falls back to parsing CertJson at read time for those.
            migrationBuilder.Sql(@"
                DO $$
                DECLARE r RECORD;
                BEGIN
                    FOR r IN SELECT ""Id"", ""CertJson"" FROM ""CalibrationCertificates""
                             WHERE ""CertJson"" IS NOT NULL AND ""CertJson"" <> '' LOOP
                        BEGIN
                            UPDATE ""CalibrationCertificates""
                               SET ""ClientName"" = r.""CertJson""::jsonb->>'CustomerName',
                                   ""SheetType""  = r.""CertJson""::jsonb->>'SheetType'
                             WHERE ""Id"" = r.""Id"";
                        EXCEPTION WHEN others THEN
                            NULL;
                        END;
                    END LOOP;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientName",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "Recall30SentAt",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "Recall60SentAt",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "Recall7SentAt",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "SheetType",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "WithdrawnAt",
                table: "CalibrationCertificates");

            migrationBuilder.DropColumn(
                name: "WithdrawnReason",
                table: "CalibrationCertificates");
        }
    }
}
