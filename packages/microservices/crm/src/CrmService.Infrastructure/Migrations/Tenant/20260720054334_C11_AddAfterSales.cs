using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrmService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class C11_AddAfterSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastNpsScore",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastSatisfactionScore",
                table: "Customers",
                type: "numeric(4,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientComplaints",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ComplaintNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedTo = table.Column<string>(type: "text", nullable: true),
                    AssignedToName = table.Column<string>(type: "text", nullable: true),
                    Resolution = table.Column<string>(type: "text", nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientComplaints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientSatisfactionSurveys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<string>(type: "text", nullable: true),
                    ProjectName = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Feedback = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientSatisfactionSurveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NpsSurveys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Feedback = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpsSurveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceContracts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ContractNumber = table.Column<string>(type: "text", nullable: false),
                    CustomerId = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    ContractType = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    BillingFrequency = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AutoRenew = table.Column<bool>(type: "boolean", nullable: false),
                    RenewedFromContractId = table.Column<string>(type: "text", nullable: true),
                    RenewalAlert60SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RenewalAlert30SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceContracts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientComplaints_ComplaintNumber",
                table: "ClientComplaints",
                column: "ComplaintNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientComplaints_CustomerId",
                table: "ClientComplaints",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientComplaints_Status",
                table: "ClientComplaints",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSatisfactionSurveys_CustomerId",
                table: "ClientSatisfactionSurveys",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientSatisfactionSurveys_Status",
                table: "ClientSatisfactionSurveys",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NpsSurveys_CustomerId_Year",
                table: "NpsSurveys",
                columns: new[] { "CustomerId", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_NpsSurveys_Status",
                table: "NpsSurveys",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceContracts_ContractNumber",
                table: "ServiceContracts",
                column: "ContractNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceContracts_CustomerId",
                table: "ServiceContracts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceContracts_EndDate",
                table: "ServiceContracts",
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceContracts_Status",
                table: "ServiceContracts",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientComplaints");

            migrationBuilder.DropTable(
                name: "ClientSatisfactionSurveys");

            migrationBuilder.DropTable(
                name: "NpsSurveys");

            migrationBuilder.DropTable(
                name: "ServiceContracts");

            migrationBuilder.DropColumn(
                name: "LastNpsScore",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastSatisfactionScore",
                table: "Customers");
        }
    }
}
