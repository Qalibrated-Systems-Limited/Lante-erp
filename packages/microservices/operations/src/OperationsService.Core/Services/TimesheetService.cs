using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Timesheets;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O4 — see <see cref="ITimesheetService"/>.</summary>
public class TimesheetService(
    IGenericRepository<Timesheet> timesheets,
    IGenericRepository<TimesheetEntry> entries,
    IGenericRepository<ContractRate> rates,
    IHrGateway hr,
    IFinanceGateway finance,
    IMapper mapper) : ITimesheetService
{
    /// <summary>Overtime is paid at time-and-a-half, matching HR's weekday overtime multiplier.</summary>
    private const decimal OvertimeMultiplier = 1.5m;


    // ── Reads ───────────────────────────────────────────────────────────────────

    public async Task<TimesheetReadDto?> GetByIdAsync(string id)
    {
        var ts = await timesheets.Query()
            .Include(t => t.Entries.Where(e => !e.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        return ts is null ? null : mapper.Map<TimesheetReadDto>(ts);
    }

    public async Task<PaginatedResult<TimesheetReadDto>> GetAllAsync(TimesheetFilterParameters filters)
    {
        var query = timesheets.Query().Include(t => t.Entries.Where(e => !e.IsDeleted)).Where(t => !t.IsDeleted);

        if (!string.IsNullOrEmpty(filters.EmployeeId))   query = query.Where(t => t.EmployeeId == filters.EmployeeId);
        if (!string.IsNullOrEmpty(filters.DepartmentId)) query = query.Where(t => t.DepartmentId == filters.DepartmentId);
        if (!string.IsNullOrEmpty(filters.Status) && Enum.TryParse<TimesheetStatus>(filters.Status, true, out var st))
            query = query.Where(t => t.Status == st);

        query = filters.SortDescending
            ? query.OrderByDescending(t => t.WeekStartDate)
            : query.OrderBy(t => t.WeekStartDate);

        var total = await query.CountAsync();
        var items = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<TimesheetReadDto>
        {
            Items = mapper.Map<List<TimesheetReadDto>>(items),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize,
        };
    }

    // ── Timesheet lifecycle ──────────────────────────────────────────────────────

    public async Task<TimesheetReadDto> CreateAsync(CreateTimesheetDto dto, string employeeId, string employeeName, string departmentId)
    {
        var weekStart = MondayOf(dto.WeekStartDate);
        var existing = await timesheets.Query()
            .FirstOrDefaultAsync(t => t.EmployeeId == employeeId && t.WeekStartDate == weekStart && !t.IsDeleted);
        if (existing != null)
            throw new InvalidOperationException($"A timesheet already exists for the week of {weekStart:yyyy-MM-dd}.");

        var ts = await timesheets.CreateAsync(new Timesheet
        {
            EmployeeId    = employeeId,
            EmployeeName  = employeeName,
            DepartmentId  = departmentId,
            WeekStartDate = weekStart,
            WeekEndDate   = weekStart.AddDays(6),
            Status        = TimesheetStatus.Draft,
            CreatedBy     = employeeId,
            UpdatedBy     = employeeId,
        });
        return (await GetByIdAsync(ts.Id))!;
    }

    public async Task<TimesheetReadDto> SubmitAsync(string timesheetId, string userId)
    {
        var ts = await LoadEditableAsync(timesheetId);

        // OT pre-approval: every entry with overtime must be approved before submission.
        var unapprovedOt = await entries.Query()
            .AnyAsync(e => e.TimesheetId == ts.Id && !e.IsDeleted && e.OvertimeHours > 0m && !e.IsOvertimeApproved);
        if (unapprovedOt)
            throw new InvalidOperationException("All overtime must be pre-approved (HR) before the timesheet can be submitted.");

        await RecomputeTotalsAsync(ts, userId);
        ts.Status = TimesheetStatus.Submitted;
        ts.SubmittedAt = DateTime.UtcNow;
        ts.UpdatedBy = userId;
        ts.UpdatedAt = DateTime.UtcNow;
        await timesheets.UpdateAsync(ts);
        return (await GetByIdAsync(ts.Id))!;
    }

    public async Task<TimesheetReadDto> ReviewAsync(string timesheetId, ReviewTimesheetDto dto, string approverId, string approverName)
    {
        var ts = await timesheets.GetByIdAsync(timesheetId)
            ?? throw new KeyNotFoundException($"Timesheet {timesheetId} not found.");
        if (ts.Status != TimesheetStatus.Submitted)
            throw new InvalidOperationException($"Only a submitted timesheet can be reviewed. Current status: {ts.Status}.");

        if (!dto.Approved)
        {
            ts.Status = TimesheetStatus.Rejected;
            ts.RejectionReason = dto.Comments;
            ts.UpdatedBy = approverId;
            ts.UpdatedAt = DateTime.UtcNow;
            await timesheets.UpdateAsync(ts);
            return (await GetByIdAsync(ts.Id))!;
        }

        ts.Status = TimesheetStatus.Approved;
        ts.ApprovedBy = approverName;
        ts.ApprovedAt = DateTime.UtcNow;
        ts.UpdatedBy = approverId;
        ts.UpdatedAt = DateTime.UtcNow;
        await timesheets.UpdateAsync(ts);

        // O4 — LM approval fans out: labour → Finance project-actuals journal (per project), hours →
        // HR payroll. Config-gated seams; wrapped so a downstream failure never voids approval.
        var lines = await entries.Query().Where(e => e.TimesheetId == ts.Id && !e.IsDeleted).ToListAsync();
        try
        {
            foreach (var g in lines.Where(e => !string.IsNullOrEmpty(e.ProjectId)).GroupBy(e => e.ProjectId!))
            {
                var hours = g.Sum(e => e.Hours);
                var otHours = g.Sum(e => e.OvertimeHours);
                var (amount, rateCode) = await PriceLabourAsync(g.Key, hours, otHours);

                await finance.PostTimesheetLabourAsync(new TimesheetLabourPosting(
                    g.Key, ts.EmployeeId, ts.WeekEndDate, hours, otHours, amount, rateCode));
            }

            await hr.PostTimesheetToPayrollAsync(new PayrollPosting(
                ts.EmployeeId, ts.WeekEndDate, ts.TotalHours, ts.OvertimeHours));
        }
        catch (Exception) { /* seams are best-effort; approval already committed */ }

        return (await GetByIdAsync(ts.Id))!;
    }

    // ── Entries ──────────────────────────────────────────────────────────────────

    public async Task<TimesheetEntryReadDto> AddEntryAsync(string timesheetId, CreateTimesheetEntryDto dto, string userId)
    {
        var ts = await LoadEditableAsync(timesheetId);
        var entry = await entries.CreateAsync(new TimesheetEntry
        {
            TimesheetId   = ts.Id,
            WorkDate      = dto.WorkDate,
            ProjectId     = dto.ProjectId,
            AssignmentId  = dto.AssignmentId,
            ProjectTaskId = dto.ProjectTaskId,
            Description   = dto.Description,
            Hours         = dto.Hours,
            OvertimeHours = dto.OvertimeHours,
            IsBillable    = dto.IsBillable ?? true,
            Source        = TimesheetEntrySource.Manual,
            CreatedBy     = userId,
            UpdatedBy     = userId,
        });
        await RecomputeTotalsAsync(ts, userId);
        return mapper.Map<TimesheetEntryReadDto>(entry);
    }

    public async Task<TimesheetEntryReadDto> UpdateEntryAsync(string entryId, UpdateTimesheetEntryDto dto, string userId)
    {
        var entry = await entries.GetByIdAsync(entryId)
            ?? throw new KeyNotFoundException($"Timesheet entry {entryId} not found.");
        var ts = await LoadEditableAsync(entry.TimesheetId);

        if (dto.WorkDate.HasValue)      entry.WorkDate = dto.WorkDate.Value;
        if (dto.ProjectId != null)      entry.ProjectId = dto.ProjectId;
        if (dto.AssignmentId != null)   entry.AssignmentId = dto.AssignmentId;
        if (dto.ProjectTaskId != null)  entry.ProjectTaskId = dto.ProjectTaskId;
        if (dto.Description != null)     entry.Description = dto.Description;
        if (dto.Hours.HasValue)          entry.Hours = dto.Hours.Value;
        if (dto.IsBillable.HasValue)     entry.IsBillable = dto.IsBillable.Value;
        if (dto.OvertimeHours.HasValue)
        {
            // Changing overtime invalidates any prior approval — it must be re-requested.
            if (dto.OvertimeHours.Value != entry.OvertimeHours)
            {
                entry.OvertimeHours = dto.OvertimeHours.Value;
                entry.IsOvertimeApproved = false;
                entry.OvertimeRequestRef = null;
            }
        }
        entry.UpdatedBy = userId;
        entry.UpdatedAt = DateTime.UtcNow;
        await entries.UpdateAsync(entry);
        await RecomputeTotalsAsync(ts, userId);
        return mapper.Map<TimesheetEntryReadDto>(entry);
    }

    public async Task DeleteEntryAsync(string entryId, string userId)
    {
        var entry = await entries.GetByIdAsync(entryId)
            ?? throw new KeyNotFoundException($"Timesheet entry {entryId} not found.");
        var ts = await LoadEditableAsync(entry.TimesheetId);
        entry.IsDeleted = true;
        entry.UpdatedBy = userId;
        entry.UpdatedAt = DateTime.UtcNow;
        await entries.UpdateAsync(entry);
        await RecomputeTotalsAsync(ts, userId);
    }

    public async Task<TimesheetEntryReadDto> RequestOvertimeAsync(string entryId, RequestOvertimeDto dto, string userId)
    {
        var entry = await entries.GetByIdAsync(entryId)
            ?? throw new KeyNotFoundException($"Timesheet entry {entryId} not found.");
        if (entry.OvertimeHours <= 0m)
            throw new InvalidOperationException("This entry has no overtime hours to approve.");

        var ts = await timesheets.GetByIdAsync(entry.TimesheetId);

        // PR2 — verify against HR's overtime register rather than asking HR to create an approval.
        // HR only grants overtime BEFORE it is worked (ATT-007); a timesheet is filled in afterwards,
        // so this checks that the manager already approved the day. An unapproved entry stays
        // unapproved and the submit gate keeps the timesheet closed.
        var result = await hr.ResolveOvertimeApprovalAsync(new OvertimeApprovalQuery(
            ts?.EmployeeId ?? string.Empty, entry.Id, entry.WorkDate, entry.OvertimeHours));

        if (!result.Approved)
            throw new InvalidOperationException(
                result.Message ?? "This overtime has not been approved in HR.");

        entry.OvertimeRequestRef = result.Reference;
        entry.IsOvertimeApproved = true;
        entry.UpdatedBy = userId;
        entry.UpdatedAt = DateTime.UtcNow;
        await entries.UpdateAsync(entry);
        return mapper.Map<TimesheetEntryReadDto>(entry);
    }

    // ── Check-in time auto-capture ───────────────────────────────────────────────

    /// <summary>
    /// PR2 — the user's explicit ask: field time should not have to be typed twice. A check-out closes
    /// a geo-stamped interval that already says who was where and for how long, so it writes the
    /// timesheet line itself.
    ///
    /// <para>Idempotent on the check-in id, reusing the dedup mechanism <c>FsrImport</c> already
    /// established. The line lands on a DRAFT timesheet, created for the week if the technician does
    /// not have one — capture must never depend on someone having opened their timesheet first. It is
    /// still theirs to review and submit: this captures time, it does not approve it.</para>
    /// </summary>
    public async Task<string?> ImportFromCheckInAsync(CheckInTimeImport import)
    {
        if (import.Hours <= 0m) return null;
        if (string.IsNullOrWhiteSpace(import.EmployeeId)) return null;

        var existing = await entries.Query()
            .FirstOrDefaultAsync(e => e.SourceRef == import.SourceRef && !e.IsDeleted);
        if (existing is not null) return existing.Id;

        var weekStart = MondayOf(import.WorkDate);
        var ts = await timesheets.Query()
            .FirstOrDefaultAsync(t => t.EmployeeId == import.EmployeeId && t.WeekStartDate == weekStart && !t.IsDeleted);

        // A submitted or approved week is closed to new lines — silently appending to it would change
        // hours a manager has already signed off. Leave the time uncaptured and let it be added
        // manually rather than mutating an approved record.
        if (ts is not null && ts.Status is not (TimesheetStatus.Draft or TimesheetStatus.Rejected))
            return null;

        ts ??= await timesheets.CreateAsync(new Timesheet
        {
            EmployeeId    = import.EmployeeId,
            EmployeeName  = import.EmployeeName,
            DepartmentId  = import.DepartmentId,
            WeekStartDate = weekStart,
            WeekEndDate   = weekStart.AddDays(6),
            Status        = TimesheetStatus.Draft,
            CreatedBy     = "system",
            UpdatedBy     = "system",
        });

        var entry = await entries.CreateAsync(new TimesheetEntry
        {
            TimesheetId   = ts.Id,
            WorkDate      = import.WorkDate,
            ProjectId     = import.ProjectId,
            AssignmentId  = import.AssignmentId,
            ProjectTaskId = import.ProjectTaskId,
            Description   = import.Description,
            Hours         = import.Hours,
            // Overtime is deliberately left at zero. A long day on site is not overtime until someone
            // approves it as such (ATT-007) — deriving it from the clock would create hours that were
            // never pre-approved and would then block the whole timesheet at submission.
            OvertimeHours = 0m,
            IsBillable    = import.IsBillable,
            Source        = TimesheetEntrySource.CheckIn,
            SourceRef     = import.SourceRef,
            CreatedBy     = "system",
            UpdatedBy     = "system",
        });

        await RecomputeTotalsAsync(ts, "system");
        return entry.Id;
    }

    // ── Labour pricing ───────────────────────────────────────────────────────────

    /// <summary>
    /// PR2 — prices a project's timesheet hours from its PR1 rate card, so the finance labour journal
    /// carries money rather than hours. Uses <see cref="ContractRate.CostRate"/> (what the work costs
    /// us), not the client rate: this posts cost of delivery, not revenue.
    ///
    /// <para>Returns zero when the project has no single unambiguous hourly labour rate in force. That
    /// is deliberate — the gateway skips a zero posting, and skipping is far better than picking one
    /// of several rates arbitrarily, which would put a number in the general ledger that nobody can
    /// derive from the contract.</para>
    /// </summary>
    private async Task<(decimal Amount, string? RateCode)> PriceLabourAsync(
        string projectId, decimal hours, decimal overtimeHours)
    {
        if (hours <= 0m && overtimeHours <= 0m) return (0m, null);

        var today = DateTime.UtcNow.Date;
        var candidates = await rates.Query()
            .Where(r => r.ProjectId == projectId
                     && !r.IsDeleted
                     && r.IsActive
                     && r.Category == BudgetCategory.Labour
                     && r.Unit == RateUnit.Hour
                     && r.EffectiveFrom <= today
                     && (r.EffectiveTo == null || r.EffectiveTo >= today))
            .ToListAsync();

        if (candidates.Count != 1) return (0m, null);

        var rate = candidates[0];
        var amount = (hours * rate.CostRate) + (overtimeHours * rate.CostRate * OvertimeMultiplier);
        return (decimal.Round(amount, 2), rate.Code);
    }

    // ── FSR time auto-import ─────────────────────────────────────────────────────

    public async Task ImportFromServiceReportAsync(FsrTimeImport import)
    {
        if (import.Hours <= 0m) return;

        // Idempotent: skip if this report was already imported anywhere.
        if (await entries.Query().AnyAsync(e => e.SourceRef == import.SourceRef && !e.IsDeleted)) return;

        var weekStart = MondayOf(import.WorkDate);
        var ts = await timesheets.Query()
            .FirstOrDefaultAsync(t => t.EmployeeId == import.EmployeeId && t.WeekStartDate == weekStart && !t.IsDeleted);
        ts ??= await timesheets.CreateAsync(new Timesheet
        {
            EmployeeId    = import.EmployeeId,
            EmployeeName  = import.EmployeeName,
            DepartmentId  = import.DepartmentId,
            WeekStartDate = weekStart,
            WeekEndDate   = weekStart.AddDays(6),
            Status        = TimesheetStatus.Draft,
            CreatedBy     = "system",
            UpdatedBy     = "system",
        });

        await entries.CreateAsync(new TimesheetEntry
        {
            TimesheetId  = ts.Id,
            WorkDate     = import.WorkDate,
            ProjectId    = import.ProjectId,
            AssignmentId = import.AssignmentId,
            Description  = import.Description,
            Hours        = import.Hours,
            Source       = TimesheetEntrySource.FsrImport,
            SourceRef    = import.SourceRef,
            CreatedBy    = "system",
            UpdatedBy    = "system",
        });
        await RecomputeTotalsAsync(ts, "system");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<Timesheet> LoadEditableAsync(string timesheetId)
    {
        var ts = await timesheets.GetByIdAsync(timesheetId)
            ?? throw new KeyNotFoundException($"Timesheet {timesheetId} not found.");
        if (ts.Status is TimesheetStatus.Submitted or TimesheetStatus.Approved)
            throw new InvalidOperationException($"A {ts.Status} timesheet cannot be edited.");
        return ts;
    }

    private async Task RecomputeTotalsAsync(Timesheet ts, string userId)
    {
        var lines = await entries.Query().Where(e => e.TimesheetId == ts.Id && !e.IsDeleted).ToListAsync();
        ts.TotalHours    = lines.Sum(e => e.Hours) + lines.Sum(e => e.OvertimeHours);
        ts.OvertimeHours = lines.Sum(e => e.OvertimeHours);
        ts.UpdatedBy = userId;
        ts.UpdatedAt = DateTime.UtcNow;
        await timesheets.UpdateAsync(ts);
    }

    // Normalize any date to the Monday (UTC, date-only) of its week.
    private static DateTime MondayOf(DateTime date)
    {
        var d = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return d.AddDays(-diff);
    }
}
