using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Payroll;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H8 (P13) — the salary increment workflow.
/// <para><b>The gates are checked twice, not once.</b> The DFD checks eligibility at proposal (step 13.2), and
/// that is right — nothing ineligible should ever reach the MD's queue. But a certification can expire, or a
/// mandatory training lapse, between the proposal and the signature. Re-checking at approval means the MD's
/// signature cannot become the thing that bypasses the control it was meant to sit behind.</para>
/// <para><b>The increment is the decision; the salary assignment is the pay.</b> They are separate records so
/// a rejected or withdrawn proposal leaves no mark on what anybody is actually paid. Approval writes the
/// assignment as APPROVED and supersedes the previous one — mirroring H5's semantics deliberately, because the
/// MD approving the increment IS the approval of the pay. Raising it as another proposal would ask the same
/// person to approve the same decision twice.</para>
/// <para>The H6 payroll run then applies it at the right month on its own, through
/// <c>EffectiveFromPeriodCode</c> — H8 writes no payroll logic of its own.</para>
/// </summary>
public class SalaryIncrementService(
    IGenericRepository<Employee> employees,
    IGenericRepository<EmployeeSalary> salaries,
    IGenericRepository<SalaryStructure> structures,
    IGenericRepository<JobGrade> grades,
    IGenericRepository<PayrollPeriod> periods,
    IGenericRepository<SalaryIncrement> increments,
    IGenericRepository<HrAuditLog> audit,
    ILearningService learning,
    IHrAlertGateway notifier) : ISalaryIncrementService
{
    private static readonly EmploymentStatus[] OnPayroll =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    public async Task<List<SalaryIncrementDto>> ListAsync(string? status, string? employeeId)
    {
        var q = increments.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(i => i.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SalaryIncrementStatus>(status, true, out var st))
            q = q.Where(i => i.Status == st);

        var list = await q.OrderByDescending(i => i.ProposedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<IncrementPreviewDto?> PreviewAsync(string employeeId, CancellationToken ct = default)
    {
        var employee = await employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return null;

        var dto = new IncrementPreviewDto
        {
            EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber, EmployeeName = employee.FullName,
        };

        var current = await CurrentSalaryAsync(employee.Id);
        if (current is not null)
        {
            dto.HasCurrentSalary = true;
            dto.CurrentSalary = current.BasicSalary;
            dto.CurrencyCode = current.CurrencyCode;
            dto.SalaryStructureName = current.SalaryStructureName;
            dto.CurrentPeriodCode = current.EffectiveFromPeriodCode;
        }

        var suggested = await SuggestPeriodAsync(current?.EffectiveFromPeriodCode);
        dto.SuggestedPeriodId = suggested?.Id;
        dto.SuggestedPeriodCode = suggested?.Code;

        var gate = await GateAsync(employee, ct);
        dto.Blockers = gate.Blockers;
        dto.Warnings = gate.Warnings;
        dto.HoursYtd = gate.HoursYtd;
        dto.TargetHours = gate.TargetHours;

        var pending = await increments.Query().AsNoTracking()
            .FirstOrDefaultAsync(i => i.EmployeeId == employee.Id && i.Status == SalaryIncrementStatus.PendingMd, ct);
        dto.PendingIncrementId = pending?.Id;

        if (!dto.HasCurrentSalary)
            dto.Blockers.Add("No approved salary is on file — set an initial salary before proposing a rise.");
        if (pending is not null)
            dto.Blockers.Add($"An increment to {Money(pending.ProposedSalary, pending.CurrencyCode)} is already with the MD.");
        if (suggested is null)
            dto.Blockers.Add("No open payroll period is available to take effect from — generate the year's periods first.");

        dto.Eligible = dto.Blockers.Count == 0;
        return dto;
    }

    public async Task<PayrollActionResult> ProposeAsync(ProposeIncrementDto dto, string userId, CancellationToken ct = default)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!OnPayroll.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and is no longer on the payroll.");

        var current = await CurrentSalaryAsync(employee.Id);
        if (current is null) return Err($"{employee.FullName} has no approved salary — set an initial salary before proposing a rise.");

        if (dto.ProposedSalary <= 0) return Err("The proposed salary must be greater than zero.");
        if (dto.ProposedSalary <= current.BasicSalary)
            return Err($"An increment must be an increase — {employee.FullName} is already on {Money(current.BasicSalary, current.CurrencyCode)}.");

        if (await increments.Query().AnyAsync(i => i.EmployeeId == employee.Id && i.Status == SalaryIncrementStatus.PendingMd, ct))
            return Err($"{employee.FullName} already has an increment awaiting the MD — settle that one first.");

        var period = string.IsNullOrWhiteSpace(dto.EffectivePeriodId)
            ? await SuggestPeriodAsync(current.EffectiveFromPeriodCode)
            : await periods.GetByIdAsync(dto.EffectivePeriodId!);
        if (period is null || period.IsDeleted)
            return Err(string.IsNullOrWhiteSpace(dto.EffectivePeriodId)
                ? "No open payroll period is available to take effect from — generate the year's periods first."
                : "Payroll period not found.");
        if (period.Status != PayrollPeriodStatus.Open)
            return Err($"Payroll period {period.Code} is {period.Status.ToString().ToLowerInvariant()} — pick one that is still open.");
        if (string.CompareOrdinal(period.Code, current.EffectiveFromPeriodCode ?? "") <= 0)
            return Err($"{employee.FullName} is on a salary effective from {current.EffectiveFromPeriodCode} — a rise must start in a later period.");

        // ── The two hard gates (P13 step 13.2). A blocked increment is refused, never written: nothing
        // ineligible should be sitting in the MD's queue waiting to be waved through.
        var gate = await GateAsync(employee, ct);
        if (gate.Blockers.Count > 0)
            return Err($"{employee.FullName} is not eligible for an increment. {string.Join(" ", gate.Blockers)}");

        var structure = await structures.GetByIdAsync(current.SalaryStructureId);

        var created = await increments.CreateAsync(new SalaryIncrement
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            CurrentSalary = current.BasicSalary,
            ProposedSalary = dto.ProposedSalary,
            CurrencyCode = current.CurrencyCode,
            CurrentEmployeeSalaryId = current.Id,
            SalaryStructureId = current.SalaryStructureId,
            SalaryStructureName = structure?.Name ?? current.SalaryStructureName,
            EffectivePeriodId = period.Id,
            EffectivePeriodCode = period.Code,
            Justification = dto.Justification,
            ProposedBy = userId,
            ProposedAt = DateTime.UtcNow,
            // Stored, not recomputed on read — the record should show what the proposer actually saw.
            EligibilityNotes = gate.Warnings.Count == 0
                ? $"Both gates clear at {DateTime.UtcNow:dd MMM yyyy}."
                : $"Gates clear at {DateTime.UtcNow:dd MMM yyyy}, with: {string.Join(" ", gate.Warnings)}",
            CreatedBy = userId, UpdatedBy = userId,
        });

        var increase = dto.ProposedSalary - current.BasicSalary;
        var result = new PayrollActionResult("Proposed",
            $"{employee.FullName}: {Money(current.BasicSalary, current.CurrencyCode)} → {Money(dto.ProposedSalary, current.CurrencyCode)} " +
            $"({Percent(increase, current.BasicSalary)}) from {period.Code} — awaiting the MD.", created.Id);
        result.Warnings.AddRange(gate.Warnings);

        var band = await BandWarningAsync(structure, dto.ProposedSalary, current.CurrencyCode);
        if (band is not null) result.Warnings.Add(band);

        await LogAsync("SalaryIncrement", created.Id, HrAuditAction.SalaryIncrementProposed,
            $"{employee.EmployeeNumber}: {current.BasicSalary:N2} → {dto.ProposedSalary:N2} ({Percent(increase, current.BasicSalary)}) from {period.Code}. {created.EligibilityNotes}", userId);
        return result;
    }

    public async Task<PayrollActionResult> DecideAsync(string id, DecideIncrementDto dto, string? tenantSchema, string userId, string? userName, CancellationToken ct = default)
    {
        var increment = await increments.GetByIdAsync(id);
        if (increment is null || increment.IsDeleted) return Err("Increment not found.");
        if (increment.Status != SalaryIncrementStatus.PendingMd)
            return Err($"That increment is already {increment.Status.ToString().ToLowerInvariant()} — only one awaiting the MD can be decided.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
        var withdraw = decision.Equals("Withdraw", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject && !withdraw) return Err("The decision must be Approve, Reject or Withdraw.");
        var pastTense = reject ? "rejected" : withdraw ? "withdrawn" : "approved";
        if ((reject || withdraw) && string.IsNullOrWhiteSpace(dto.Reason))
            return Err($"A {(reject ? "rejection" : "withdrawal")} needs a reason.");

        if (reject || withdraw)
        {
            increment.Status = reject ? SalaryIncrementStatus.Rejected : SalaryIncrementStatus.Withdrawn;
            increment.DecidedBy = userId;
            increment.DecidedAt = DateTime.UtcNow;
            increment.DecisionReason = dto.Reason!.Trim();
            Touch(increment, userId);
            await increments.UpdateAsync(increment);

            await LogAsync("SalaryIncrement", increment.Id,
                reject ? HrAuditAction.SalaryIncrementRejected : HrAuditAction.SalaryIncrementWithdrawn,
                $"{increment.EmployeeNumber}: increment to {increment.ProposedSalary:N2} {pastTense} by {userName ?? userId}. {increment.DecisionReason}", userId, userName);
            return new PayrollActionResult(reject ? "Rejected" : "Withdrawn",
                $"Increment for {increment.EmployeeName} {pastTense}. Their pay is unchanged.", increment.Id);
        }

        // ── Approve ──
        var employee = await employees.GetByIdAsync(increment.EmployeeId);
        if (employee is null || employee.IsDeleted) return Err("The employee on this increment no longer exists.");
        if (!OnPayroll.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and is no longer on the payroll.");

        // The proposer must not be the approver — the same second-officer rule every other money decision in
        // HR carries.
        if (!string.IsNullOrWhiteSpace(increment.ProposedBy) && increment.ProposedBy == userId)
            return Err("The person who proposed an increment cannot approve it — it needs the MD.");

        // Re-check the gates. Eligibility can lapse between proposal and signature, and an approval that
        // skipped this would be exactly the bypass the gates exist to prevent.
        var gate = await GateAsync(employee, ct);
        if (gate.Blockers.Count > 0)
            return Err($"{employee.FullName} is no longer eligible, so this cannot be approved. {string.Join(" ", gate.Blockers)} Reject it, or clear the block and re-propose.");

        var period = await periods.GetByIdAsync(increment.EffectivePeriodId);
        if (period is null) return Err("The effective payroll period no longer exists.");
        if (period.Status == PayrollPeriodStatus.Closed)
            return Err($"Payroll period {period.Code} is closed — that month has been paid. Re-propose from an open period.");

        // The current assignment may have moved since the proposal was raised.
        var current = await CurrentSalaryAsync(employee.Id);
        if (current is null) return Err($"{employee.FullName} no longer has an approved salary to increment.");
        if (string.CompareOrdinal(increment.EffectivePeriodCode ?? "", current.EffectiveFromPeriodCode ?? "") <= 0)
            return Err($"{employee.FullName} has since moved to a salary effective from {current.EffectiveFromPeriodCode}, which is not earlier than this increment's {increment.EffectivePeriodCode}. Re-propose from a later period.");

        // P13 step 13.3b — the new assignment, written APPROVED because the MD has just approved exactly this.
        var assignment = await salaries.CreateAsync(new EmployeeSalary
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            SalaryStructureId = increment.SalaryStructureId,
            SalaryStructureName = increment.SalaryStructureName,
            BasicSalary = increment.ProposedSalary,
            CurrencyCode = increment.CurrencyCode,
            EffectiveFromPeriodId = increment.EffectivePeriodId,
            EffectiveFromPeriodCode = increment.EffectivePeriodCode,
            Status = SalaryAssignmentStatus.Approved,
            ProposedBy = increment.ProposedBy,
            ProposedAt = increment.ProposedAt,
            ApprovedBy = userId,
            ApprovedAt = DateTime.UtcNow,
            Notes = $"Salary increment {increment.Id}." + (string.IsNullOrWhiteSpace(increment.Justification) ? "" : $" {increment.Justification}"),
            CreatedBy = userId, UpdatedBy = userId,
        });

        // Supersede whatever it replaces — the same semantics H5 uses, so pay history stays a chain.
        var superseded = await salaries.Query()
            .Where(s => s.EmployeeId == employee.Id && s.Id != assignment.Id && s.Status == SalaryAssignmentStatus.Approved)
            .ToListAsync(ct);
        foreach (var prior in superseded)
        {
            prior.Status = SalaryAssignmentStatus.Superseded;
            prior.SupersededById = assignment.Id;
            Touch(prior, userId);
            await salaries.UpdateAsync(prior);
        }

        increment.Status = SalaryIncrementStatus.Approved;
        increment.DecidedBy = userId;
        increment.DecidedAt = DateTime.UtcNow;
        increment.DecisionReason = dto.Reason?.Trim();
        increment.ResultingEmployeeSalaryId = assignment.Id;
        Touch(increment, userId);
        await increments.UpdateAsync(increment);

        var result = new PayrollActionResult("Approved",
            $"{employee.FullName} moves to {Money(increment.ProposedSalary, increment.CurrencyCode)} from {increment.EffectivePeriodCode}. " +
            "The payroll run for that month will apply it automatically.", increment.Id);
        if (superseded.Count > 0)
            result.Warnings.Add($"{superseded.Count} earlier assignment(s) superseded — kept as history, not deleted.");
        result.Warnings.AddRange(gate.Warnings);

        await LogAsync("SalaryIncrement", increment.Id, HrAuditAction.SalaryIncrementApproved,
            $"{employee.EmployeeNumber}: increment to {increment.ProposedSalary:N2} from {increment.EffectivePeriodCode} approved by {userName ?? userId}; salary assignment {assignment.Id} written.", userId, userName);

        // Addressed by schema, passed in explicitly rather than inferred — an alert nobody receives is worse
        // than no alert at all, because it looks like one was sent.
        if (!string.IsNullOrWhiteSpace(tenantSchema))
            await notifier.CreateAlertAsync(tenantSchema, "HR", "Info",
                $"Salary increment approved — {employee.FullName} from {increment.EffectivePeriodCode}",
                $"{employee.EmployeeNumber} {employee.FullName} moves from {Money(increment.CurrentSalary, increment.CurrencyCode)} to {Money(increment.ProposedSalary, increment.CurrencyCode)} effective {increment.EffectivePeriodCode}.",
                "hr.payroll.read");
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════

    private record Gate(List<string> Blockers, List<string> Warnings, decimal HoursYtd, int TargetHours);

    /// <summary>
    /// The HR-029 / HR-035 gates, taken from H7 rather than reimplemented — H7 owns the mandatory-training
    /// rule and knows that an unreadable evidence source is "could not check", not a failure.
    /// </summary>
    private async Task<Gate> GateAsync(Employee employee, CancellationToken ct)
    {
        var eligibility = await learning.GetIncrementEligibilityAsync(employee.Id, ct);
        return eligibility is null
            ? new Gate([], [], 0m, 0)
            : new Gate([.. eligibility.Blockers], [.. eligibility.Warnings], eligibility.HoursYtd, eligibility.TargetHours);
    }

    /// <summary>The assignment in force now — the approved one with the latest effective period.</summary>
    private async Task<EmployeeSalary?> CurrentSalaryAsync(string employeeId)
        => await salaries.Query().AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.Status == SalaryAssignmentStatus.Approved)
            .OrderByDescending(s => s.EffectiveFromPeriodCode)
            .FirstOrDefaultAsync();

    /// <summary>The earliest open period after the one the current salary starts in — a rise has to move
    /// forward, so offering an earlier period would only produce a refusal.</summary>
    private async Task<PayrollPeriod?> SuggestPeriodAsync(string? currentPeriodCode)
    {
        var open = await periods.Query().AsNoTracking()
            .Where(p => p.Status == PayrollPeriodStatus.Open)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync();

        var today = DateTime.UtcNow.Date;
        return open.FirstOrDefault(p =>
                   string.CompareOrdinal(p.Code, currentPeriodCode ?? "") > 0 && p.EndDate.Date >= today)
            ?? open.FirstOrDefault(p => string.CompareOrdinal(p.Code, currentPeriodCode ?? "") > 0);
    }

    /// <summary>Out-of-band pay warns, never refuses — the same call H5 made, for the same reason.</summary>
    private async Task<string?> BandWarningAsync(SalaryStructure? structure, decimal proposed, string currency)
    {
        if (structure?.JobGradeId is null) return null;
        var grade = await grades.GetByIdAsync(structure.JobGradeId);
        if (grade is null || grade.IsDeleted) return null;

        if (grade.MaxSalary is not null && proposed > grade.MaxSalary)
            return $"{Money(proposed, currency)} is above grade {grade.Code}'s maximum of {Money(grade.MaxSalary.Value, currency)}.";
        if (grade.MinSalary is not null && proposed < grade.MinSalary)
            return $"{Money(proposed, currency)} is below grade {grade.Code}'s minimum of {Money(grade.MinSalary.Value, currency)}.";
        return null;
    }

    private static SalaryIncrementDto ToDto(SalaryIncrement i) => new()
    {
        Id = i.Id, EmployeeId = i.EmployeeId, EmployeeNumber = i.EmployeeNumber, EmployeeName = i.EmployeeName,
        CurrentSalary = i.CurrentSalary, ProposedSalary = i.ProposedSalary,
        IncreaseAmount = Round(i.ProposedSalary - i.CurrentSalary),
        IncreasePercent = i.CurrentSalary > 0 ? Math.Round((i.ProposedSalary - i.CurrentSalary) / i.CurrentSalary * 100m, 1) : 0m,
        CurrencyCode = i.CurrencyCode, SalaryStructureName = i.SalaryStructureName,
        EffectivePeriodId = i.EffectivePeriodId, EffectivePeriodCode = i.EffectivePeriodCode,
        Status = i.Status.ToString(), Justification = i.Justification,
        ProposedBy = i.ProposedBy, ProposedAt = i.ProposedAt,
        DecidedBy = i.DecidedBy, DecidedAt = i.DecidedAt, DecisionReason = i.DecisionReason,
        EligibilityNotes = i.EligibilityNotes, ResultingEmployeeSalaryId = i.ResultingEmployeeSalaryId,
    };

    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";
    private static string Percent(decimal increase, decimal from)
        => from > 0 ? $"+{Math.Round(increase / from * 100m, 1):0.#}%" : "";
    private static decimal Round(decimal value) => HrService.Core.Services.Money.Round(value);
    private static PayrollActionResult Err(string message) => new("Error", message);
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
