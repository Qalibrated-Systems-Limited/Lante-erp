using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Discipline;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H10 (P18–P21) — discipline, warnings, grievances and separation.
/// <para><b>The sequence is the fairness.</b> Each stage refuses to run before the one before it and stamps
/// who did what and when. This is the one part of HR whose audit trail may have to stand up outside the
/// building, so nothing here is a convenience shortcut.</para>
/// <para><b>Deadlines are WORKING days</b>, on the shared calendar — a show-cause letter issued on a Thursday
/// does not quietly expire over the weekend.</para>
/// <para><b>Final dues show their arithmetic.</b> Every component is stored, not just the total: someone
/// disputing their last payment needs an answer to "why", and "total dues 143,000" is not one.</para>
/// </summary>
public class DisciplineService(
    IGenericRepository<Employee> employees,
    IGenericRepository<EmployeeSalary> salaries,
    IGenericRepository<LeaveEntitlement> entitlements,
    IGenericRepository<LeaveType> leaveTypes,
    IGenericRepository<DisciplinaryCase> cases,
    IGenericRepository<WarningRecord> warnings,
    IGenericRepository<GrievanceCase> grievances,
    IGenericRepository<Separation> separations,
    IGenericRepository<HrAuditLog> audit,
    IWorkCalendar calendar,
    IFinanceGateway finance,
    IHrAlertGateway notifier) : IDisciplineService
{
    private static readonly EmploymentStatus[] Active =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>HR-021 — five working days to answer a show-cause letter.</summary>
    private const int DefaultResponseDays = 5;
    /// <summary>HR-021 — fourteen calendar days to appeal.</summary>
    private const int DefaultAppealDays = 14;
    /// <summary>HR-023 — two working days for HR to acknowledge a grievance.</summary>
    private const int GrievanceAckDays = 2;
    /// <summary>HR-022 — a verbal warning stands for six months, anything written for twelve.</summary>
    private const int VerbalWarningMonths = 6, WrittenWarningMonths = 12;
    /// <summary>HR-022 — three live warnings inside a rolling twelve months triggers a termination review.</summary>
    private const int WarningEscalationThreshold = 3, WarningWindowMonths = 12;
    /// <summary>Working days in an average month, for turning a monthly salary into a daily rate.</summary>
    private const decimal WorkingDaysPerMonth = 21m;

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<DisciplineSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var allCases = await cases.Query().AsNoTracking().ToListAsync();
        var live = allCases.Where(c => c.Status is not (DisciplinaryStatus.Closed or DisciplinaryStatus.Withdrawn)).ToList();

        var allWarnings = await warnings.Query().AsNoTracking().ToListAsync();
        var activeWarnings = allWarnings.Where(w => w.IsActive).ToList();
        var windowStart = today.AddMonths(-WarningWindowMonths);
        var atThreshold = activeWarnings
            .Where(w => w.IssuedDate.Date >= windowStart)
            .GroupBy(w => w.EmployeeId)
            .Count(g => g.Count() >= WarningEscalationThreshold);

        var allGrievances = await grievances.Query().AsNoTracking().ToListAsync();
        var openGrievances = allGrievances
            .Where(g => g.Status is not (GrievanceStatus.Resolved or GrievanceStatus.Withdrawn)).ToList();

        var allSeparations = await separations.Query().AsNoTracking().ToListAsync();
        var inProgress = allSeparations
            .Where(s => s.Status is not (SeparationStatus.Paid or SeparationStatus.Cancelled)).ToList();

        return new DisciplineSummaryDto
        {
            OpenCases = live.Count,
            AwaitingResponse = live.Count(c => c.Status == DisciplinaryStatus.ShowCauseIssued),
            ResponseOverdue = live.Count(c => c.Status == DisciplinaryStatus.ShowCauseIssued
                                           && c.ResponseDeadline is not null && c.ResponseDeadline.Value.Date < today),
            AwaitingHearing = live.Count(c => c.Status == DisciplinaryStatus.AwaitingHearing),
            UnderAppeal = live.Count(c => c.Status == DisciplinaryStatus.Appealed),

            ActiveWarnings = activeWarnings.Count,
            UnacknowledgedWarnings = activeWarnings.Count(w => !w.AcknowledgedByEmployee),
            EmployeesAtReviewThreshold = atThreshold,

            OpenGrievances = openGrievances.Count,
            GrievancesAwaitingAcknowledgement = openGrievances.Count(g => g.AcknowledgedAt is null),
            GrievanceSlaBreaches = openGrievances.Count(g => g.AcknowledgedAt is null
                && g.AcknowledgementDeadline is not null && g.AcknowledgementDeadline.Value.Date < today),
            GrievancesEscalated = allGrievances.Count(g => g.Status == GrievanceStatus.Escalated),

            SeparationsInProgress = inProgress.Count,
            SeparationsAwaitingMd = inProgress.Count(s => s.Status == SeparationStatus.PendingMd),
            SeparationsAwaitingPayment = inProgress.Count(s => s.Status == SeparationStatus.Approved),
            DuesOutstanding = Round(inProgress.Where(s => s.Status == SeparationStatus.Approved).Sum(s => s.NetDues)),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Disciplinary cases (P18)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<DisciplinaryCaseDto>> ListCasesAsync(string? status, string? employeeId)
    {
        var q = cases.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(c => c.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DisciplinaryStatus>(status, true, out var st))
            q = q.Where(c => c.Status == st);
        var list = await q.OrderByDescending(c => c.IncidentDate).ToListAsync();
        var today = DateTime.UtcNow.Date;
        return list.Select(c => ToDto(c, today)).ToList();
    }

    public async Task<DisciplinaryCaseDto?> GetCaseAsync(string id)
    {
        var c = await cases.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return c is null ? null : ToDto(c, DateTime.UtcNow.Date);
    }

    public async Task<DisciplineActionResult> OpenCaseAsync(OpenCaseDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.Description)) return Err("A case needs a description of the incident.");

        var incident = DateTime.SpecifyKind(dto.IncidentDate.Date, DateTimeKind.Utc);
        if (incident > DateTime.UtcNow.Date) return Err("An incident cannot be dated in the future.");
        if (incident < employee.HireDate.Date)
            return Err($"{incident:dd MMM yyyy} is before {employee.FullName} was hired.");

        var created = await cases.CreateAsync(new DisciplinaryCase
        {
            CaseNumber = await NextNumberAsync("DC", incident.Year),
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            IncidentDate = incident,
            Description = dto.Description.Trim(),
            Witnesses = dto.Witnesses,
            SourceModule = dto.SourceModule,
            SourceReference = dto.SourceReference,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new DisciplineActionResult("Opened",
            $"{created.CaseNumber} opened for {employee.FullName}. Issue the show-cause letter next.", created.Id);
        if (!string.IsNullOrWhiteSpace(dto.SourceModule))
            result.Warnings.Add($"Raised from {dto.SourceModule} reference {dto.SourceReference} — that record stays where it is; this case is the formal process.");

        await LogAsync("DisciplinaryCase", created.Id, HrAuditAction.DisciplinaryCaseOpened,
            $"{created.CaseNumber} opened for {employee.EmployeeNumber}: {created.Description}", userId);
        return result;
    }

    public async Task<DisciplineActionResult> IssueShowCauseAsync(string id, IssueShowCauseDto dto, string? tenantSchema, string userId)
    {
        var c = await cases.GetByIdAsync(id);
        if (c is null || c.IsDeleted) return Err("Case not found.");
        if (c.Status != DisciplinaryStatus.Open)
            return Err($"{c.CaseNumber} is {Spaced(c.Status)} — a show-cause letter belongs at the start.");

        var days = dto.ResponseDays ?? DefaultResponseDays;
        if (days is < 1 or > 30) return Err("The response window must be between 1 and 30 working days.");

        var issued = DateTime.UtcNow.Date;
        c.ShowCauseIssuedAt = DateTime.UtcNow;
        c.ShowCauseLetter = dto.Letter;
        // Working days, not calendar — five days over a weekend is three days to answer a career-affecting letter.
        c.ResponseDeadline = await AddWorkingDaysAsync(issued, days);
        c.Status = DisciplinaryStatus.ShowCauseIssued;
        Touch(c, userId);
        await cases.UpdateAsync(c);

        await NotifyAsync(tenantSchema, "Warning", $"Show cause issued — {c.CaseNumber}, {c.EmployeeName}",
            $"{c.EmployeeNumber} {c.EmployeeName} has until {c.ResponseDeadline:dd MMM yyyy} ({days} working days) to respond to {c.CaseNumber}.",
            "hr.manager");
        await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.ShowCauseIssued,
            $"{c.CaseNumber}: show cause issued, response due {c.ResponseDeadline:dd MMM yyyy} ({days} working days).", userId);
        return new DisciplineActionResult("Issued",
            $"Show-cause letter issued. {c.EmployeeName} has until {c.ResponseDeadline:dd MMM yyyy} to respond.", c.Id);
    }

    public async Task<DisciplineActionResult> RecordResponseAsync(string id, RecordResponseDto dto, string userId)
    {
        var c = await cases.GetByIdAsync(id);
        if (c is null || c.IsDeleted) return Err("Case not found.");
        if (c.Status != DisciplinaryStatus.ShowCauseIssued)
            return Err($"{c.CaseNumber} is {Spaced(c.Status)} — there is no show-cause letter outstanding.");

        var result = new DisciplineActionResult("Recorded", "", c.Id);

        if (!string.IsNullOrWhiteSpace(dto.Response))
        {
            c.EmployeeResponse = dto.Response.Trim();
            c.EmployeeRespondedAt = DateTime.UtcNow;
            if (c.ResponseDeadline is not null && DateTime.UtcNow.Date > c.ResponseDeadline.Value.Date)
                result.Warnings.Add($"This response came after the {c.ResponseDeadline:dd MMM yyyy} deadline — recorded as late, not refused.");
        }
        else
        {
            // No response is itself a fact the hearing needs, and the case must be able to proceed without one.
            result.Warnings.Add("No response was recorded — the hearing proceeds on the papers, which is worth noting in the hearing record.");
        }

        if (dto.HearingDate is not null)
        {
            var hearing = DateTime.SpecifyKind(dto.HearingDate.Value.Date, DateTimeKind.Utc);
            if (c.ShowCauseIssuedAt is not null && hearing < c.ShowCauseIssuedAt.Value.Date)
                return Err("A hearing cannot be dated before the show-cause letter was issued.");
            c.HearingDate = hearing;
            c.HearingPanel = dto.HearingPanel;
        }

        c.Status = DisciplinaryStatus.AwaitingHearing;
        Touch(c, userId);
        await cases.UpdateAsync(c);

        var message = c.HearingDate is null
            ? "Response recorded. Set a hearing date next."
            : $"Response recorded. Hearing set for {c.HearingDate:dd MMM yyyy}.";
        await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryResponseRecorded,
            $"{c.CaseNumber}: response {(string.IsNullOrWhiteSpace(dto.Response) ? "not given" : "recorded")}.", userId);
        // The hearing being convened is its own fact — who set it, for when. Kept separate from the response
        // so the trail reads as a sequence of acts rather than one lumped entry.
        if (c.HearingDate is not null)
            await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryHearingHeld,
                $"{c.CaseNumber}: hearing convened for {c.HearingDate:dd MMM yyyy}, panel: {c.HearingPanel ?? "not named"}.", userId);
        return result with { Message = message };
    }

    public async Task<DisciplineActionResult> RecordOutcomeAsync(string id, RecordOutcomeDto dto, string? tenantSchema, string userId, string? userName)
    {
        var c = await cases.GetByIdAsync(id);
        if (c is null || c.IsDeleted) return Err("Case not found.");
        if (c.Status != DisciplinaryStatus.AwaitingHearing)
            return Err($"{c.CaseNumber} is {Spaced(c.Status)} — an outcome can only follow a hearing.");
        if (c.HearingDate is null) return Err("Set the hearing date before recording its outcome.");
        if (!Enum.TryParse<DisciplinaryOutcome>(dto.Outcome, true, out var outcome) || outcome == DisciplinaryOutcome.None)
            return Err("The outcome must be NoAction, Warning, Suspension or Termination.");
        if (string.IsNullOrWhiteSpace(dto.Notes)) return Err("An outcome needs to say why — this record may be read outside the company.");

        WarningType? warningType = null;
        if (outcome == DisciplinaryOutcome.Warning)
        {
            if (!Enum.TryParse<WarningType>(dto.WarningType ?? "", true, out var wt))
                return Err("A warning outcome needs a type: Verbal, Written or FinalWritten.");
            warningType = wt;
        }

        var appealDays = dto.AppealDays ?? DefaultAppealDays;
        if (appealDays is < 1 or > 90) return Err("The appeal window must be between 1 and 90 days.");

        c.Outcome = outcome;
        c.OutcomeNotes = dto.Notes.Trim();
        c.HearingNotes = dto.HearingNotes ?? c.HearingNotes;
        c.OutcomeRecordedAt = DateTime.UtcNow;
        c.OutcomeRecordedBy = userId;
        c.RightOfAppealDeadline = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(appealDays), DateTimeKind.Utc);
        c.Status = DisciplinaryStatus.OutcomeRecorded;

        var result = new DisciplineActionResult("Recorded",
            $"{c.CaseNumber}: {Spaced(outcome)}. Right of appeal runs to {c.RightOfAppealDeadline:dd MMM yyyy}.", c.Id);

        // 18.4a — a warning outcome writes the warning record that the tracker then counts.
        if (warningType is not null)
        {
            var warning = await CreateWarningAsync(c.EmployeeId, warningType.Value, c.IncidentDate,
                $"{c.CaseNumber}: {c.OutcomeNotes}", c.Id, userId);
            c.WarningRecordId = warning.Id;
            result.Warnings.Add($"A {Spaced(warningType.Value).ToLowerInvariant()} warning was issued, standing until {warning.ExpiryDate:dd MMM yyyy}.");

            var live = await CountLiveWarningsAsync(c.EmployeeId);
            if (live >= WarningEscalationThreshold)
                result.Warnings.Add($"{c.EmployeeName} now has {live} live warnings in the last 12 months — at or past the termination-review threshold.");
        }

        // 18.4b — termination routes to separation, but does NOT itself terminate anyone: the separation and
        // its final dues are a separate approval, and somebody stays employed until they have been paid.
        if (outcome == DisciplinaryOutcome.Termination)
            result.Warnings.Add("Termination recorded. Open a separation to compute and approve the final dues — the employee stays active until that is paid.");

        Touch(c, userId);
        await cases.UpdateAsync(c);

        await NotifyAsync(tenantSchema, outcome == DisciplinaryOutcome.Termination ? "Critical" : "Warning",
            $"Disciplinary outcome — {c.CaseNumber}, {c.EmployeeName}: {Spaced(outcome)}",
            $"{c.EmployeeNumber} {c.EmployeeName}: {c.OutcomeNotes} Right of appeal to {c.RightOfAppealDeadline:dd MMM yyyy}.",
            "hr.approve");
        await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryOutcomeRecorded,
            $"{c.CaseNumber}: outcome {outcome} recorded by {userName ?? userId}. {c.OutcomeNotes}", userId, userName);
        return result;
    }

    public async Task<DisciplineActionResult> RecordAppealAsync(string id, RecordAppealDto dto, string userId, string? userName)
    {
        var c = await cases.GetByIdAsync(id);
        if (c is null || c.IsDeleted) return Err("Case not found.");

        // Lodging an appeal.
        if (string.IsNullOrWhiteSpace(dto.Decision))
        {
            if (c.Status != DisciplinaryStatus.OutcomeRecorded)
                return Err($"{c.CaseNumber} is {Spaced(c.Status)} — there is no outcome to appeal.");
            if (string.IsNullOrWhiteSpace(dto.Grounds)) return Err("An appeal needs its grounds.");
            if (c.RightOfAppealDeadline is not null && DateTime.UtcNow.Date > c.RightOfAppealDeadline.Value.Date)
                return Err($"The right of appeal closed on {c.RightOfAppealDeadline:dd MMM yyyy}.");

            c.AppealGrounds = dto.Grounds.Trim();
            c.AppealSubmittedAt = DateTime.UtcNow;
            c.Status = DisciplinaryStatus.Appealed;
            Touch(c, userId);
            await cases.UpdateAsync(c);

            await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryAppealRecorded,
                $"{c.CaseNumber}: appeal lodged. {c.AppealGrounds}", userId);
            return new DisciplineActionResult("Appealed", $"Appeal lodged against {c.CaseNumber}.", c.Id);
        }

        // Deciding one.
        if (c.Status != DisciplinaryStatus.Appealed) return Err($"{c.CaseNumber} has no appeal outstanding.");
        // The person who recorded the outcome must not decide the appeal against it.
        if (!string.IsNullOrWhiteSpace(c.OutcomeRecordedBy) && c.OutcomeRecordedBy == userId)
            return Err("Whoever recorded the outcome cannot decide the appeal against it — an appeal needs fresh eyes.");

        c.AppealOutcome = dto.Decision.Trim();
        c.AppealDecidedAt = DateTime.UtcNow;
        c.Status = DisciplinaryStatus.OutcomeRecorded;
        Touch(c, userId);
        await cases.UpdateAsync(c);

        await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryAppealRecorded,
            $"{c.CaseNumber}: appeal decided by {userName ?? userId}. {c.AppealOutcome}", userId, userName);
        return new DisciplineActionResult("Decided", $"Appeal on {c.CaseNumber} decided.", c.Id);
    }

    public async Task<DisciplineActionResult> CloseCaseAsync(string id, string? reason, string userId)
    {
        var c = await cases.GetByIdAsync(id);
        if (c is null || c.IsDeleted) return Err("Case not found.");
        if (c.Status is DisciplinaryStatus.Closed or DisciplinaryStatus.Withdrawn)
            return new DisciplineActionResult("NoChange", $"{c.CaseNumber} is already {Spaced(c.Status).ToLowerInvariant()}.", c.Id);

        var withdrawing = c.Status is DisciplinaryStatus.Open or DisciplinaryStatus.ShowCauseIssued;
        if (withdrawing && string.IsNullOrWhiteSpace(reason))
            return Err("Withdrawing a case before its outcome needs a reason.");
        if (c.Status == DisciplinaryStatus.Appealed)
            return Err("The appeal has not been decided — decide it before closing the case.");

        c.Status = withdrawing ? DisciplinaryStatus.Withdrawn : DisciplinaryStatus.Closed;
        c.ClosedAt = DateTime.UtcNow;
        c.ClosedBy = userId;
        c.OutcomeNotes = Append(c.OutcomeNotes, string.IsNullOrWhiteSpace(reason) ? "Closed." : $"Closed: {reason.Trim()}");
        Touch(c, userId);
        await cases.UpdateAsync(c);

        await LogAsync("DisciplinaryCase", c.Id, HrAuditAction.DisciplinaryCaseClosed,
            $"{c.CaseNumber} {Spaced(c.Status).ToLowerInvariant()}.{(string.IsNullOrWhiteSpace(reason) ? "" : $" {reason.Trim()}")}", userId);
        return new DisciplineActionResult(c.Status.ToString(),
            $"{c.CaseNumber} {Spaced(c.Status).ToLowerInvariant()}.", c.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Warnings (P19)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<WarningRecordDto>> ListWarningsAsync(string? employeeId, bool includeExpired)
    {
        var q = warnings.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(w => w.EmployeeId == employeeId);
        if (!includeExpired) q = q.Where(w => w.IsActive);

        var list = await q.OrderByDescending(w => w.IssuedDate).ToListAsync();
        if (list.Count == 0) return [];

        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddMonths(-WarningWindowMonths);
        var ids = list.Select(w => w.EmployeeId).Distinct().ToList();
        var liveCounts = await warnings.Query().AsNoTracking()
            .Where(w => ids.Contains(w.EmployeeId) && w.IsActive && w.IssuedDate >= windowStart)
            .GroupBy(w => w.EmployeeId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return list.Select(w => new WarningRecordDto
        {
            Id = w.Id, EmployeeId = w.EmployeeId, EmployeeNumber = w.EmployeeNumber, EmployeeName = w.EmployeeName,
            DisciplinaryCaseId = w.DisciplinaryCaseId, WarningType = w.WarningType.ToString(),
            IncidentDate = w.IncidentDate, IssuedDate = w.IssuedDate, ExpiryDate = w.ExpiryDate,
            Reason = w.Reason, IsActive = w.IsActive,
            AcknowledgedByEmployee = w.AcknowledgedByEmployee, AcknowledgedAt = w.AcknowledgedAt,
            DaysToExpiry = (int)(w.ExpiryDate.Date - today).TotalDays,
            ActiveInLast12Months = liveCounts.GetValueOrDefault(w.EmployeeId),
        }).ToList();
    }

    public async Task<DisciplineActionResult> IssueWarningAsync(IssueWarningDto dto, string? tenantSchema, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!Enum.TryParse<WarningType>(dto.WarningType, true, out var type))
            return Err("The warning type must be Verbal, Written or FinalWritten.");

        var incident = DateTime.SpecifyKind((dto.IncidentDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        if (incident > DateTime.UtcNow.Date) return Err("An incident cannot be dated in the future.");

        var warning = await CreateWarningAsync(employee.Id, type, incident, dto.Reason, dto.DisciplinaryCaseId, userId);

        var result = new DisciplineActionResult("Issued",
            $"{Spaced(type)} warning issued to {employee.FullName}, standing until {warning.ExpiryDate:dd MMM yyyy}.", warning.Id);
        result.Warnings.Add("The employee must acknowledge it — an unacknowledged warning is weaker evidence if it is ever relied on.");

        var live = await CountLiveWarningsAsync(employee.Id);
        if (live >= WarningEscalationThreshold)
        {
            result.Warnings.Add($"{employee.FullName} now has {live} live warnings in 12 months — the termination-review threshold.");
            await NotifyAsync(tenantSchema, "Critical",
                $"Termination review threshold reached — {employee.FullName}",
                $"{employee.EmployeeNumber} {employee.FullName} has {live} live warnings within a rolling 12 months (HR-022). A termination review is required.",
                "hr.approve");
        }
        return result;
    }

    public async Task<DisciplineActionResult> AcknowledgeWarningAsync(string id, AcknowledgeWarningDto dto, string userId)
    {
        var w = await warnings.GetByIdAsync(id);
        if (w is null || w.IsDeleted) return Err("Warning not found.");
        if (w.AcknowledgedByEmployee)
            return new DisciplineActionResult("NoChange", "That warning has already been acknowledged.", w.Id);

        // Only the employee it concerns may acknowledge it — HR ticking the box on someone's behalf would make
        // the acknowledgement worthless as evidence.
        var me = await employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
        if (me is null || me.Id != w.EmployeeId)
            return Err("A warning can only be acknowledged by the employee it concerns.");

        w.AcknowledgedByEmployee = true;
        w.AcknowledgedAt = DateTime.UtcNow;
        w.AcknowledgementNote = dto.Note;
        Touch(w, userId);
        await warnings.UpdateAsync(w);

        await LogAsync("WarningRecord", w.Id, HrAuditAction.WarningAcknowledged,
            $"{w.EmployeeNumber} acknowledged their {w.WarningType} warning of {w.IssuedDate:dd MMM yyyy}.", userId);
        return new DisciplineActionResult("Acknowledged", "Warning acknowledged.", w.Id);
    }

    private async Task<WarningRecord> CreateWarningAsync(string employeeId, WarningType type, DateTime incident,
        string? reason, string? caseId, string userId)
    {
        var employee = await employees.Query().AsNoTracking().FirstAsync(e => e.Id == employeeId);
        var issued = DateTime.UtcNow.Date;
        var months = type == WarningType.Verbal ? VerbalWarningMonths : WrittenWarningMonths;

        var warning = await warnings.CreateAsync(new WarningRecord
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DisciplinaryCaseId = caseId,
            WarningType = type,
            IncidentDate = incident,
            IssuedDate = DateTime.SpecifyKind(issued, DateTimeKind.Utc),
            ExpiryDate = DateTime.SpecifyKind(issued.AddMonths(months), DateTimeKind.Utc),
            Reason = reason,
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("WarningRecord", warning.Id, HrAuditAction.WarningIssued,
            $"{employee.EmployeeNumber}: {type} warning issued, expires {warning.ExpiryDate:dd MMM yyyy}. {reason}", userId);
        return warning;
    }

    private async Task<int> CountLiveWarningsAsync(string employeeId)
    {
        var windowStart = DateTime.UtcNow.Date.AddMonths(-WarningWindowMonths);
        return await warnings.Query().CountAsync(w => w.EmployeeId == employeeId && w.IsActive && w.IssuedDate >= windowStart);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Grievances (P20)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<GrievanceCaseDto>> ListGrievancesAsync(string? status, string? employeeId)
    {
        var q = grievances.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(g => g.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GrievanceStatus>(status, true, out var st))
            q = q.Where(g => g.Status == st);
        var list = await q.OrderByDescending(g => g.SubmittedAt).ToListAsync();
        var today = DateTime.UtcNow.Date;
        return list.Select(g => ToDto(g, today)).ToList();
    }

    public async Task<DisciplineActionResult> SubmitGrievanceAsync(SubmitGrievanceDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (string.IsNullOrWhiteSpace(dto.Category)) return Err("A grievance needs a category.");
        if (string.IsNullOrWhiteSpace(dto.Description)) return Err("A grievance needs a description.");

        var created = await grievances.CreateAsync(new GrievanceCase
        {
            CaseNumber = await NextNumberAsync("GR", DateTime.UtcNow.Year),
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            Category = dto.Category.Trim(),
            Description = dto.Description.Trim(),
            IsConfidential = dto.IsConfidential,
            SubmittedAt = DateTime.UtcNow,
            // Two WORKING days — a grievance raised on a Friday is not late because of the weekend.
            AcknowledgementDeadline = await AddWorkingDaysAsync(DateTime.UtcNow.Date, GrievanceAckDays),
            CreatedBy = userId, UpdatedBy = userId,
        });

        await LogAsync("GrievanceCase", created.Id, HrAuditAction.GrievanceSubmitted,
            $"{created.CaseNumber} submitted by {employee.EmployeeNumber} ({created.Category}).", userId);
        return new DisciplineActionResult("Submitted",
            $"Grievance {created.CaseNumber} received. HR will acknowledge by {created.AcknowledgementDeadline:dd MMM yyyy}.", created.Id);
    }

    public async Task<DisciplineActionResult> AcknowledgeGrievanceAsync(string id, string userId, string? userName)
    {
        var g = await grievances.GetByIdAsync(id);
        if (g is null || g.IsDeleted) return Err("Grievance not found.");
        if (g.AcknowledgedAt is not null)
            return new DisciplineActionResult("NoChange", $"{g.CaseNumber} was already acknowledged.", g.Id);

        g.AcknowledgedAt = DateTime.UtcNow;
        g.AcknowledgedBy = userId;
        if (g.Status == GrievanceStatus.Submitted) g.Status = GrievanceStatus.Acknowledged;
        Touch(g, userId);
        await grievances.UpdateAsync(g);

        var result = new DisciplineActionResult("Acknowledged", $"{g.CaseNumber} acknowledged.", g.Id);
        if (g.AcknowledgementDeadline is not null && DateTime.UtcNow.Date > g.AcknowledgementDeadline.Value.Date)
            result.Warnings.Add($"This was acknowledged after the {g.AcknowledgementDeadline:dd MMM yyyy} deadline — the breach stays on the record.");

        await LogAsync("GrievanceCase", g.Id, HrAuditAction.GrievanceAcknowledged,
            $"{g.CaseNumber} acknowledged by {userName ?? userId}.", userId, userName);
        return result;
    }

    public async Task<DisciplineActionResult> AssignGrievanceAsync(string id, AssignGrievanceDto dto, string userId)
    {
        var g = await grievances.GetByIdAsync(id);
        if (g is null || g.IsDeleted) return Err("Grievance not found.");
        if (g.Status is GrievanceStatus.Resolved or GrievanceStatus.Withdrawn)
            return Err($"{g.CaseNumber} is closed.");

        var investigator = await employees.GetByIdAsync(dto.InvestigatorEmployeeId ?? string.Empty);
        if (investigator is null || investigator.IsDeleted) return Err("Investigator not found.");
        // The person complained about, or complaining, cannot investigate it.
        if (investigator.Id == g.EmployeeId)
            return Err("Someone cannot investigate their own grievance.");

        g.AssignedToEmployeeId = investigator.Id;
        g.AssignedToName = investigator.FullName;
        g.AssignedAt = DateTime.UtcNow;
        g.Status = GrievanceStatus.Investigating;
        g.AcknowledgedAt ??= DateTime.UtcNow;
        Touch(g, userId);
        await grievances.UpdateAsync(g);

        await LogAsync("GrievanceCase", g.Id, HrAuditAction.GrievanceAssigned,
            $"{g.CaseNumber} assigned to {investigator.FullName} for investigation.", userId);
        return new DisciplineActionResult("Assigned", $"{g.CaseNumber} assigned to {investigator.FullName}.", g.Id);
    }

    public async Task<DisciplineActionResult> ResolveGrievanceAsync(string id, ResolveGrievanceDto dto, string? tenantSchema, string userId, string? userName)
    {
        var g = await grievances.GetByIdAsync(id);
        if (g is null || g.IsDeleted) return Err("Grievance not found.");
        if (g.Status is GrievanceStatus.Resolved or GrievanceStatus.Withdrawn)
            return Err($"{g.CaseNumber} is already closed.");
        if (!Enum.TryParse<GrievanceStatus>(dto.Outcome, true, out var outcome)
            || outcome is not (GrievanceStatus.Resolved or GrievanceStatus.PartiallyResolved or GrievanceStatus.Escalated))
            return Err("The outcome must be Resolved, PartiallyResolved or Escalated.");
        if (string.IsNullOrWhiteSpace(dto.OutcomeNotes)) return Err("An outcome needs to say what was decided.");
        if (outcome == GrievanceStatus.PartiallyResolved && string.IsNullOrWhiteSpace(dto.FollowUpActions))
            return Err("A partial resolution needs the follow-up actions — otherwise it is just an unresolved grievance with a nicer label.");

        g.InvestigationFindings = dto.Findings ?? g.InvestigationFindings;
        g.Outcome = dto.OutcomeNotes.Trim();
        g.FollowUpActions = dto.FollowUpActions;
        g.Status = outcome;

        if (outcome == GrievanceStatus.Escalated)
        {
            g.EscalatedAt = DateTime.UtcNow;
            await NotifyAsync(tenantSchema, "Critical",
                $"Grievance escalated — {g.CaseNumber}",
                $"{g.CaseNumber} ({g.Category}) could not be resolved and needs a formal hearing. {g.Outcome}",
                "hr.approve");
        }
        else
        {
            g.ResolvedAt = DateTime.UtcNow;
            g.ResolvedBy = userId;
        }
        Touch(g, userId);
        await grievances.UpdateAsync(g);

        await LogAsync("GrievanceCase", g.Id,
            outcome == GrievanceStatus.Escalated ? HrAuditAction.GrievanceEscalated : HrAuditAction.GrievanceResolved,
            $"{g.CaseNumber}: {Spaced(outcome)} by {userName ?? userId}. {g.Outcome}", userId, userName);

        return new DisciplineActionResult(outcome.ToString(),
            outcome == GrievanceStatus.Escalated
                ? $"{g.CaseNumber} escalated to the MD for a formal hearing."
                : $"{g.CaseNumber} closed as {Spaced(outcome).ToLowerInvariant()}.", g.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Separation and final dues (P21)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<SeparationDto>> ListSeparationsAsync(string? status, string? employeeId)
    {
        var q = separations.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(s => s.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SeparationStatus>(status, true, out var st))
            q = q.Where(s => s.Status == st);
        var list = await q.OrderByDescending(s => s.EffectiveDate).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<SeparationDto?> GetSeparationAsync(string id)
    {
        var s = await separations.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? null : ToDto(s);
    }

    public async Task<DisciplineActionResult> InitiateSeparationAsync(InitiateSeparationDto dto, string userId, CancellationToken ct = default)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!Active.Contains(employee.Status))
            return Err($"{employee.FullName} is already {employee.Status}.");
        if (!Enum.TryParse<SeparationType>(dto.SeparationType, true, out var type))
            return Err("The type must be Resignation, Termination, Retirement, EndOfContract or Death.");

        var effective = DateTime.SpecifyKind(dto.EffectiveDate.Date, DateTimeKind.Utc);
        if (effective < employee.HireDate.Date) return Err("The leaving date cannot precede the hire date.");
        var notice = DateTime.SpecifyKind((dto.NoticeDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        if (effective < notice && !dto.NoticeWaived)
            return Err("The leaving date is before notice was given — either correct the dates or mark the notice as waived.");

        if (await separations.Query().AnyAsync(s => s.EmployeeId == employee.Id && s.Status != SeparationStatus.Cancelled, ct))
            return Err($"{employee.FullName} already has a separation in progress.");

        // ── The final dues (P21 step 21.2) ──
        var salary = await salaries.Query().AsNoTracking()
            .Where(s => s.EmployeeId == employee.Id && s.Status == SalaryAssignmentStatus.Approved)
            .OrderByDescending(s => s.EffectiveFromPeriodCode).FirstOrDefaultAsync(ct);

        var notesSeed = new List<string>();
        var monthly = salary?.BasicSalary ?? 0m;
        var daily = monthly > 0 ? Round(monthly / WorkingDaysPerMonth) : 0m;

        // Unused leave, balance derived exactly as H3 derives it (entitled minus taken).
        // ONLY leave types that CARRY FORWARD are paid out. That is the line between leave the employee owns
        // and leave that exists for an event: nobody is owed cash for the maternity or sick days they did not
        // need. Paying every type out would have turned 21 annual days into 142.
        var payableTypes = await leaveTypes.Query().AsNoTracking()
            .Where(t => t.CarriesForward).Select(t => t.Id).ToListAsync(ct);
        var leaveRows = await entitlements.Query().AsNoTracking()
            .Where(e => e.EmployeeId == employee.Id && e.Year == effective.Year
                     && payableTypes.Contains(e.LeaveTypeId)).ToListAsync(ct);
        var leaveBalance = Round(leaveRows.Sum(e => e.DaysEntitled - e.DaysTaken));
        if (leaveBalance < 0) leaveBalance = 0m;    // an overdrawn balance is not a debt to claw back here
        if (payableTypes.Count == 0)
            notesSeed.Add("No leave type is marked as carrying forward, so no leave payout was computed — check the leave configuration.");

        var daysWorked = await calendar.CountWorkingDaysAsync(
            new DateTime(effective.Year, effective.Month, 1, 0, 0, 0, DateTimeKind.Utc), effective);
        var noticePay = dto.NoticeWaived ? Round(daily * (dto.NoticePeriodDays ?? 30)) : 0m;

        // Outstanding advances — read where finance can be reached, and SAID SO where it cannot.
        var notes = notesSeed;
        decimal advanceRecovery = 0m;
        var advances = string.IsNullOrWhiteSpace(employee.UserId)
            ? null
            : await finance.ListOutstandingAdvancesAsync(employee.UserId!, ct);
        if (advances is null)
            notes.Add(string.IsNullOrWhiteSpace(employee.UserId)
                ? "This employee has no login account, so outstanding advances could not be looked up in finance — check manually before approving."
                : "Finance could not be read, so outstanding advances are UNKNOWN and set to zero — check manually before approving.");
        else if (advances.Count == 0) notes.Add("Finance reports no outstanding advances.");
        else
        {
            advanceRecovery = Round(advances.Sum(a => a.Amount));
            notes.Add($"Finance reports {advances.Count} outstanding item(s): {string.Join("; ", advances.Select(a => $"{a.Kind} {a.Reference} {a.Amount:N2}"))}.");
        }

        var created = await separations.CreateAsync(new Separation
        {
            SeparationNumber = await NextNumberAsync("SEP", effective.Year),
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            HireDate = employee.HireDate,
            SeparationType = type,
            NoticeDate = notice,
            EffectiveDate = effective,
            NoticePeriodDays = dto.NoticePeriodDays ?? 30,
            NoticeWaived = dto.NoticeWaived,
            Reason = dto.Reason,
            SourceModule = dto.SourceModule,
            SourceReference = dto.SourceReference,
            MonthlySalary = monthly,
            DailyRate = daily,
            LeaveDaysBalance = leaveBalance,
            LeavePayout = Round(leaveBalance * daily),
            FinalMonthDaysWorked = daysWorked,
            ProRataSalary = Round(daysWorked * daily),
            NoticePay = noticePay,
            AdvanceRecovery = advanceRecovery,
            CurrencyCode = salary?.CurrencyCode ?? "KES",
            DuesNotes = string.Join(" ", notes),
            CreatedBy = userId, UpdatedBy = userId,
        });
        created.NetDues = NetOf(created);
        await separations.UpdateAsync(created);

        var result = new DisciplineActionResult("Initiated",
            $"{created.SeparationNumber} opened for {employee.FullName}, leaving {effective:dd MMM yyyy}. Net dues {Money(created.NetDues, created.CurrencyCode)}.", created.Id);
        result.Warnings.AddRange(notes);
        if (salary is null)
            result.Warnings.Add("No approved salary is on file, so every money figure here is zero — set one before approving.");

        await LogAsync("Separation", created.Id, HrAuditAction.SeparationInitiated,
            $"{created.SeparationNumber}: {type} for {employee.EmployeeNumber} effective {effective:dd MMM yyyy}. Leave {leaveBalance:0.##}d, pro-rata {created.ProRataSalary:N2}, notice {noticePay:N2}, advances {advanceRecovery:N2}, net {created.NetDues:N2}.", userId);
        return result;
    }

    public async Task<DisciplineActionResult> AdjustDuesAsync(string id, AdjustDuesDto dto, string userId)
    {
        var s = await separations.GetByIdAsync(id);
        if (s is null || s.IsDeleted) return Err("Separation not found.");
        if (s.Status is SeparationStatus.Paid or SeparationStatus.Cancelled)
            return Err($"{s.SeparationNumber} is {Spaced(s.Status).ToLowerInvariant()} — its figures are frozen.");
        if (s.Status == SeparationStatus.Approved)
            return Err($"{s.SeparationNumber} has been approved — changing the dues now would pay a figure nobody approved. Cancel and re-open it instead.");
        if (dto.OtherEarnings is < 0 || dto.AdvanceRecovery is < 0 || dto.OtherDeductions is < 0)
            return Err("Dues components cannot be negative — use the right line rather than a negative one.");

        if (dto.OtherEarnings.HasValue) s.OtherEarnings = dto.OtherEarnings.Value;
        if (dto.AdvanceRecovery.HasValue) s.AdvanceRecovery = dto.AdvanceRecovery.Value;
        if (dto.OtherDeductions.HasValue) s.OtherDeductions = dto.OtherDeductions.Value;
        if (!string.IsNullOrWhiteSpace(dto.Notes)) s.DuesNotes = Append(s.DuesNotes, dto.Notes.Trim());
        if (!string.IsNullOrWhiteSpace(dto.ExitInterviewNotes)) s.ExitInterviewNotes = dto.ExitInterviewNotes;
        s.NetDues = NetOf(s);
        Touch(s, userId);
        await separations.UpdateAsync(s);

        var result = new DisciplineActionResult("Updated",
            $"{s.SeparationNumber}: net dues now {Money(s.NetDues, s.CurrencyCode)}.", s.Id);
        if (s.NetDues < 0)
            result.Warnings.Add("The net is negative — the employee owes the company. That is a debt to recover, not a payment to make.");
        return result;
    }

    public async Task<DisciplineActionResult> SubmitSeparationAsync(string id, string userId)
    {
        var s = await separations.GetByIdAsync(id);
        if (s is null || s.IsDeleted) return Err("Separation not found.");
        if (s.Status != SeparationStatus.Draft)
            return Err($"{s.SeparationNumber} is {Spaced(s.Status).ToLowerInvariant()} — only a draft can be sent for approval.");
        if (s.MonthlySalary <= 0)
            return Err("There is no salary on file, so the dues are all zero. Set the salary before sending this to the MD.");

        s.Status = SeparationStatus.PendingMd;
        s.SubmittedBy = userId;
        s.SubmittedAt = DateTime.UtcNow;
        Touch(s, userId);
        await separations.UpdateAsync(s);

        return new DisciplineActionResult("Submitted",
            $"{s.SeparationNumber} sent to the MD — {Money(s.NetDues, s.CurrencyCode)} for approval.", s.Id);
    }

    public async Task<DisciplineActionResult> DecideSeparationAsync(string id, DecideSeparationDto dto, string userId, string? userName)
    {
        var s = await separations.GetByIdAsync(id);
        if (s is null || s.IsDeleted) return Err("Separation not found.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var cancel = decision.Equals("Cancel", StringComparison.OrdinalIgnoreCase);
        if (!approve && !cancel) return Err("The decision must be Approve or Cancel.");

        if (cancel)
        {
            if (s.Status == SeparationStatus.Paid)
                return Err($"{s.SeparationNumber} has been paid — it cannot be cancelled.");
            if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("Cancelling a separation needs a reason.");
            s.Status = SeparationStatus.Cancelled;
            s.CancellationReason = dto.Reason.Trim();
            Touch(s, userId);
            await separations.UpdateAsync(s);

            await LogAsync("Separation", s.Id, HrAuditAction.SeparationCancelled,
                $"{s.SeparationNumber} cancelled by {userName ?? userId}: {s.CancellationReason}", userId, userName);
            return new DisciplineActionResult("Cancelled",
                $"{s.SeparationNumber} cancelled. {s.EmployeeName} remains employed.", s.Id);
        }

        if (s.Status != SeparationStatus.PendingMd)
            return Err($"{s.SeparationNumber} is {Spaced(s.Status).ToLowerInvariant()} — only one awaiting the MD can be approved.");
        // Whoever computed the dues must not approve them.
        if (!string.IsNullOrWhiteSpace(s.SubmittedBy) && s.SubmittedBy == userId)
            return Err("The person who prepared a separation cannot approve its final dues — it needs the MD.");

        s.Status = SeparationStatus.Approved;
        s.ApprovedBy = userId;
        s.ApprovedAt = DateTime.UtcNow;
        Touch(s, userId);
        await separations.UpdateAsync(s);

        var result = new DisciplineActionResult("Approved",
            $"{s.SeparationNumber} approved at {Money(s.NetDues, s.CurrencyCode)}. Record the payment to complete it.", s.Id);
        result.Warnings.Add($"{s.EmployeeName} stays active on the payroll until the payment is recorded — deactivating now would drop them from a run that still owes them money.");

        await LogAsync("Separation", s.Id, HrAuditAction.SeparationApproved,
            $"{s.SeparationNumber} approved by {userName ?? userId} at {s.NetDues:N2}.", userId, userName);
        return result;
    }

    public async Task<DisciplineActionResult> PaySeparationAsync(string id, PaySeparationDto dto, string? tenantSchema, string userId, string? userName)
    {
        var s = await separations.GetByIdAsync(id);
        if (s is null || s.IsDeleted) return Err("Separation not found.");
        if (s.Status != SeparationStatus.Approved)
            return Err($"{s.SeparationNumber} is {Spaced(s.Status).ToLowerInvariant()} — only an approved separation can be paid.");

        s.Status = SeparationStatus.Paid;
        s.PaidBy = userId;
        s.PaidAt = DateTime.UtcNow;
        s.PaymentReference = dto.PaymentReference;
        if (dto.IssueCertificate) s.CertificateIssuedAt = DateTime.UtcNow;
        Touch(s, userId);
        await separations.UpdateAsync(s);

        // P21 step 21.5 — NOW the employee leaves the payroll, not before.
        var employee = await employees.GetByIdAsync(s.EmployeeId);
        if (employee is not null)
        {
            employee.Status = s.SeparationType switch
            {
                SeparationType.Resignation => EmploymentStatus.Resigned,
                SeparationType.Termination => EmploymentStatus.Terminated,
                _ => EmploymentStatus.Resigned,
            };
            employee.ExitDate = s.EffectiveDate;
            Touch(employee, userId);
            await employees.UpdateAsync(employee);
        }

        var result = new DisciplineActionResult("Paid",
            $"{s.SeparationNumber} paid. {s.EmployeeName} is now {employee?.Status.ToString().ToLowerInvariant() ?? "inactive"}.", s.Id);
        if (dto.IssueCertificate) result.Warnings.Add("Certificate of Service recorded as issued — attach the document to the employee's vault.");
        // HR cannot revoke a login; that lives in user-service. Say so rather than implying it happened.
        result.Warnings.Add("System access is NOT revoked by this step — disable the login in user administration separately.");

        await NotifyAsync(tenantSchema, "Info", $"Separation completed — {s.EmployeeName} ({s.SeparationNumber})",
            $"{s.EmployeeNumber} {s.EmployeeName} left on {s.EffectiveDate:dd MMM yyyy}. Final dues {Money(s.NetDues, s.CurrencyCode)} paid. Their system access still needs revoking.",
            "hr.write");
        await LogAsync("Separation", s.Id, HrAuditAction.SeparationPaid,
            $"{s.SeparationNumber} paid by {userName ?? userId}: {s.NetDues:N2}, ref {s.PaymentReference}. Employee set to {employee?.Status}.", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // The daily sweep
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<DisciplineSweepResultDto> RunDisciplineSweepAsync(string? tenantSchema, string userId)
    {
        var result = new DisciplineSweepResultDto();
        var today = DateTime.UtcNow.Date;

        // ── P19 step 19.2a — warnings past their expiry ──
        var expiring = await warnings.Query().Where(w => w.IsActive && w.ExpiryDate < today).ToListAsync();
        foreach (var w in expiring)
        {
            w.IsActive = false;
            w.ExpiredAt = DateTime.UtcNow;
            Touch(w, userId);
            await warnings.UpdateAsync(w);
            result.WarningsExpired++;
            await LogAsync("WarningRecord", w.Id, HrAuditAction.WarningExpired,
                $"{w.EmployeeNumber}: {w.WarningType} warning of {w.IssuedDate:dd MMM yyyy} expired.", userId);
        }

        // ── P19 step 19.2c — three live warnings in a rolling twelve months ──
        var windowStart = today.AddMonths(-WarningWindowMonths);
        var live = await warnings.Query().AsNoTracking()
            .Where(w => w.IsActive && w.IssuedDate >= windowStart).ToListAsync();
        foreach (var group in live.GroupBy(w => w.EmployeeId).Where(g => g.Count() >= WarningEscalationThreshold))
        {
            var latest = group.OrderByDescending(w => w.IssuedDate).First();
            // Keyed on the newest warning, so crossing the threshold AGAIN with a further warning re-alerts,
            // while the same standing set does not.
            var key = $"escalation:{group.Key}:{latest.Id}";
            if (await audit.Query().AnyAsync(a => a.Action == HrAuditAction.WarningEscalation && a.EntityId == key)) continue;

            result.WarningEscalations++;
            await NotifyAsync(tenantSchema, "Critical",
                $"Termination review — {latest.EmployeeName} has {group.Count()} live warnings",
                $"{latest.EmployeeNumber} {latest.EmployeeName} has {group.Count()} live warnings within a rolling 12 months (HR-022). A termination review is required.",
                "hr.approve");
            await LogAsync("WarningRecord", key, HrAuditAction.WarningEscalation,
                $"{latest.EmployeeNumber}: {group.Count()} live warnings in 12 months — termination review raised.", userId);
        }

        // ── P18 — show-cause windows that have lapsed with no response ──
        var overdue = await cases.Query()
            .Where(c => c.Status == DisciplinaryStatus.ShowCauseIssued
                     && c.ResponseDeadline != null && c.ResponseDeadline < today
                     && c.EmployeeRespondedAt == null && c.ResponseOverdueAlertedAt == null).ToListAsync();
        foreach (var c in overdue)
        {
            c.ResponseOverdueAlertedAt = DateTime.UtcNow;
            Touch(c, userId);
            await cases.UpdateAsync(c);
            result.ShowCauseOverdueAlerts++;
            await NotifyAsync(tenantSchema, "Warning",
                $"Show-cause response overdue — {c.CaseNumber}, {c.EmployeeName}",
                $"{c.EmployeeNumber} {c.EmployeeName} did not respond to {c.CaseNumber} by {c.ResponseDeadline:dd MMM yyyy}. The hearing may proceed on the papers.",
                "hr.manager");
        }

        // ── P20 step 20.2 — grievances past the two-working-day acknowledgement SLA ──
        var breached = await grievances.Query()
            .Where(g => g.AcknowledgedAt == null && g.SlaBreachAlertedAt == null
                     && g.AcknowledgementDeadline != null && g.AcknowledgementDeadline < today
                     && g.Status != GrievanceStatus.Withdrawn).ToListAsync();
        foreach (var g in breached)
        {
            g.SlaBreachAlertedAt = DateTime.UtcNow;
            Touch(g, userId);
            await grievances.UpdateAsync(g);
            result.GrievanceSlaBreaches++;
            await NotifyAsync(tenantSchema, "Critical",
                $"Grievance not acknowledged in time — {g.CaseNumber}",
                $"{g.CaseNumber} ({g.Category}) was raised on {g.SubmittedAt:dd MMM yyyy} and should have been acknowledged by {g.AcknowledgementDeadline:dd MMM yyyy} (HR-023).",
                "hr.approve");
        }

        if (result.WarningsExpired + result.WarningEscalations + result.ShowCauseOverdueAlerts + result.GrievanceSlaBreaches == 0)
            result.Notes.Add("Nothing new to raise.");
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Net = what was earned, less what is being recovered. Kept in one place so every path that
    /// touches a component recomputes it the same way.</summary>
    private static decimal NetOf(Separation s)
        => Round(s.LeavePayout + s.ProRataSalary + s.NoticePay + s.OtherEarnings - s.AdvanceRecovery - s.OtherDeductions);

    /// <summary>Adds working days on the shared calendar — the same rule leave and attendance use.</summary>
    private async Task<DateTime> AddWorkingDaysAsync(DateTime from, int days)
    {
        var date = from.Date;
        var added = 0;
        // Bounded so a misconfigured calendar with no working days cannot spin forever.
        for (var guard = 0; added < days && guard < days * 10 + 30; guard++)
        {
            date = date.AddDays(1);
            if (await calendar.IsWorkingDayAsync(date)) added++;
        }
        return DateTime.SpecifyKind(date, DateTimeKind.Utc);
    }

    /// <summary>Sequential reference per prefix per year. Derived from the highest issued, not a row count —
    /// the same trap that broke finance's journal numbering.</summary>
    private async Task<string> NextNumberAsync(string prefix, int year)
    {
        var pattern = $"{prefix}-{year}-";
        var issued = prefix switch
        {
            "DC" => await cases.Query().IgnoreQueryFilters().Where(c => c.CaseNumber.StartsWith(pattern)).Select(c => c.CaseNumber).ToListAsync(),
            "GR" => await grievances.Query().IgnoreQueryFilters().Where(g => g.CaseNumber.StartsWith(pattern)).Select(g => g.CaseNumber).ToListAsync(),
            _ => await separations.Query().IgnoreQueryFilters().Where(s => s.SeparationNumber.StartsWith(pattern)).Select(s => s.SeparationNumber).ToListAsync(),
        };
        var highest = issued
            .Select(n => int.TryParse(n[pattern.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        return $"{pattern}{highest + 1:D4}";
    }

    private static DisciplinaryCaseDto ToDto(DisciplinaryCase c, DateTime today) => new()
    {
        Id = c.Id, CaseNumber = c.CaseNumber,
        EmployeeId = c.EmployeeId, EmployeeNumber = c.EmployeeNumber, EmployeeName = c.EmployeeName,
        IncidentDate = c.IncidentDate, Description = c.Description, Witnesses = c.Witnesses,
        SourceModule = c.SourceModule, SourceReference = c.SourceReference,
        Status = c.Status.ToString(),
        ShowCauseIssuedAt = c.ShowCauseIssuedAt, ShowCauseLetter = c.ShowCauseLetter,
        ResponseDeadline = c.ResponseDeadline, EmployeeRespondedAt = c.EmployeeRespondedAt,
        EmployeeResponse = c.EmployeeResponse,
        HearingDate = c.HearingDate, HearingPanel = c.HearingPanel, HearingNotes = c.HearingNotes,
        Outcome = c.Outcome.ToString(), OutcomeNotes = c.OutcomeNotes, OutcomeRecordedAt = c.OutcomeRecordedAt,
        RightOfAppealDeadline = c.RightOfAppealDeadline, AppealSubmittedAt = c.AppealSubmittedAt,
        AppealGrounds = c.AppealGrounds, AppealOutcome = c.AppealOutcome, ClosedAt = c.ClosedAt,
        WarningRecordId = c.WarningRecordId, SeparationId = c.SeparationId,
        ResponseOverdue = c.Status == DisciplinaryStatus.ShowCauseIssued
                       && c.ResponseDeadline is not null && c.ResponseDeadline.Value.Date < today
                       && c.EmployeeRespondedAt is null,
        NextStep = c.Status switch
        {
            DisciplinaryStatus.Open => "Issue the show-cause letter.",
            DisciplinaryStatus.ShowCauseIssued => c.ResponseDeadline is not null && c.ResponseDeadline.Value.Date < today
                ? "The response window has lapsed — record that and set a hearing."
                : $"Awaiting the employee's response by {c.ResponseDeadline:dd MMM yyyy}.",
            DisciplinaryStatus.AwaitingHearing => c.HearingDate is null ? "Set a hearing date." : "Record the hearing outcome.",
            DisciplinaryStatus.OutcomeRecorded => $"Right of appeal open to {c.RightOfAppealDeadline:dd MMM yyyy}.",
            DisciplinaryStatus.Appealed => "Decide the appeal — it needs someone other than whoever recorded the outcome.",
            _ => "Closed.",
        },
    };

    private static GrievanceCaseDto ToDto(GrievanceCase g, DateTime today) => new()
    {
        Id = g.Id, CaseNumber = g.CaseNumber,
        EmployeeId = g.EmployeeId, EmployeeNumber = g.EmployeeNumber, EmployeeName = g.EmployeeName,
        Category = g.Category, Description = g.Description, IsConfidential = g.IsConfidential,
        Status = g.Status.ToString(), SubmittedAt = g.SubmittedAt,
        AcknowledgementDeadline = g.AcknowledgementDeadline, AcknowledgedAt = g.AcknowledgedAt,
        AssignedToEmployeeId = g.AssignedToEmployeeId, AssignedToName = g.AssignedToName,
        InvestigationFindings = g.InvestigationFindings, Outcome = g.Outcome,
        FollowUpActions = g.FollowUpActions, ResolvedAt = g.ResolvedAt, EscalatedAt = g.EscalatedAt,
        SlaBreached = g.AcknowledgedAt is null && g.AcknowledgementDeadline is not null
                   && g.AcknowledgementDeadline.Value.Date < today,
        NextStep = g.Status switch
        {
            GrievanceStatus.Submitted => $"HR to acknowledge by {g.AcknowledgementDeadline:dd MMM yyyy}.",
            GrievanceStatus.Acknowledged => "Assign an investigator.",
            GrievanceStatus.Investigating => "Record the investigation outcome.",
            GrievanceStatus.PartiallyResolved => "Follow-up actions outstanding.",
            GrievanceStatus.Escalated => "Awaiting a formal hearing with the MD.",
            _ => "Closed.",
        },
    };

    private static SeparationDto ToDto(Separation s) => new()
    {
        Id = s.Id, SeparationNumber = s.SeparationNumber,
        EmployeeId = s.EmployeeId, EmployeeNumber = s.EmployeeNumber, EmployeeName = s.EmployeeName,
        HireDate = s.HireDate, SeparationType = s.SeparationType.ToString(),
        NoticeDate = s.NoticeDate, EffectiveDate = s.EffectiveDate,
        NoticePeriodDays = s.NoticePeriodDays, NoticeWaived = s.NoticeWaived, Reason = s.Reason,
        SourceModule = s.SourceModule, SourceReference = s.SourceReference,
        MonthlySalary = s.MonthlySalary, DailyRate = s.DailyRate,
        LeaveDaysBalance = s.LeaveDaysBalance, LeavePayout = s.LeavePayout,
        FinalMonthDaysWorked = s.FinalMonthDaysWorked, ProRataSalary = s.ProRataSalary,
        NoticePay = s.NoticePay, OtherEarnings = s.OtherEarnings,
        AdvanceRecovery = s.AdvanceRecovery, OtherDeductions = s.OtherDeductions,
        NetDues = s.NetDues, CurrencyCode = s.CurrencyCode, DuesNotes = s.DuesNotes,
        Status = s.Status.ToString(), SubmittedAt = s.SubmittedAt,
        ApprovedBy = s.ApprovedBy, ApprovedAt = s.ApprovedAt,
        PaidAt = s.PaidAt, PaymentReference = s.PaymentReference,
        CertificateIssuedAt = s.CertificateIssuedAt, ExitInterviewNotes = s.ExitInterviewNotes,
        CancellationReason = s.CancellationReason,
        YearsOfService = Math.Max(0, (int)((s.EffectiveDate - s.HireDate).TotalDays / 365.25)),
        NextStep = s.Status switch
        {
            SeparationStatus.Draft => "Check the dues, then send to the MD.",
            SeparationStatus.PendingMd => "Awaiting MD approval of the final dues.",
            SeparationStatus.Approved => "Record the payment — the employee stays active until then.",
            SeparationStatus.Paid => "Complete. Revoke system access separately.",
            _ => "Cancelled.",
        },
    };

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    /// <summary>"FinalWritten" → "Final written"; "PartiallyResolved" → "Partially resolved".</summary>
    private static string Spaced(Enum value)
    {
        var text = value.ToString();
        var chars = text.SelectMany((c, i) => i > 0 && char.IsUpper(c) ? [' ', char.ToLowerInvariant(c)] : new[] { c });
        return new string(chars.ToArray());
    }

    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";
    private static string Append(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";
    private static decimal Round(decimal value) => HrService.Core.Services.Money.Round(value);
    private static DisciplineActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName = null)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
