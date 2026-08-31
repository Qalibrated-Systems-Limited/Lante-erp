using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrService.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class H4_AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkMode",
                table: "Employees",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AbsenceRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    IsAuthorised = table.Column<bool>(type: "boolean", nullable: false),
                    LinkedLeaveRequestId = table.Column<string>(type: "text", nullable: true),
                    LinkedLeaveTypeCode = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    UnpaidDays = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    ReleasedToPayrollAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PatternAlertSentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExcusedBy = table.Column<string>(type: "text", nullable: true),
                    ExcusedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExcuseNotes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AbsenceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AbsenceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceMonthlyReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Headcount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedDays = table.Column<int>(type: "integer", nullable: false),
                    DaysPresent = table.Column<int>(type: "integer", nullable: false),
                    DaysLate = table.Column<int>(type: "integer", nullable: false),
                    DaysOnLeave = table.Column<int>(type: "integer", nullable: false),
                    AuthorisedAbsences = table.Column<int>(type: "integer", nullable: false),
                    UnauthorisedAbsences = table.Column<int>(type: "integer", nullable: false),
                    TotalLateMinutes = table.Column<int>(type: "integer", nullable: false),
                    UnpaidDays = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    PunctualityRate = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    AbsenceRate = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceMonthlyReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClockInAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClockOutAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClockInLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ClockInLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ClockOutLatitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ClockOutLongitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    ClockInMethod = table.Column<int>(type: "integer", nullable: true),
                    WorkMode = table.Column<int>(type: "integer", nullable: false),
                    LateMinutes = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HoursWorked = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    RecordedBy = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceScorecards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmployeeId = table.Column<string>(type: "text", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "text", nullable: true),
                    EmployeeName = table.Column<string>(type: "text", nullable: true),
                    DepartmentId = table.Column<string>(type: "text", nullable: true),
                    DepartmentName = table.Column<string>(type: "text", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ExpectedDays = table.Column<int>(type: "integer", nullable: false),
                    DaysPresent = table.Column<int>(type: "integer", nullable: false),
                    DaysLate = table.Column<int>(type: "integer", nullable: false),
                    DaysOnLeave = table.Column<int>(type: "integer", nullable: false),
                    AuthorisedAbsences = table.Column<int>(type: "integer", nullable: false),
                    UnauthorisedAbsences = table.Column<int>(type: "integer", nullable: false),
                    TotalLateMinutes = table.Column<int>(type: "integer", nullable: false),
                    PunctualityRate = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    AbsenceRate = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Score = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceScorecards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceScorecards_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    WorkDayStartMinutes = table.Column<int>(type: "integer", nullable: false),
                    WorkDayEndMinutes = table.Column<int>(type: "integer", nullable: false),
                    LunchStartMinutes = table.Column<int>(type: "integer", nullable: false),
                    LunchMinutes = table.Column<int>(type: "integer", nullable: false),
                    GraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AbsenceCutoffMinutes = table.Column<int>(type: "integer", nullable: false),
                    WorksMonday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksTuesday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksWednesday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksThursday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksFriday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksSaturday = table.Column<bool>(type: "boolean", nullable: false),
                    WorksSunday = table.Column<bool>(type: "boolean", nullable: false),
                    RequireGpsForField = table.Column<bool>(type: "boolean", nullable: false),
                    AbsencePatternThreshold = table.Column<int>(type: "integer", nullable: false),
                    AbsencePatternWindowDays = table.Column<int>(type: "integer", nullable: false),
                    AbsenceBackfillDays = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRecords_Date",
                table: "AbsenceRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRecords_EmployeeId_Date",
                table: "AbsenceRecords",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRecords_IsAuthorised",
                table: "AbsenceRecords",
                column: "IsAuthorised");

            migrationBuilder.CreateIndex(
                name: "IX_AbsenceRecords_ReleasedToPayrollAt",
                table: "AbsenceRecords",
                column: "ReleasedToPayrollAt");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceMonthlyReports_DepartmentId_Year_Month",
                table: "AttendanceMonthlyReports",
                columns: new[] { "DepartmentId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceMonthlyReports_Year_Month",
                table: "AttendanceMonthlyReports",
                columns: new[] { "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_Date",
                table: "AttendanceRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId_Date",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_Status",
                table: "AttendanceRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceScorecards_EmployeeId_Year",
                table: "AttendanceScorecards",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceScorecards_Year",
                table: "AttendanceScorecards",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                table: "PublicHolidays",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_IsActive",
                table: "PublicHolidays",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AbsenceRecords");

            migrationBuilder.DropTable(
                name: "AttendanceMonthlyReports");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "AttendanceScorecards");

            migrationBuilder.DropTable(
                name: "AttendanceSettings");

            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "WorkMode",
                table: "Employees");
        }
    }
}
