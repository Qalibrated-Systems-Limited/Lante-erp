using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UserService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryCountToTenantServiceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: a prior `dotnet ef migrations add` run here also picked up a CreateTable for
            // "TenantEmailSettings" — that table is already owned by migration
            // 20260817115551_AddTenantEmailSettings (commit 648ed213); the model snapshot had
            // simply never been regenerated to reflect it, a pre-existing drift unrelated to this
            // change. Deliberately NOT re-issuing that CreateTable here to avoid a duplicate-table
            // error against an environment where 20260817115551 has already run. Left out of this
            // migration; flagged separately rather than silently folded in.
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                schema: "public",
                table: "TenantServiceSchemas",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetryCount",
                schema: "public",
                table: "TenantServiceSchemas");
        }
    }
}
