using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntercompanyManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RelatedParties",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CompanyName = table.Column<string>(type: "text", nullable: false),
                    RegNo = table.Column<string>(type: "text", nullable: false),
                    Relationship = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelatedParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Icsas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RelatedPartyId = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    RechargeRate = table.Column<decimal>(type: "numeric", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Icsas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Icsas_RelatedParties_RelatedPartyId",
                        column: x => x.RelatedPartyId,
                        principalTable: "RelatedParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntercompanyTxns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    IcsaId = table.Column<string>(type: "text", nullable: false),
                    RelatedPartyId = table.Column<string>(type: "text", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    QslLedgerRef = table.Column<string>(type: "text", nullable: false),
                    SisterLedgerRef = table.Column<string>(type: "text", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMPTZ", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "BOOLEAN", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntercompanyTxns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntercompanyTxns_Icsas_IcsaId",
                        column: x => x.IcsaId,
                        principalTable: "Icsas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IntercompanyTxns_RelatedParties_RelatedPartyId",
                        column: x => x.RelatedPartyId,
                        principalTable: "RelatedParties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Icsas_RelatedPartyId",
                table: "Icsas",
                column: "RelatedPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_IntercompanyTxns_IcsaId",
                table: "IntercompanyTxns",
                column: "IcsaId");

            migrationBuilder.CreateIndex(
                name: "IX_IntercompanyTxns_ReconciledAt",
                table: "IntercompanyTxns",
                column: "ReconciledAt");

            migrationBuilder.CreateIndex(
                name: "IX_IntercompanyTxns_RelatedPartyId",
                table: "IntercompanyTxns",
                column: "RelatedPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_RelatedParties_RegNo",
                table: "RelatedParties",
                column: "RegNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntercompanyTxns");

            migrationBuilder.DropTable(
                name: "Icsas");

            migrationBuilder.DropTable(
                name: "RelatedParties");
        }
    }
}
