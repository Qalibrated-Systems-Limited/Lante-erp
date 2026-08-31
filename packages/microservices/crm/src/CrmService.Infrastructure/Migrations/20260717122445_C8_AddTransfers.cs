using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class C8_AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientTransferRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    OutgoingOwnerId = table.Column<string>(type: "text", nullable: false),
                    IncomingOwnerId = table.Column<string>(type: "text", nullable: false),
                    IncomingOwnerName = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RaisedBy = table.Column<string>(type: "text", nullable: false),
                    HeadBdApprovedBy = table.Column<string>(type: "text", nullable: true),
                    HeadBdApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CfoApprovedBy = table.Column<string>(type: "text", nullable: true),
                    CfoApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MdApprovedBy = table.Column<string>(type: "text", nullable: true),
                    MdApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "text", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTransferRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientTransferHandovers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TransferRequestId = table.Column<string>(type: "text", nullable: false),
                    ClientHistory = table.Column<string>(type: "text", nullable: true),
                    ActiveWork = table.Column<string>(type: "text", nullable: true),
                    PricingNotes = table.Column<string>(type: "text", nullable: true),
                    CreditTerms = table.Column<string>(type: "text", nullable: true),
                    OpenIssues = table.Column<string>(type: "text", nullable: true),
                    HandoverDocUrl = table.Column<string>(type: "text", nullable: true),
                    OutgoingSignedName = table.Column<string>(type: "text", nullable: true),
                    OutgoingSignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IncomingSignedName = table.Column<string>(type: "text", nullable: true),
                    IncomingSignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DeptHeadSignedName = table.Column<string>(type: "text", nullable: true),
                    DeptHeadSignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MdSignedName = table.Column<string>(type: "text", nullable: true),
                    MdSignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsPermanent = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientTransferHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientTransferHandovers_ClientTransferRequests_TransferRequ~",
                        column: x => x.TransferRequestId,
                        principalTable: "ClientTransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientTransferHandovers_TransferRequestId",
                table: "ClientTransferHandovers",
                column: "TransferRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientTransferRequests_CustomerId",
                table: "ClientTransferRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientTransferRequests_Status",
                table: "ClientTransferRequests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientTransferHandovers");

            migrationBuilder.DropTable(
                name: "ClientTransferRequests");
        }
    }
}
