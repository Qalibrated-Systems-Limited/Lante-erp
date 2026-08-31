using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Attendance;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>H4 (ATT-001..008, P27 + P28) — clock-in/out with GPS, absences and their reconciliation against
/// approved leave, working-time and holiday configuration, and the monthly/annual attendance reporting.
/// <para>Clock-in sits behind <c>hr.write</c> because in this pass HR records attendance on an employee's behalf
/// (HR-DEC-8 defers self-service); the endpoint shape is already what a mobile app will call. Working-time and
/// holiday configuration needs <c>hr.write</c> too — the holiday calendar silently changes every leave day
/// count.</para></summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class AttendanceController(IAttendanceService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name");
    private string? Schema => User.FindFirstValue("schema");

    [HttpGet("attendance/summary")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> Summary() => Ok(new { data = await service.GetSummaryAsync() });

    // ── Clock-in / clock-out (P27) ──
    [HttpGet("attendance/records")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListRecords(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? employeeId, [FromQuery] string? status)
        => Ok(new { data = await service.ListRecordsAsync(from, to, employeeId, status) });

    [HttpPost("attendance/clock-in")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ClockIn([FromBody] ClockInDto dto)
        => Act(await service.ClockInAsync(dto, Schema, UserId, UserName));

    [HttpPost("attendance/clock-out")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> ClockOut([FromBody] ClockOutDto dto)
        => Act(await service.ClockOutAsync(dto, UserId));

    // ── Absences (P27 step 27.5 / ATT-004) ──
    [HttpGet("attendance/absences")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListAbsences(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? employeeId, [FromQuery] bool? authorised)
        => Ok(new { data = await service.ListAbsencesAsync(from, to, employeeId, authorised) });

    [HttpPost("attendance/absences")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> RecordAbsence([FromBody] RecordAbsenceDto dto)
        => Act(await service.RecordAbsenceAsync(dto, UserId));

    /// <summary>Authorises an absence after the fact — needs approval rights, since it removes a pay deduction.</summary>
    [HttpPost("attendance/absences/{id}/excuse")]
    [Authorize(Policy = "Permission:hr.approve")]
    public async Task<IActionResult> ExcuseAbsence(string id, [FromBody] ExcuseAbsenceDto dto)
        => Act(await service.ExcuseAbsenceAsync(id, dto, UserId));

    /// <summary>Unpaid days awaiting payroll (H6 seam). Payroll-tier permission — it is pay data.</summary>
    [HttpGet("attendance/unpaid-absences")]
    [Authorize(Policy = "Permission:hr.payroll.read")]
    public async Task<IActionResult> UnpaidAbsences([FromQuery] int? year, [FromQuery] int? month)
        => Ok(new { data = await service.ListUnpaidAbsencesAsync(year, month) });

    // ── Working-time configuration + holidays ──
    [HttpGet("attendance/settings")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetSettings() => Ok(new { data = await service.GetSettingsAsync() });

    [HttpPut("attendance/settings")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdateSettings([FromBody] SaveAttendanceSettingDto dto)
        => Act(await service.UpdateSettingsAsync(dto, UserId));

    [HttpGet("attendance/holidays")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListHolidays([FromQuery] int? year, [FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListHolidaysAsync(year, includeInactive) });

    [HttpPost("attendance/holidays")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreateHoliday([FromBody] SavePublicHolidayDto dto)
        => Act(await service.CreateHolidayAsync(dto, UserId));

    [HttpPut("attendance/holidays/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdateHoliday(string id, [FromBody] SavePublicHolidayDto dto)
        => Act(await service.UpdateHolidayAsync(id, dto, UserId));

    [HttpDelete("attendance/holidays/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> DeleteHoliday(string id)
        => Act(await service.DeleteHolidayAsync(id, UserId));

    [HttpPost("attendance/holidays/seed-kenyan")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SeedHolidays([FromQuery] int? year)
        => Act(await service.SeedKenyanHolidaysAsync(year ?? DateTime.UtcNow.Year, UserId));

    // ── Reporting (P28) ──
    [HttpGet("attendance/scorecards")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListScorecards(
        [FromQuery] int? year, [FromQuery] string? employeeId, [FromQuery] string? departmentId)
        => Ok(new { data = await service.ListScorecardsAsync(year, employeeId, departmentId) });

    [HttpGet("attendance/monthly-reports")]
    [Authorize(Policy = "Permission:hr.read.dept")]
    public async Task<IActionResult> ListMonthlyReports(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? departmentId)
        => Ok(new { data = await service.ListMonthlyReportsAsync(year, month, departmentId) });

    /// <summary>Runs the attendance sweep now instead of waiting for the daily tick. Idempotent.</summary>
    [HttpPost("attendance/sweep")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Sweep()
        => Ok(new { data = await service.RunAttendanceSweepAsync(Schema, UserId) });

    private IActionResult Act(AttendanceActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
