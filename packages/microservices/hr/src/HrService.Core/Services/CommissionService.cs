using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Commission;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H11 (P29–P31, COM-001 to COM-009) — sales commission.
/// <para><b>H11-DEC-1: CRM owns the revenue target and the attainment; HR owns the overlay.</b> HR holds the
/// bands, the MD's approval that someone is on commission, and the quarterly split — never a copy of the
/// target. Two stored numbers for "the annual target" would disagree the moment one was edited, and the one
/// that paid somebody would be whichever was written last.</para>
/// <para><b>A statement SNAPSHOTS what it read.</b> That is not a contradiction: HR does not own the target,
/// it records what CRM said at the moment of computation, because a statement issued in April must still
/// explain itself in December.</para>
/// <para><b>Commission is paid through payroll</b> (COM-005) on the same claim-and-release machinery as
/// overtime, so it is taxed correctly and cannot be paid twice.</para>
/// </summary>
public class CommissionService(
    IGenericRepository<Employee> employees,
    IGenericRepository<PayrollPeriod> periods,
    IGenericRepository<CommissionBand> bands,
    IGenericRepository<CommissionPlan> plans,
    IGenericRepository<CommissionStatement> statements,
    IGenericRepository<CommissionDispute> disputes,
    IGenericRepository<HrAuditLog> audit,
    ICrmRevenueGateway crm,
    IHrAlertGateway notifier) : ICommissionService
{
    private static readonly EmploymentStatus[] Active =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>COM-007 — below half the pro-rated target at quarter end.</summary>
    private const decimal RedFlagBelow = 50m;
    /// <summary>COM-008 — between half and 70% of the pro-rated target.</summary>
    private const decimal AmberFlagBelow = 70m;

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<CommissionSummaryDto> GetSummaryAsync(int? year, CancellationToken ct = default)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var yearPlans = await plans.Query().AsNoTracking().Where(p => p.Year == y).ToListAsync(ct);
        var yearStatements = await statements.Query().AsNoTracking()
            .Where(s => s.Year == y && s.Status != CommissionStatementStatus.Cancelled).ToListAsync(ct);
        var openDisputes = await disputes.Query().AsNoTracking()
            .CountAsync(d => d.Status == CommissionDisputeStatus.Open || d.Status == CommissionDisputeStatus.UnderReview, ct);

        var attainment = await crm.ListSalesAttainmentAsync(y, ct);

        return new CommissionSummaryDto
        {
            Year = y,
            BandsInForce = await bands.Query().CountAsync(b => b.IsActive, ct),
            PlansApproved = yearPlans.Count(p => p.Status == CommissionPlanStatus.Approved),
            PlansAwaitingMd = yearPlans.Count(p => p.Status == CommissionPlanStatus.PendingMd),
            StatementsComputed = yearStatements.Count(s => s.Status == CommissionStatementStatus.Computed),
            StatementsApproved = yearStatements.Count(s => s.Status == CommissionStatementStatus.Approved),
            StatementsPaid = yearStatements.Count(s => s.Status == CommissionStatementStatus.Paid),
            CommissionEarnedYtd = Round(yearStatements.Sum(s => s.CommissionAmount)),
            CommissionPaidYtd = Round(yearStatements.Where(s => s.Status == CommissionStatementStatus.Paid).Sum(s => s.PaidAmount ?? 0m)),
            OpenDisputes = openDisputes,
            // Counted on the pro-rated figure, the same one the flags themselves test.
            RedFlagsBelow50 = yearStatements.Count(s => s.ProRatedAttainmentPercent < RedFlagBelow),
            RedFlags50To69 = yearStatements.Count(s => s.ProRatedAttainmentPercent >= RedFlagBelow
                                                    && s.ProRatedAttainmentPercent < AmberFlagBelow),
            SourceNote = attainment is null
                ? "CRM could not be read, so live targets and attainment are unavailable — the figures here come only from statements already computed."
                : null,
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Bands (P29 step 29.1)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<CommissionBandDto>> ListBandsAsync(bool includeInactive)
    {
        var q = bands.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(b => b.IsActive);
        var list = await q.OrderBy(b => b.DisplayOrder).ThenBy(b => b.MinPercent).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<CommissionActionResult> SeedBandsAsync(string userId)
    {
        var effective = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (await bands.Query().AnyAsync(b => b.EffectiveFrom == effective))
            return new CommissionActionResult("NoChange", $"Commission bands effective {effective:yyyy-MM-dd} are already installed.");

        // The QSL Sales Commission Policy scale. Bounds are half-open like the PAYE bands, so no attainment
        // percentage falls between two bands.
        var seed = new (string Label, decimal Min, decimal? Max, decimal Rate)[]
        {
            ("Below 50% — no commission", 0m, 50m, 0m),
            ("50–69%", 50m, 70m, 2m),
            ("70–89%", 70m, 90m, 3.5m),
            ("90–99%", 90m, 100m, 5m),
            ("100% and above", 100m, null, 7m),
        };

        var order = 1;
        foreach (var (label, min, max, rate) in seed)
            await bands.CreateAsync(new CommissionBand
            {
                Label = label, MinPercent = min, MaxPercent = max, CommissionRatePercent = rate,
                EffectiveFrom = effective, DisplayOrder = order++,
                CreatedBy = userId, UpdatedBy = userId,
            });

        var result = new CommissionActionResult("Created", $"{seed.Length} commission bands installed, effective {effective:yyyy-MM-dd}.");
        result.Warnings.Add("These are the policy's five bands (0/2/3.5/5/7%). Check them against your current Sales Commission Policy before the first statement is approved.");

        await LogAsync("CommissionBand", effective.ToString("yyyy-MM-dd"), HrAuditAction.CommissionBandConfigured,
            $"Seeded {seed.Length} commission bands effective {effective:yyyy-MM-dd}.", userId);
        return result;
    }

    public async Task<CommissionActionResult> SaveBandAsync(string? id, SaveCommissionBandDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Label)) return Err("A band needs a label.");
        if (dto.MinPercent < 0) return Err("A band cannot start below zero.");
        if (dto.MaxPercent is not null && dto.MaxPercent <= dto.MinPercent)
            return Err("A band's upper bound must be above its lower bound.");
        if (dto.CommissionRatePercent is < 0 or > 100) return Err("A commission rate must be between 0 and 100 percent.");

        var effective = DateTime.SpecifyKind((dto.EffectiveFrom ?? DateTime.UtcNow).Date, DateTimeKind.Utc);
        CommissionBand band;
        if (string.IsNullOrWhiteSpace(id))
        {
            band = new CommissionBand { EffectiveFrom = effective, CreatedBy = userId, UpdatedBy = userId };
        }
        else
        {
            var found = await bands.GetByIdAsync(id!);
            if (found is null || found.IsDeleted) return Err("Band not found.");
            band = found;
            band.EffectiveFrom = effective;
        }

        band.Label = dto.Label.Trim();
        band.MinPercent = dto.MinPercent;
        band.MaxPercent = dto.MaxPercent;
        band.CommissionRatePercent = dto.CommissionRatePercent;
        band.DisplayOrder = dto.DisplayOrder;
        if (dto.IsActive.HasValue) band.IsActive = dto.IsActive.Value;

        // Position is unique per effective date. Saying so beats letting the unique index surface as a 500.
        var clash = await bands.Query().AsNoTracking().FirstOrDefaultAsync(b =>
            b.EffectiveFrom == effective && b.DisplayOrder == band.DisplayOrder && b.Id != band.Id && !b.IsDeleted);
        if (clash is not null)
            return Err($"Position {band.DisplayOrder} on the {effective:d MMM yyyy} scale is already taken by '{clash.Label}'. Give this band a different position.");

        Touch(band, userId);

        var saved = string.IsNullOrWhiteSpace(id) ? await bands.CreateAsync(band) : await bands.UpdateAsync(band);

        var result = new CommissionActionResult(string.IsNullOrWhiteSpace(id) ? "Created" : "Updated",
            $"{saved.Label} saved — {BandLabel(saved)}.", saved.Id);
        // The scale as a whole has to cover every attainment exactly once; one band cannot tell you that.
        var inForce = await bands.Query().AsNoTracking().Where(b => b.IsActive && b.EffectiveFrom == effective)
            .OrderBy(b => b.DisplayOrder).ToListAsync();
        result.Warnings.AddRange(ValidateScale(inForce));

        await LogAsync("CommissionBand", saved.Id, HrAuditAction.CommissionBandConfigured,
            $"Band '{saved.Label}' saved — {BandLabel(saved)}.", userId);
        return result;
    }

    /// <summary>The scale must cover 0 upwards without a gap or an overlap. A gap pays nothing at an
    /// attainment somebody actually reached; an overlap pays whichever band is found first.</summary>
    private static List<string> ValidateScale(List<CommissionBand> scale)
    {
        var problems = new List<string>();
        if (scale.Count == 0) return problems;
        if (scale[0].MinPercent != 0m)
            problems.Add($"The scale starts at {scale[0].MinPercent:0.##}% rather than 0% — attainment below that would match no band.");
        for (var i = 1; i < scale.Count; i++)
        {
            var previous = scale[i - 1];
            var current = scale[i];
            if (previous.MaxPercent is null) { problems.Add($"Band '{previous.Label}' has no ceiling but is not the last."); continue; }
            if (previous.MaxPercent != current.MinPercent)
                problems.Add(previous.MaxPercent < current.MinPercent
                    ? $"Gap between {previous.MaxPercent:0.##}% and {current.MinPercent:0.##}% — attainment there matches no band."
                    : $"'{previous.Label}' and '{current.Label}' overlap between {current.MinPercent:0.##}% and {previous.MaxPercent:0.##}%.");
        }
        if (scale[^1].MaxPercent is not null)
            problems.Add($"The top band stops at {scale[^1].MaxPercent:0.##}% — over-achievement above that matches no band.");
        return problems;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Plans (P29 step 29.2)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<CommissionPlanDto>> ListPlansAsync(int? year, string? status, CancellationToken ct = default)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var q = plans.Query().AsNoTracking().Where(p => p.Year == y);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionPlanStatus>(status, true, out var st))
            q = q.Where(p => p.Status == st);
        var list = await q.OrderBy(p => p.EmployeeName).ToListAsync(ct);
        if (list.Count == 0) return [];

        // Live from CRM — HR holds no copy of either number.
        var attainment = await crm.ListSalesAttainmentAsync(y, ct);
        var ids = list.Select(p => p.EmployeeId).ToList();
        var userIds = await employees.Query().AsNoTracking()
            .Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.UserId, ct);

        return list.Select(p =>
        {
            var dto = ToDto(p);
            var userId = userIds.GetValueOrDefault(p.EmployeeId);
            if (attainment is null)
                dto.TargetSourceNote = "CRM could not be read — the target and attainment are unknown, not zero.";
            else if (string.IsNullOrWhiteSpace(userId))
                dto.TargetSourceNote = "This employee has no login account, so CRM has no record to match.";
            else
            {
                var row = attainment.FirstOrDefault(a => a.EmployeeUserId == userId);
                if (row is null) dto.TargetSourceNote = $"CRM holds no {y} annual sales target for this person.";
                else
                {
                    dto.AnnualTargetFromCrm = row.AnnualTarget;
                    dto.RevenueAchievedFromCrm = row.RevenueAchieved;
                    dto.AttainmentPercent = row.AnnualTarget > 0
                        ? Round(row.RevenueAchieved / row.AnnualTarget * 100m) : 0m;
                    dto.TargetSourceNote = "Target and attainment read live from CRM.";
                }
            }
            return dto;
        }).ToList();
    }

    public async Task<CommissionActionResult> SavePlanAsync(SaveCommissionPlanDto dto, string userId, CancellationToken ct = default)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!Active.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and cannot be put on commission.");

        var year = dto.Year ?? DateTime.UtcNow.Year;
        var plan = await plans.Query().FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.Year == year, ct);
        if (plan?.Status == CommissionPlanStatus.Approved)
            return Err($"{employee.FullName}'s {year} commission plan is already approved — it cannot be edited.");

        var isNew = plan is null;
        plan ??= new CommissionPlan
        {
            EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber, EmployeeName = employee.FullName,
            Year = year, CreatedBy = userId, UpdatedBy = userId,
        };
        plan.Basis = dto.Basis;
        plan.Notes = dto.Notes;
        if (plan.Status == CommissionPlanStatus.Rejected) { plan.Status = CommissionPlanStatus.Draft; plan.RejectionReason = null; }
        Touch(plan, userId);

        var saved = isNew ? await plans.CreateAsync(plan) : await plans.UpdateAsync(plan);

        var result = new CommissionActionResult(isNew ? "Created" : "Updated",
            $"{employee.FullName}'s {year} commission plan saved.", saved.Id);

        // Say plainly whether CRM actually has a target for this person — a plan with no target pays nothing.
        var attainment = await crm.ListSalesAttainmentAsync(year, ct);
        if (attainment is null)
            result.Warnings.Add("CRM could not be read, so it is not known whether a sales target exists for this person. HR does not hold one — the target is CRM's.");
        else if (string.IsNullOrWhiteSpace(employee.UserId))
            result.Warnings.Add("This employee has no login account, so CRM has no record to match them to.");
        else if (!attainment.Any(a => a.EmployeeUserId == employee.UserId))
            result.Warnings.Add($"CRM holds no {year} annual sales target for this person — set it in CRM, not here. Without it no commission can be computed.");

        return result;
    }

    public async Task<CommissionActionResult> SubmitPlanAsync(string id, string userId)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null || plan.IsDeleted) return Err("Plan not found.");
        if (plan.Status == CommissionPlanStatus.Approved) return Err("That plan is already approved.");
        if (plan.Status == CommissionPlanStatus.PendingMd)
            return new CommissionActionResult("NoChange", "That plan is already with the MD.", plan.Id);

        plan.Status = CommissionPlanStatus.PendingMd;
        plan.SubmittedBy = userId;
        plan.SubmittedAt = DateTime.UtcNow;
        Touch(plan, userId);
        await plans.UpdateAsync(plan);

        await LogAsync("CommissionPlan", plan.Id, HrAuditAction.CommissionPlanSubmitted,
            $"{plan.EmployeeNumber}: {plan.Year} commission plan submitted for MD approval.", userId);
        return new CommissionActionResult("Submitted", $"{plan.EmployeeName}'s plan sent to the MD.", plan.Id);
    }

    public async Task<CommissionActionResult> DecidePlanAsync(string id, DecideCommissionPlanDto dto, string userId, string? userName)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null || plan.IsDeleted) return Err("Plan not found.");
        if (plan.Status != CommissionPlanStatus.PendingMd)
            return Err($"That plan is {plan.Status.ToString().ToLowerInvariant()} — only one awaiting the MD can be decided.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject) return Err("The decision must be Approve or Reject.");
        if (reject && string.IsNullOrWhiteSpace(dto.Reason)) return Err("A rejection needs a reason.");
        // COM-002 makes MD approval the control; the person who prepared it must not be the one giving it.
        if (approve && !string.IsNullOrWhiteSpace(plan.SubmittedBy) && plan.SubmittedBy == userId)
            return Err("The person who prepared a commission plan cannot approve it — COM-002 requires the MD.");

        plan.Status = approve ? CommissionPlanStatus.Approved : CommissionPlanStatus.Rejected;
        plan.ApprovedBy = userId;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.RejectionReason = reject ? dto.Reason!.Trim() : null;
        Touch(plan, userId);
        await plans.UpdateAsync(plan);

        await LogAsync("CommissionPlan", plan.Id,
            approve ? HrAuditAction.CommissionPlanApproved : HrAuditAction.CommissionPlanSubmitted,
            $"{plan.EmployeeNumber}: {plan.Year} commission plan {decision.ToLowerInvariant()}d by {userName ?? userId}.", userId, userName);
        return new CommissionActionResult(approve ? "Approved" : "Rejected",
            approve ? $"{plan.EmployeeName} is on commission for {plan.Year}."
                    : $"{plan.EmployeeName}'s plan sent back.", plan.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Statements (P30)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<CommissionStatementDto>> ListStatementsAsync(int? year, string? status, string? employeeId)
    {
        var q = statements.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(s => s.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(s => s.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionStatementStatus>(status, true, out var st))
            q = q.Where(s => s.Status == st);

        var list = await q.OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter)
            .ThenBy(s => s.EmployeeNumber).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(s => s.Id).ToList();
        var open = await disputes.Query().AsNoTracking()
            .Where(d => ids.Contains(d.CommissionStatementId)
                     && (d.Status == CommissionDisputeStatus.Open || d.Status == CommissionDisputeStatus.UnderReview))
            .Select(d => d.CommissionStatementId).ToListAsync();

        return list.Select(s => ToDto(s, open.Contains(s.Id))).ToList();
    }

    public async Task<CommissionActionResult> ComputeStatementsAsync(ComputeStatementsDto dto, string? tenantSchema, string userId, CancellationToken ct = default)
    {
        var y = dto.Year ?? DateTime.UtcNow.Year;
        var quarter = dto.Quarter ?? PreviousQuarter(DateTime.UtcNow, ref y);
        if (quarter is < 1 or > 4) return Err("The quarter must be between 1 and 4.");

        var approvedPlans = await plans.Query().AsNoTracking()
            .Where(p => p.Year == y && p.Status == CommissionPlanStatus.Approved).ToListAsync(ct);
        if (approvedPlans.Count == 0)
            return Err($"No approved {y} commission plans — nothing to compute. A plan needs MD approval first (COM-002).");

        // The whole computation rests on CRM's numbers. If they cannot be read, computing would produce a set
        // of zero-commission statements that look like a real result.
        // The schema goes in explicitly so this works from the quarterly sweep, which has no request to take a
        // schema claim from — the same fix H6 made to the finance seam.
        var attainment = string.IsNullOrWhiteSpace(tenantSchema)
            ? await crm.ListSalesAttainmentAsync(y, ct)
            : await crm.ListSalesAttainmentAsync(tenantSchema, y, ct);
        if (attainment is null)
            return Err("CRM could not be read, so no target or attainment is available. Commission is NOT computed against a zero target — try again once CRM is reachable.");

        var scale = (await bands.Query().AsNoTracking().Where(b => b.IsActive).ToListAsync(ct))
            .Where(b => b.EffectiveFrom.Date <= new DateTime(y, 12, 31) && (b.EffectiveTo is null || b.EffectiveTo.Value.Year >= y))
            .OrderBy(b => b.DisplayOrder).ToList();
        if (scale.Count == 0) return Err("No commission bands are in force — configure the scale first.");
        // A gap or overlap in the scale makes some attainment percentage match no band, or two. Either way the
        // arithmetic is not defensible, and a zero-commission statement from an unmatched band looks exactly
        // like a deliberate one. Same reasoning as refusing when CRM cannot be read: fix the input first.
        var scaleProblems = ValidateScale(scale);
        if (scaleProblems.Count > 0)
        {
            var err = Err("The commission scale is not sound, so nothing was computed: " + string.Join(" ", scaleProblems));
            err.Warnings.AddRange(scaleProblems);
            return err;
        }

        var (start, end) = QuarterRange(y, quarter);
        var employeeIds = approvedPlans.Select(p => p.EmployeeId).ToList();
        var staff = await employees.Query().AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToListAsync(ct);
        var existing = await statements.Query()
            .Where(s => s.Year == y && s.Quarter == quarter && s.Status != CommissionStatementStatus.Cancelled)
            .ToListAsync(ct);
        // Earlier quarters that actually count as settled. A cancelled one paid nothing; a merely computed one
        // has not been approved, so it cannot have been paid either.
        var settledEarlier = await statements.Query().AsNoTracking()
            .Where(s => s.Year == y && s.Quarter < quarter
                     && (s.Status == CommissionStatementStatus.Approved
                      || s.Status == CommissionStatementStatus.Paid
                      || s.Status == CommissionStatementStatus.Disputed))
            .ToListAsync(ct);

        int created = 0, recomputed = 0, skipped = 0;
        var flags = new List<string>();

        foreach (var plan in approvedPlans)
        {
            var employee = staff.FirstOrDefault(e => e.Id == plan.EmployeeId);
            if (employee is null) { skipped++; continue; }

            // Two different reasons to skip, and they send you to two different systems to fix it. Reporting a
            // missing account as a missing target would send HR to CRM to set one that could never match.
            if (string.IsNullOrWhiteSpace(employee.UserId))
            {
                skipped++;
                flags.Add($"{employee.EmployeeNumber} {employee.FullName} — no login account, so there is nothing for CRM's sales records to be matched against. Create the account first; the target in CRM is not the problem.");
                continue;
            }

            var row = attainment.FirstOrDefault(a => a.EmployeeUserId == employee.UserId);
            if (row is null || row.AnnualTarget <= 0)
            {
                skipped++;
                flags.Add($"{employee.EmployeeNumber} {employee.FullName} — no {y} annual target in CRM.");
                continue;
            }

            // Two attainment figures, because the DFD asks two different questions and they have different
            // denominators. Conflating them is not a rounding difference — it inverts the answer.
            //
            //  * BAND selection (step 30.2) measures year-to-date revenue against the FULL annual target, so
            //    the rate builds through the year as cumulative attainment builds.
            //  * The COM-007/COM-008 RED FLAGS (step 29.4, "pro-rated quarterly target") measure it against
            //    the target pro-rated to the quarters elapsed — annual x N/4.
            //
            // Someone exactly on plan at the end of Q1 has banked 25% of the annual target. Against the annual
            // figure that reads 25% and would fire a CRITICAL under-performance escalation to the MD; against
            // the pro-rated figure it reads 100%, which is the truth. In Q1 the annual denominator flags
            // essentially the whole sales team, top performers included.
            var quarterTarget = Round(row.AnnualTarget / 4m);
            var ytdTarget = quarterTarget * quarter;
            var attainmentPercent = Round(row.RevenueAchieved / row.AnnualTarget * 100m);
            var proRatedAttainment = ytdTarget > 0
                ? Round(row.RevenueAchieved / ytdTarget * 100m)
                : 0m;
            var band = BandFor(scale, attainmentPercent);
            var rate = band?.CommissionRatePercent ?? 0m;

            // Settle year-to-date and net off the earlier quarters. Paying (cumulative revenue x rate) each
            // quarter would pay Q1's revenue again in Q2, Q3 and Q4 — four times over by year end.
            var earnedToDate = Round(row.RevenueAchieved * rate / 100m);
            var priorPaid = settledEarlier
                .Where(p => p.EmployeeId == employee.Id && p.Quarter < quarter)
                .Sum(p => p.CommissionAmount);
            var amount = Round(earnedToDate - priorPaid);
            decimal? overpaid = null;
            if (amount < 0)
            {
                // Revenue fell — a cancelled deal, or a correction in CRM. Recovering an overpayment out of
                // someone's pay is a policy and legal decision, so it is surfaced, never netted off silently.
                overpaid = Math.Abs(amount);
                amount = 0m;
                flags.Add($"{employee.EmployeeNumber} {employee.FullName} — earlier quarters already settled {priorPaid:N2}, which is {overpaid:N2} more than the year has now earned. Nothing is deducted; decide the recovery separately.");
            }

            var statement = existing.FirstOrDefault(s => s.EmployeeId == employee.Id);
            if (statement is not null && statement.Status is not CommissionStatementStatus.Computed)
            {
                // An approved, paid or disputed statement is not ours to overwrite.
                skipped++;
                flags.Add($"{employee.EmployeeNumber} {employee.FullName} — statement already {statement.Status.ToString().ToLowerInvariant()}, left alone.");
                continue;
            }

            var isNew = statement is null;
            statement ??= new CommissionStatement
            {
                StatementNumber = await NextNumberAsync(y, quarter),
                EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber, EmployeeName = employee.FullName,
                CommissionPlanId = plan.Id, Year = y, Quarter = quarter,
                CreatedBy = userId, UpdatedBy = userId,
            };

            statement.PeriodStart = start;
            statement.PeriodEnd = end;
            statement.AnnualTarget = row.AnnualTarget;
            statement.QuarterTarget = quarterTarget;
            statement.RevenueAchieved = row.RevenueAchieved;
            statement.AttainmentPercent = attainmentPercent;
            statement.ProRatedAttainmentPercent = proRatedAttainment;
            statement.CurrencyCode = row.Currency;
            statement.CommissionBandId = band?.Id;
            statement.BandLabel = band?.Label;
            statement.CommissionRatePercent = rate;
            statement.CommissionEarnedToDate = earnedToDate;
            statement.PriorCommissionThisYear = priorPaid;
            statement.CommissionAmount = amount;
            statement.UnrecoveredOverpayment = overpaid;
            statement.SourceNotes =
                $"Target and year-to-date revenue read from CRM on {DateTime.UtcNow:dd MMM yyyy}. "
                + $"Band from {attainmentPercent:0.#}% of the annual target; performance flags from {proRatedAttainment:0.#}% of the {ytdTarget:N2} pro-rated to Q1–Q{quarter}."
                + (quarter > 1
                    ? $" Settled year-to-date: {earnedToDate:N2} earned at {rate:0.##}%, less {priorPaid:N2} settled in earlier quarters."
                    : string.Empty);
            statement.ComputedAt = DateTime.UtcNow;
            statement.ComputedBy = userId;
            statement.Status = CommissionStatementStatus.Computed;

            // COM-007 / COM-008 — the quarter-end red flags, tested against the PRO-RATED target.
            statement.RedFlag = proRatedAttainment < RedFlagBelow
                ? $"Below {RedFlagBelow:0}% of the pro-rated target (COM-007)."
                : proRatedAttainment < AmberFlagBelow
                    ? $"Between {RedFlagBelow:0}% and {AmberFlagBelow:0}% of the pro-rated target (COM-008)."
                    : null;

            Touch(statement, userId);
            if (isNew) { await statements.CreateAsync(statement); created++; }
            else { await statements.UpdateAsync(statement); recomputed++; }

            if (statement.RedFlag is not null)
            {
                // Logged as well as alerted. An alert is acknowledged and disappears; the audit row is what
                // shows, a year later, that the under-performance WAS raised at the time.
                await LogAsync("CommissionStatement", statement.Id, HrAuditAction.CommissionRedFlag,
                    $"{statement.StatementNumber}: {proRatedAttainment:0.#}% of the pro-rated target. {statement.RedFlag}",
                    userId);
                await NotifyAsync(tenantSchema,
                    proRatedAttainment < RedFlagBelow ? "Critical" : "Warning",
                    // The title carries the pro-rated figure AND the quarter, so the two tiers cannot collide
                    // and a later quarter's flag cannot be dropped as a duplicate of an earlier one.
                    $"Commission attainment {proRatedAttainment:0.#}% of pro-rated target — {employee.FullName}, {y} Q{quarter}",
                    $"{employee.EmployeeNumber} {employee.FullName} achieved {row.RevenueAchieved:N2} against a pro-rated target of {ytdTarget:N2} for {y} Q1–Q{quarter} ({proRatedAttainment:0.#}%). "
                    + $"That is {attainmentPercent:0.#}% of the full annual target of {row.AnnualTarget:N2}, which is what sets the commission band. {statement.RedFlag}",
                    "hr.approve");
            }
        }

        var result = new CommissionActionResult(created + recomputed == 0 ? "NoChange" : "Computed",
            $"{y} Q{quarter}: {created} statement(s) created, {recomputed} recomputed, {skipped} skipped.");
        if (flags.Count > 0) result.Warnings.AddRange(flags.Take(6));

        await LogAsync("CommissionStatement", $"{y}-Q{quarter}", HrAuditAction.CommissionStatementComputed,
            $"{y} Q{quarter}: {created} created, {recomputed} recomputed, {skipped} skipped.", userId);
        return result;
    }

    /// <summary>Half-open, like the PAYE bands: a band covers attainment above its lower bound up to and
    /// including its upper bound, so no percentage falls between two.</summary>
    private static CommissionBand? BandFor(List<CommissionBand> scale, decimal attainment)
        => scale.FirstOrDefault(b => attainment > b.MinPercent && (b.MaxPercent is null || attainment <= b.MaxPercent))
        ?? scale.FirstOrDefault(b => b.MinPercent == 0m && attainment <= (b.MaxPercent ?? decimal.MaxValue));

    public async Task<CommissionActionResult> DecideStatementAsync(string id, DecideStatementDto dto, string userId, string? userName)
    {
        var statement = await statements.GetByIdAsync(id);
        if (statement is null || statement.IsDeleted) return Err("Statement not found.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var cancel = decision.Equals("Cancel", StringComparison.OrdinalIgnoreCase);
        if (!approve && !cancel) return Err("The decision must be Approve or Cancel.");

        if (cancel)
        {
            if (statement.Status == CommissionStatementStatus.Paid)
                return Err($"{statement.StatementNumber} has been paid — it cannot be cancelled.");
            if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("Cancelling a statement needs a reason.");
            statement.Status = CommissionStatementStatus.Cancelled;
            statement.CancellationReason = dto.Reason.Trim();
            Touch(statement, userId);
            await statements.UpdateAsync(statement);
            return new CommissionActionResult("Cancelled", $"{statement.StatementNumber} cancelled.", statement.Id);
        }

        if (statement.Status != CommissionStatementStatus.Computed)
            return Err($"{statement.StatementNumber} is {statement.Status.ToString().ToLowerInvariant()} — only a computed statement can be approved.");
        // Whoever computed the figures must not be the one approving them.
        if (!string.IsNullOrWhiteSpace(statement.ComputedBy) && statement.ComputedBy == userId)
            return Err("The person who computed a statement cannot approve it — it needs a second officer.");

        var open = await disputes.Query().AnyAsync(d => d.CommissionStatementId == statement.Id
            && (d.Status == CommissionDisputeStatus.Open || d.Status == CommissionDisputeStatus.UnderReview));
        if (open) return Err($"{statement.StatementNumber} is under dispute — settle that before approving it for payment.");

        // Which payroll period pays it (COM-005).
        var period = string.IsNullOrWhiteSpace(dto.PayrollPeriodId)
            ? await CurrentPeriodAsync()
            : await periods.GetByIdAsync(dto.PayrollPeriodId!);
        if (period is null || period.IsDeleted)
            return Err(string.IsNullOrWhiteSpace(dto.PayrollPeriodId)
                ? "No open payroll period is available to pay this in — generate the year's periods first."
                : "Payroll period not found.");
        if (period.Status != PayrollPeriodStatus.Open)
            return Err($"Payroll period {period.Code} is {period.Status.ToString().ToLowerInvariant()} — pick one still open.");

        statement.Status = CommissionStatementStatus.Approved;
        statement.ApprovedBy = userId;
        statement.ApprovedAt = DateTime.UtcNow;
        statement.PayrollPeriodId = period.Id;
        statement.PayrollPeriodCode = period.Code;
        Touch(statement, userId);
        await statements.UpdateAsync(statement);

        var result = new CommissionActionResult("Approved",
            $"{statement.StatementNumber} approved at {Money(statement.CommissionAmount, statement.CurrencyCode)} — the {period.Code} payroll run will pay and tax it.", statement.Id);
        result.Warnings.Add("Commission is paid through payroll so PAYE is deducted correctly (COM-005). It is picked up automatically by the run for that period.");

        await LogAsync("CommissionStatement", statement.Id, HrAuditAction.CommissionStatementApproved,
            $"{statement.StatementNumber} approved by {userName ?? userId} at {statement.CommissionAmount:N2}, to be paid in {period.Code}.", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Quarterly sweep (COM-004)
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// COM-004 — the quarterly statement run, due on 1 Apr, 1 Jul, 1 Oct and 1 Jan.
    /// <para><b>Catch-up, not calendar-triggered</b> (the shape H3's carry-forward settled on): a job that only
    /// fires when today <i>is</i> the 1st never runs for a tenant whose service happened to be restarting that
    /// morning, and the quarter is then silently never issued. This asks instead whether the quarter that has
    /// most recently ended already has statements, so it lands on the 1st and self-heals on the 4th.</para>
    /// <para><b>It only ever issues a quarter that has none.</b> Recomputing an existing statement every night
    /// would churn <c>ComputedAt</c> and re-fire the red-flag alerts; changing a figure already issued is a
    /// deliberate act and stays on the manual compute endpoint.</para>
    /// <para><b>Why no back-fill of older quarters.</b> CRM reports revenue year-to-date with no as-at date, so
    /// a Q1 statement computed in October would be built on October's revenue — a wrong number wearing a
    /// Q1 label. It is also unnecessary: because commission settles year-to-date and nets off what earlier
    /// quarters paid, a missed quarter is absorbed by the next one and the year's total still comes out right.
    /// A skipped quarter is a timing difference, not a loss.</para>
    /// </summary>
    public async Task<CommissionSweepResultDto> RunCommissionSweepAsync(
        string tenantSchema, string userId, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var quarter = PreviousQuarter(DateTime.UtcNow, ref year);
        var result = new CommissionSweepResultDto { Year = year, Quarter = quarter };

        var already = await statements.Query().AsNoTracking()
            .CountAsync(s => s.Year == year && s.Quarter == quarter
                          && s.Status != CommissionStatementStatus.Cancelled, ct);
        if (already > 0)
        {
            result.Notes.Add($"{year} Q{quarter} already has {already} statement(s) — nothing issued.");
            return result;
        }

        var computed = await ComputeStatementsAsync(
            new ComputeStatementsDto { Year = year, Quarter = quarter }, tenantSchema, userId, ct);

        if (computed.Status == "Error")
        {
            // Not having plans, bands or a reachable CRM is a reason to wait, not to fail: the sweep runs again
            // tomorrow and the quarter is still unissued, so it will be picked up as soon as the input exists.
            result.Notes.Add($"{year} Q{quarter} not issued — {computed.Message}");
            return result;
        }

        var issued = await statements.Query().AsNoTracking()
            .Where(s => s.Year == year && s.Quarter == quarter && s.Status != CommissionStatementStatus.Cancelled)
            .ToListAsync(ct);
        result.StatementsIssued = issued.Count;
        result.RedFlagsRaised = issued.Count(s => s.RedFlag is not null);
        result.Notes.Add(computed.Message);
        result.Notes.AddRange(computed.Warnings);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Disputes (P31)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<CommissionDisputeDto>> ListDisputesAsync(string? status)
    {
        var q = disputes.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CommissionDisputeStatus>(status, true, out var st))
            q = q.Where(d => d.Status == st);
        var list = await q.OrderByDescending(d => d.RaisedAt).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(d => d.CommissionStatementId).Distinct().ToList();
        var numbers = await statements.Query().AsNoTracking().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.StatementNumber);
        return list.Select(d => ToDto(d, numbers.GetValueOrDefault(d.CommissionStatementId))).ToList();
    }

    public async Task<CommissionActionResult> RaiseDisputeAsync(string statementId, RaiseDisputeDto dto, string? tenantSchema, string userId)
    {
        var statement = await statements.GetByIdAsync(statementId);
        if (statement is null || statement.IsDeleted) return Err("Statement not found.");
        if (string.IsNullOrWhiteSpace(dto.Description)) return Err("A dispute needs to say what is being contested.");
        if (statement.Status == CommissionStatementStatus.Cancelled) return Err("That statement is cancelled.");
        if (await disputes.Query().AnyAsync(d => d.CommissionStatementId == statementId
            && (d.Status == CommissionDisputeStatus.Open || d.Status == CommissionDisputeStatus.UnderReview)))
            return Err("That statement already has a dispute outstanding.");

        var dispute = await disputes.CreateAsync(new CommissionDispute
        {
            CommissionStatementId = statement.Id,
            EmployeeId = statement.EmployeeId,
            EmployeeNumber = statement.EmployeeNumber,
            EmployeeName = statement.EmployeeName,
            Description = dto.Description.Trim(),
            DisputedAmount = dto.DisputedAmount,
            RaisedBy = userId, RaisedAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new CommissionActionResult("Raised",
            $"Dispute raised against {statement.StatementNumber}.", dispute.Id);

        // Freeze the payment. Paying a figure somebody is formally contesting is how a dispute becomes a
        // grievance — but an already-paid statement stays paid; the remedy is an adjustment, not a reversal.
        if (statement.Status is CommissionStatementStatus.Computed or CommissionStatementStatus.Approved)
        {
            statement.Status = CommissionStatementStatus.Disputed;
            Touch(statement, userId);
            await statements.UpdateAsync(statement);
            result.Warnings.Add("Payment is frozen until the dispute is settled.");
        }
        else if (statement.Status == CommissionStatementStatus.Paid)
            result.Warnings.Add("This statement has already been paid — if the dispute is upheld the correction is an adjustment, not a reversal.");

        // COM-006 — disputes route to HR automatically.
        await NotifyAsync(tenantSchema, "Warning",
            $"Commission dispute raised — {statement.StatementNumber}, {statement.EmployeeName}",
            $"{statement.EmployeeNumber} {statement.EmployeeName} is contesting {statement.StatementNumber} ({Money(statement.CommissionAmount, statement.CurrencyCode)}). {dispute.Description}",
            "hr.payroll.approve");
        await LogAsync("CommissionDispute", dispute.Id, HrAuditAction.CommissionDisputeRaised,
            $"{statement.StatementNumber}: dispute raised by {statement.EmployeeNumber}. {dispute.Description}", userId);
        return result;
    }

    public async Task<CommissionActionResult> ResolveDisputeAsync(string id, ResolveDisputeDto dto, string userId, string? userName)
    {
        var dispute = await disputes.GetByIdAsync(id);
        if (dispute is null || dispute.IsDeleted) return Err("Dispute not found.");
        if (dispute.Status is CommissionDisputeStatus.Upheld or CommissionDisputeStatus.Rejected)
            return Err("That dispute is already settled.");
        if (!Enum.TryParse<CommissionDisputeStatus>(dto.Outcome, true, out var outcome)
            || outcome is not (CommissionDisputeStatus.Upheld or CommissionDisputeStatus.Rejected))
            return Err("The outcome must be Upheld or Rejected.");
        if (string.IsNullOrWhiteSpace(dto.Resolution)) return Err("A resolution needs to say what was decided.");
        if (outcome == CommissionDisputeStatus.Upheld && dto.AdjustedAmount is null)
            return Err("Upholding a dispute needs the corrected commission figure.");
        if (dto.AdjustedAmount is < 0) return Err("A commission figure cannot be negative.");
        // Whoever raised a dispute cannot decide it.
        if (!string.IsNullOrWhiteSpace(dispute.RaisedBy) && dispute.RaisedBy == userId)
            return Err("The person who raised a dispute cannot settle it.");

        var statement = await statements.GetByIdAsync(dispute.CommissionStatementId);
        if (statement is null) return Err("The disputed statement no longer exists.");

        dispute.Status = outcome;
        dispute.Findings = dto.Findings;
        dispute.Resolution = dto.Resolution.Trim();
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolvedBy = userId;
        dispute.AdjustedAmount = outcome == CommissionDisputeStatus.Upheld ? dto.AdjustedAmount : null;
        Touch(dispute, userId);
        await disputes.UpdateAsync(dispute);

        var result = new CommissionActionResult(outcome.ToString(), "", dispute.Id);

        if (outcome == CommissionDisputeStatus.Upheld)
        {
            var was = statement.CommissionAmount;
            statement.CommissionAmount = dto.AdjustedAmount!.Value;
            statement.SourceNotes = Append(statement.SourceNotes,
                $"Adjusted from {was:N2} to {statement.CommissionAmount:N2} on {DateTime.UtcNow:dd MMM yyyy} after dispute {dispute.Id}.");
            // Back to computed so it goes through approval again — an adjusted figure nobody re-approved is
            // exactly the thing the approval step exists to prevent.
            if (statement.Status != CommissionStatementStatus.Paid)
                statement.Status = CommissionStatementStatus.Computed;
            result = result with { Message = $"Dispute upheld — {statement.StatementNumber} adjusted to {Money(statement.CommissionAmount, statement.CurrencyCode)}." };
            result.Warnings.Add(statement.Status == CommissionStatementStatus.Paid
                ? "The statement was already paid, so the difference must be settled separately — the figure here is corrected for the record."
                : "The statement is back with the approver: an adjusted figure has to be approved before it is paid.");
        }
        else
        {
            if (statement.Status == CommissionStatementStatus.Disputed)
                statement.Status = CommissionStatementStatus.Computed;
            result = result with { Message = $"Dispute rejected — {statement.StatementNumber} stands at {Money(statement.CommissionAmount, statement.CurrencyCode)}." };
        }
        Touch(statement, userId);
        await statements.UpdateAsync(statement);

        await LogAsync("CommissionDispute", dispute.Id, HrAuditAction.CommissionDisputeResolved,
            $"{statement.StatementNumber}: dispute {outcome.ToString().ToLowerInvariant()} by {userName ?? userId}. {dispute.Resolution}", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════
    private static int PreviousQuarter(DateTime today, ref int year)
    {
        var q = (today.Month - 1) / 3 + 1 - 1;
        if (q >= 1) return q;
        year -= 1;
        return 4;
    }

    private static (DateTime Start, DateTime End) QuarterRange(int year, int quarter)
    {
        var start = new DateTime(year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(3).AddDays(-1));
    }

    private async Task<PayrollPeriod?> CurrentPeriodAsync()
    {
        var today = DateTime.UtcNow.Date;
        var open = await periods.Query().Where(p => p.Status == PayrollPeriodStatus.Open)
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ToListAsync();
        return open.FirstOrDefault(p => p.StartDate.Date <= today && p.EndDate.Date >= today) ?? open.FirstOrDefault();
    }

    /// <summary>Derived from the highest issued, not a row count — the trap that broke finance's numbering.</summary>
    private async Task<string> NextNumberAsync(int year, int quarter)
    {
        var prefix = $"CS-{year}-Q{quarter}-";
        var issued = await statements.Query().IgnoreQueryFilters()
            .Where(s => s.StatementNumber.StartsWith(prefix)).Select(s => s.StatementNumber).ToListAsync();
        var highest = issued.Select(n => int.TryParse(n[prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{highest + 1:D3}";
    }

    private static CommissionBandDto ToDto(CommissionBand b) => new()
    {
        Id = b.Id, Label = b.Label, MinPercent = b.MinPercent, MaxPercent = b.MaxPercent,
        CommissionRatePercent = b.CommissionRatePercent, EffectiveFrom = b.EffectiveFrom,
        DisplayOrder = b.DisplayOrder, IsActive = b.IsActive, Band = BandLabel(b),
    };

    private static string BandLabel(CommissionBand b) => b.MaxPercent is null
        ? $"Above {b.MinPercent:0.##}% — {b.CommissionRatePercent:0.##}% of revenue"
        : $"{b.MinPercent:0.##}% to {b.MaxPercent:0.##}% — {b.CommissionRatePercent:0.##}% of revenue";

    private static CommissionPlanDto ToDto(CommissionPlan p) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.EmployeeName,
        Year = p.Year, Status = p.Status.ToString(), Basis = p.Basis, Notes = p.Notes,
        SubmittedAt = p.SubmittedAt, ApprovedBy = p.ApprovedBy, ApprovedAt = p.ApprovedAt,
        RejectionReason = p.RejectionReason,
    };

    private static CommissionStatementDto ToDto(CommissionStatement s, bool hasOpenDispute) => new()
    {
        Id = s.Id, StatementNumber = s.StatementNumber,
        EmployeeId = s.EmployeeId, EmployeeNumber = s.EmployeeNumber, EmployeeName = s.EmployeeName,
        Year = s.Year, Quarter = s.Quarter, PeriodStart = s.PeriodStart, PeriodEnd = s.PeriodEnd,
        AnnualTarget = s.AnnualTarget, QuarterTarget = s.QuarterTarget,
        RevenueAchieved = s.RevenueAchieved, AttainmentPercent = s.AttainmentPercent,
        ProRatedAttainmentPercent = s.ProRatedAttainmentPercent, YtdTarget = s.QuarterTarget * s.Quarter,
        CurrencyCode = s.CurrencyCode, SourceNotes = s.SourceNotes,
        BandLabel = s.BandLabel, CommissionRatePercent = s.CommissionRatePercent, CommissionAmount = s.CommissionAmount,
        // The three figures that EXPLAIN the amount. Without them a quarter reads as a bare number and nobody
        // can see why it differs from revenue x rate — which is precisely what someone disputes a statement over.
        CommissionEarnedToDate = s.CommissionEarnedToDate,
        PriorCommissionThisYear = s.PriorCommissionThisYear,
        UnrecoveredOverpayment = s.UnrecoveredOverpayment,
        Status = s.Status.ToString(), ComputedAt = s.ComputedAt, ApprovedAt = s.ApprovedAt,
        PayrollPeriodCode = s.PayrollPeriodCode, PayrollRunId = s.PayrollRunId, PaidAmount = s.PaidAmount,
        RedFlag = s.RedFlag, CancellationReason = s.CancellationReason, HasOpenDispute = hasOpenDispute,
        NextStep = s.Status switch
        {
            CommissionStatementStatus.Computed => "Awaiting approval — needs a second officer.",
            CommissionStatementStatus.Approved => $"Will be paid and taxed by the {s.PayrollPeriodCode} payroll run.",
            CommissionStatementStatus.Paid => "Paid through payroll.",
            CommissionStatementStatus.Disputed => "Under dispute — payment frozen.",
            _ => "Cancelled.",
        },
    };

    private static CommissionDisputeDto ToDto(CommissionDispute d, string? statementNumber) => new()
    {
        Id = d.Id, CommissionStatementId = d.CommissionStatementId, StatementNumber = statementNumber,
        EmployeeId = d.EmployeeId, EmployeeNumber = d.EmployeeNumber, EmployeeName = d.EmployeeName,
        Description = d.Description, DisputedAmount = d.DisputedAmount, Status = d.Status.ToString(),
        RaisedAt = d.RaisedAt, Findings = d.Findings, Resolution = d.Resolution,
        ResolvedAt = d.ResolvedAt, AdjustedAmount = d.AdjustedAmount,
    };

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    private static string Money(decimal amount, string? currency) => $"{currency ?? "KES"} {amount:N2}";
    private static string Append(string? existing, string addition)
        => string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";
    private static decimal Round(decimal value) => HrService.Core.Services.Money.Round(value);
    private static CommissionActionResult Err(string message) => new("Error", message);
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
