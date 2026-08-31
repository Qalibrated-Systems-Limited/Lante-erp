using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Learning;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H7 (P22 + P23 + P25 + P26) — learning and development.
/// <para><b>HR owns the rule, not the record</b> (HR-DEC-5). The mandatory-training matrix lives here; whether
/// someone has actually done their HSE or anti-bribery training is read from the services that own those
/// records. Copying them into HR would mean two answers to one question, and the wrong one would be the one
/// blocking somebody's pay rise.</para>
/// <para><b>"Could not check" is never "not done".</b> When an evidence source is unreachable the requirement
/// is reported <see cref="MandatoryTrainingState.Unknown"/> and does NOT block an increment. Treating a
/// network failure as non-compliance would let an hse-service outage freeze every salary review in the
/// company.</para>
/// <para><b>Training hours are summed, never counted up.</b> The YTD figure is a SUM over the hours log rather
/// than a stored total, so correcting or removing an event corrects the target position too.</para>
/// </summary>
public class LearningService(
    IGenericRepository<Employee> employees,
    IGenericRepository<Position> positions,
    IGenericRepository<EmployeeCertification> certifications,
    IGenericRepository<LearningDevelopmentPlan> plans,
    IGenericRepository<LdpObjective> objectives,
    IGenericRepository<TrainingEvent> trainingEvents,
    IGenericRepository<TrainingHoursLog> hoursLog,
    IGenericRepository<MandatoryTrainingRequirement> requirements,
    IGenericRepository<KnowledgeSharingSession> sessions,
    IGenericRepository<KnowledgeSharingAttendance> attendance,
    IGenericRepository<LdBudget> budgets,
    IGenericRepository<LearningRedFlag> redFlags,
    IGenericRepository<HrAuditLog> audit,
    ITrainingEvidenceGateway evidence,
    IHrAlertGateway notifier) : ILearningService
{
    private static readonly EmploymentStatus[] Active =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>HR-026 — the standard annual target when a position does not set its own.</summary>
    private const int DefaultAnnualHours = 40;
    /// <summary>HR-025 — plans are due by 15 January; HR-036 escalates on 15 February.</summary>
    private const int LdpDueMonth = 1, LdpDueDay = 15, LdpEscalateMonth = 2, LdpEscalateDay = 15;
    /// <summary>HR-033 — nothing logged by this date is a red flag.</summary>
    private const int ZeroHoursCheckMonth = 6, ZeroHoursCheckDay = 30;
    /// <summary>HR-030 — the company owes itself two sessions a month.</summary>
    private const int MonthlySessionTarget = 2;
    /// <summary>Renewal window before a mandatory training lapses.</summary>
    private const int DueSoonDays = 30;

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<LearningSummaryDto> GetSummaryAsync(int? year, CancellationToken ct = default)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var today = DateTime.UtcNow.Date;

        var staff = await employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status)).ToListAsync(ct);
        var yearPlans = await plans.Query().AsNoTracking().Where(p => p.PlanYear == y).ToListAsync(ct);
        var logs = await hoursLog.Query().AsNoTracking().Where(l => l.TrainingDate.Year == y).ToListAsync(ct);
        var events = await trainingEvents.Query().AsNoTracking().Where(t => t.TrainingDate.Year == y).ToListAsync(ct);
        var ks = await sessions.Query().AsNoTracking().Where(s => s.SessionDate.Year == y).ToListAsync(ct);
        var budgetRows = await budgets.Query().AsNoTracking().Where(b => b.Year == y && b.IsActive).ToListAsync(ct);

        var targets = await TargetsAsync(staff);
        var byEmployee = logs.GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.Sum(l => l.Hours));

        var compliance = await GetComplianceAsync(null, ct);

        return new LearningSummaryDto
        {
            Year = y,
            PlansExpected = staff.Count,
            PlansApproved = yearPlans.Count(p => p.Status == LdpStatus.Approved),
            PlansAwaitingApproval = yearPlans.Count(p => p.Status == LdpStatus.Submitted),
            PlansMissing = staff.Count(e => !yearPlans.Any(p => p.EmployeeId == e.Id && p.Status == LdpStatus.Approved)),

            TotalTrainingHours = Round(logs.Sum(l => l.Hours)),
            EmployeesOnTarget = staff.Count(e => byEmployee.GetValueOrDefault(e.Id) >= targets.GetValueOrDefault(e.Id, DefaultAnnualHours)),
            EmployeesZeroHours = staff.Count(e => byEmployee.GetValueOrDefault(e.Id) <= 0),
            TrainingEvents = events.Count,

            MandatoryRequirements = await requirements.Query().CountAsync(r => r.IsActive, ct),
            ComplianceBreaches = compliance.Sum(c => c.Breached),
            ComplianceUnknown = compliance.Sum(c => c.Unknown),
            BlockedFromIncrement = compliance.Count(c => c.AnyBlocksIncrement),

            KnowledgeSessionsThisMonth = ks.Count(s => s.SessionDate.Month == today.Month && s.SessionDate.Year == today.Year),
            KnowledgeSessionsThisYear = ks.Count,
            MonthlySessionTargetMet = ks.Count(s => s.SessionDate.Month == today.Month && s.SessionDate.Year == today.Year) >= MonthlySessionTarget,

            BudgetTotal = Round(budgetRows.Sum(b => b.BudgetedAmount)),
            BudgetSpent = Round(budgetRows.Sum(b => b.ActualSpend)),
            BudgetsOver80 = budgetRows.Count(b => b.BudgetedAmount > 0 && b.ActualSpend / b.BudgetedAmount >= 0.8m),
            BudgetsExhausted = budgetRows.Count(b => b.BudgetedAmount > 0 && b.ActualSpend >= b.BudgetedAmount),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Learning & development plans (P22)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<LdpDto>> ListPlansAsync(int? year, string? status, string? employeeId)
    {
        var q = plans.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(p => p.PlanYear == year.Value);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LdpStatus>(status, true, out var st))
            q = q.Where(p => p.Status == st);

        var list = await q.OrderByDescending(p => p.PlanYear).ThenBy(p => p.EmployeeName).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(p => p.Id).ToList();
        var objs = await objectives.Query().AsNoTracking()
            .Where(o => ids.Contains(o.LearningDevelopmentPlanId)).ToListAsync();
        return list.Select(p => ToDto(p, objs.Where(o => o.LearningDevelopmentPlanId == p.Id).ToList())).ToList();
    }

    public async Task<LdpDto?> GetPlanAsync(string id)
    {
        var plan = await plans.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return null;
        var objs = await objectives.Query().AsNoTracking().Where(o => o.LearningDevelopmentPlanId == id).ToListAsync();
        return ToDto(plan, objs);
    }

    public async Task<LearningActionResult> SavePlanAsync(SaveLdpDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId ?? string.Empty);
        if (employee is null || employee.IsDeleted) return Err("Employee not found.");
        if (!Active.Contains(employee.Status))
            return Err($"{employee.FullName} is {employee.Status} and does not need a plan.");

        var year = dto.PlanYear ?? DateTime.UtcNow.Year;
        var clean = dto.Objectives.Where(o => !string.IsNullOrWhiteSpace(o.Objective)).ToList();
        if (clean.Count == 0) return Err("A plan needs at least one objective.");

        var plan = await plans.Query().FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.PlanYear == year);
        if (plan is not null && plan.Status == LdpStatus.Approved)
            return Err($"{employee.FullName}'s {year} plan is already approved — it cannot be edited.");

        if (plan is null)
        {
            plan = await plans.CreateAsync(new LearningDevelopmentPlan
            {
                EmployeeId = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                EmployeeName = employee.FullName,
                DepartmentId = employee.DepartmentId,
                PlanYear = year,
                Notes = dto.Notes,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }
        else
        {
            plan.Notes = dto.Notes;
            // Re-saving a rejected plan puts it back in the author's hands rather than leaving it rejected.
            if (plan.Status == LdpStatus.Rejected) { plan.Status = LdpStatus.Draft; plan.RejectionReason = null; }
            Touch(plan, userId);
            await plans.UpdateAsync(plan);
        }

        // Objectives are replaced wholesale, EXCEPT any already completed — a training event closed those, and
        // rewriting the plan must not erase the evidence that something was actually done.
        var existing = await objectives.Query().Where(o => o.LearningDevelopmentPlanId == plan.Id).ToListAsync();
        var completed = existing.Where(o => o.Status == LdpObjectiveStatus.Completed).ToList();
        foreach (var stale in existing.Except(completed)) await objectives.DeleteAsync(stale);

        foreach (var o in clean)
        {
            if (completed.Any(c => c.Id == o.Id)) continue;   // keep the completed one as it stands
            await objectives.CreateAsync(new LdpObjective
            {
                LearningDevelopmentPlanId = plan.Id,
                Objective = o.Objective.Trim(),
                Activity = o.Activity,
                TargetDate = o.TargetDate is null ? null : DateTime.SpecifyKind(o.TargetDate.Value.Date, DateTimeKind.Utc),
                Notes = o.Notes,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }

        var result = new LearningActionResult("Saved",
            $"{employee.FullName}'s {year} plan saved with {clean.Count} objective(s).", plan.Id);
        if (completed.Count > 0)
            result.Warnings.Add($"{completed.Count} completed objective(s) were kept — a training event closed them.");
        return result;
    }

    public async Task<LearningActionResult> SubmitPlanAsync(string id, string userId)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null || plan.IsDeleted) return Err("Plan not found.");
        if (plan.Status == LdpStatus.Approved) return Err("That plan is already approved.");
        if (plan.Status == LdpStatus.Submitted) return new LearningActionResult("NoChange", "That plan is already with the line manager.", plan.Id);

        var count = await objectives.Query().CountAsync(o => o.LearningDevelopmentPlanId == plan.Id);
        if (count == 0) return Err("A plan needs at least one objective before it can be submitted.");

        plan.Status = LdpStatus.Submitted;
        plan.SubmittedAt = DateTime.UtcNow;
        plan.RejectionReason = null;
        Touch(plan, userId);
        await plans.UpdateAsync(plan);

        await LogAsync("LearningDevelopmentPlan", plan.Id, HrAuditAction.LdpSubmitted,
            $"{plan.EmployeeNumber} submitted their {plan.PlanYear} LDP with {count} objective(s).", userId);
        return new LearningActionResult("Submitted", $"{plan.PlanYear} plan sent to the line manager.", plan.Id);
    }

    public async Task<LearningActionResult> DecidePlanAsync(string id, DecideLdpDto dto, string userId, string? userName)
    {
        var plan = await plans.GetByIdAsync(id);
        if (plan is null || plan.IsDeleted) return Err("Plan not found.");
        if (plan.Status != LdpStatus.Submitted)
            return Err($"That plan is {plan.Status.ToString().ToLowerInvariant()} — only a submitted plan can be decided.");

        var decision = (dto.Decision ?? string.Empty).Trim();
        var approve = decision.Equals("Approve", StringComparison.OrdinalIgnoreCase);
        var reject = decision.Equals("Reject", StringComparison.OrdinalIgnoreCase);
        if (!approve && !reject) return Err("The decision must be Approve or Reject.");
        if (reject && string.IsNullOrWhiteSpace(dto.Reason)) return Err("A rejection needs a reason.");

        // The author must not approve their own plan — the same second-officer rule the rest of HR uses.
        if (approve && !string.IsNullOrWhiteSpace(plan.CreatedBy) && plan.CreatedBy == userId)
            return Err("You cannot approve a plan you wrote — it needs the line manager.");

        plan.Status = approve ? LdpStatus.Approved : LdpStatus.Rejected;
        plan.ApprovedBy = userId;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.RejectionReason = reject ? dto.Reason!.Trim() : null;
        Touch(plan, userId);
        await plans.UpdateAsync(plan);

        await LogAsync("LearningDevelopmentPlan", plan.Id,
            approve ? HrAuditAction.LdpApproved : HrAuditAction.LdpRejected,
            $"{plan.EmployeeNumber}'s {plan.PlanYear} LDP {decision.ToLowerInvariant()}d by {userName ?? userId}." +
            (reject ? $" {plan.RejectionReason}" : ""), userId, userName);

        return new LearningActionResult(approve ? "Approved" : "Rejected",
            approve ? $"{plan.EmployeeName}'s {plan.PlanYear} plan approved."
                    : $"{plan.EmployeeName}'s plan sent back.", plan.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Training events and hours (P23)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<TrainingEventDto>> ListTrainingAsync(int? year, string? departmentId, string? employeeId)
    {
        var q = trainingEvents.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(t => t.TrainingDate.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(t => t.DepartmentId == departmentId);

        var list = await q.OrderByDescending(t => t.TrainingDate).ToListAsync();
        if (!string.IsNullOrWhiteSpace(employeeId))
        {
            var attended = await hoursLog.Query().AsNoTracking()
                .Where(l => l.EmployeeId == employeeId).Select(l => l.TrainingEventId).ToListAsync();
            list = list.Where(t => attended.Contains(t.Id)).ToList();
        }
        if (list.Count == 0) return [];

        var ids = list.Select(t => t.Id).ToList();
        var logs = await hoursLog.Query().AsNoTracking().Where(l => ids.Contains(l.TrainingEventId)).ToListAsync();
        return list.Select(t => ToDto(t, logs.Where(l => l.TrainingEventId == t.Id).ToList())).ToList();
    }

    public async Task<LearningActionResult> LogTrainingAsync(SaveTrainingEventDto dto, string? tenantSchema, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Err("A training event needs a title.");
        if (dto.DurationHours <= 0) return Err("A training event needs a duration in hours.");
        if (dto.Cost < 0) return Err("A cost cannot be negative.");
        if (!Enum.TryParse<TrainingSource>(dto.Source, true, out var source))
            return Err("The source must be External, Internal, KnowledgeSharing or OnTheJob.");

        var attendeeIds = dto.Attendees.Select(a => a.EmployeeId).Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        if (attendeeIds.Count == 0) return Err("A training event needs at least one attendee — hours belong to people.");

        var people = await employees.Query().Where(e => attendeeIds.Contains(e.Id)).ToListAsync();
        var missing = attendeeIds.Except(people.Select(p => p.Id)).ToList();
        if (missing.Count > 0) return Err($"{missing.Count} attendee(s) are not employees on file.");

        var date = DateTime.SpecifyKind(dto.TrainingDate.Date, DateTimeKind.Utc);
        if (date > DateTime.UtcNow.Date.AddDays(1))
            return Err("A training event cannot be logged for a future date — log it once it has happened.");

        string? mandatoryCode = null;
        if (!string.IsNullOrWhiteSpace(dto.MandatoryTrainingCode))
        {
            var code = dto.MandatoryTrainingCode!.Trim().ToUpperInvariant();
            var req = await requirements.Query().FirstOrDefaultAsync(r => r.Code == code);
            if (req is null) return Err($"There is no mandatory requirement with code '{code}'.");
            // HR-DEC-5: HR may only satisfy requirements whose evidence it actually owns.
            if (req.EvidenceSource != TrainingEvidenceSource.Hr)
                return Err($"{req.Name} evidence is held by {req.EvidenceSource.ToString().ToLowerInvariant()}-service — record the completion there, not in HR.");
            mandatoryCode = code;
        }

        // Department: explicit, else the first attendee's, so the budget always has somewhere to land.
        var departmentId = dto.DepartmentId ?? people.FirstOrDefault(p => p.DepartmentId != null)?.DepartmentId;

        var created = await trainingEvents.CreateAsync(new TrainingEvent
        {
            Title = dto.Title.Trim(),
            Provider = dto.Provider,
            Description = dto.Description,
            Source = source,
            TrainingDate = date,
            DurationHours = dto.DurationHours,
            Cost = dto.Cost,
            DepartmentId = departmentId,
            DepartmentName = people.FirstOrDefault(p => p.DepartmentId == departmentId)?.DepartmentName,
            MandatoryTrainingCode = mandatoryCode,
            CertificateUrl = dto.CertificateUrl,
            CreatedBy = userId, UpdatedBy = userId,
        });

        var result = new LearningActionResult("Logged",
            $"{created.Title} logged for {people.Count} attendee(s).", created.Id);

        // Attendance, hours, and any LDP objective the training closes.
        var closed = 0;
        foreach (var person in people)
        {
            var input = dto.Attendees.First(a => a.EmployeeId == person.Id);
            var hours = input.Hours is > 0 ? input.Hours!.Value : dto.DurationHours;

            var objectiveId = input.LdpObjectiveId;
            if (string.IsNullOrWhiteSpace(objectiveId))
                objectiveId = await MatchObjectiveAsync(person.Id, date.Year, created.Title);

            await hoursLog.CreateAsync(new TrainingHoursLog
            {
                EmployeeId = person.Id,
                EmployeeNumber = person.EmployeeNumber,
                EmployeeName = person.FullName,
                DepartmentId = person.DepartmentId,
                TrainingEventId = created.Id,
                TrainingTitle = created.Title,
                TrainingDate = date,
                Hours = hours,
                LdpObjectiveId = objectiveId,
                VerifiedBy = userId,
                LoggedAt = DateTime.UtcNow,
                CreatedBy = userId, UpdatedBy = userId,
            });

            if (!string.IsNullOrWhiteSpace(objectiveId))
            {
                var objective = await objectives.GetByIdAsync(objectiveId!);
                if (objective is not null && objective.Status != LdpObjectiveStatus.Completed)
                {
                    objective.Status = LdpObjectiveStatus.Completed;
                    objective.CompletionDate = date;
                    objective.CompletedByTrainingId = created.Id;
                    Touch(objective, userId);
                    await objectives.UpdateAsync(objective);
                    closed++;
                }
            }
        }
        if (closed > 0)
        {
            result.Warnings.Add($"{closed} LDP objective(s) closed automatically.");
            await LogAsync("TrainingEvent", created.Id, HrAuditAction.TrainingObjectiveCompleted,
                $"{created.Title} closed {closed} LDP objective(s).", userId);
        }

        // Budget (P26 step 26.2) — recomputed from the events, never incremented, so a corrected cost corrects
        // the spend rather than leaving a counter that only ever goes up.
        if (dto.Cost > 0 && !string.IsNullOrWhiteSpace(departmentId))
        {
            var budgetWarnings = await RecomputeBudgetAsync(departmentId!, date.Year, tenantSchema, userId);
            result.Warnings.AddRange(budgetWarnings);
        }
        else if (dto.Cost > 0)
        {
            result.Warnings.Add("No department could be resolved, so this cost is not counted against any L&D budget.");
        }

        await LogAsync("TrainingEvent", created.Id, HrAuditAction.TrainingLogged,
            $"{created.Title} ({source}, {dto.DurationHours:0.##} h, cost {dto.Cost:N2}) logged for {people.Count} attendee(s).", userId);
        return result;
    }

    /// <summary>An objective is matched on a word overlap with the training title — deliberately loose, since
    /// nobody writes their plan in the provider's course names. The caller can always name one explicitly.</summary>
    private async Task<string?> MatchObjectiveAsync(string employeeId, int year, string title)
    {
        var plan = await plans.Query().AsNoTracking()
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.PlanYear == year && p.Status == LdpStatus.Approved);
        if (plan is null) return null;

        var open = await objectives.Query().AsNoTracking()
            .Where(o => o.LearningDevelopmentPlanId == plan.Id && o.Status != LdpObjectiveStatus.Completed)
            .ToListAsync();
        if (open.Count == 0) return null;

        var words = Words(title);
        if (words.Count == 0) return null;

        return open
            .Select(o => new { o.Id, Score = Words($"{o.Objective} {o.Activity}").Intersect(words).Count() })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault()?.Id;
    }

    private static HashSet<string> Words(string? text) => (text ?? "")
        .Split(new[] { ' ', ',', '.', '-', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.Trim().ToLowerInvariant())
        .Where(w => w.Length > 3)   // drop "the", "and", "of" and friends without keeping a stop-word list
        .ToHashSet();

    public async Task<List<TrainingHoursSummaryDto>> ListHoursAsync(int? year, string? departmentId)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var today = DateTime.UtcNow.Date;

        var q = employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status));
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(e => e.DepartmentId == departmentId);
        var staff = await q.OrderBy(e => e.EmployeeNumber).ToListAsync();
        if (staff.Count == 0) return [];

        var ids = staff.Select(e => e.Id).ToList();
        var logs = await hoursLog.Query().AsNoTracking()
            .Where(l => ids.Contains(l.EmployeeId) && l.TrainingDate.Year == y).ToListAsync();
        var targets = await TargetsAsync(staff);
        var positionTitles = await positions.Query().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Title);

        // HR-033 fires only once the checkpoint has passed — flagging zero hours in February would be noise.
        var pastCheckpoint = today >= new DateTime(y, ZeroHoursCheckMonth, ZeroHoursCheckDay, 0, 0, 0, DateTimeKind.Utc) || today.Year > y;

        return staff.Select(e =>
        {
            var mine = logs.Where(l => l.EmployeeId == e.Id).ToList();
            var hours = Round(mine.Sum(l => l.Hours));
            var target = targets.GetValueOrDefault(e.Id, DefaultAnnualHours);
            return new TrainingHoursSummaryDto
            {
                EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.FullName,
                PositionTitle = e.PositionId is null ? null : positionTitles.GetValueOrDefault(e.PositionId),
                Year = y,
                HoursYtd = hours,
                TargetHours = target,
                PercentOfTarget = target > 0 ? Math.Round(hours / target * 100m, 1) : 0m,
                EventsAttended = mine.Count,
                LastTrainingDate = mine.OrderByDescending(l => l.TrainingDate).FirstOrDefault()?.TrainingDate,
                ZeroHoursRedFlag = hours <= 0 && pastCheckpoint,
            };
        }).ToList();
    }

    private async Task<Dictionary<string, int>> TargetsAsync(List<Employee> staff)
    {
        var positionIds = staff.Where(e => e.PositionId != null).Select(e => e.PositionId!).Distinct().ToList();
        var byPosition = positionIds.Count == 0
            ? []
            : await positions.Query().AsNoTracking().Where(p => positionIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.AnnualTrainingHoursTarget ?? DefaultAnnualHours);

        return staff.ToDictionary(e => e.Id,
            e => e.PositionId is not null && byPosition.TryGetValue(e.PositionId, out var t) ? t : DefaultAnnualHours);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Mandatory training (P23, HR-029/034)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<MandatoryRequirementDto>> ListRequirementsAsync(bool includeInactive)
    {
        var q = requirements.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(r => r.IsActive);
        var list = await q.OrderBy(r => r.DisplayOrder).ThenBy(r => r.Code).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<LearningActionResult> SeedRequirementsAsync(string userId)
    {
        var seed = new (string Code, string Name, TrainingEvidenceSource Source, int? Validity, int Order, string Description)[]
        {
            ("HSE", "HSE Induction & Refresher", TrainingEvidenceSource.Hse, 12, 10,
                "Health and safety training. The completion record lives in hse-service — HR reads it, never copies it."),
            ("ANTI_BRIBERY", "Anti-Bribery & Corruption", TrainingEvidenceSource.Compliance, 12, 20,
                "Anti-bribery training. The completion record lives in compliance-service."),
            ("DATA_PROTECTION", "Data Protection", TrainingEvidenceSource.Hr, 12, 30,
                "Data protection training. HR owns this record because no other module does."),
        };

        var created = 0;
        foreach (var s in seed)
        {
            if (await requirements.Query().AnyAsync(r => r.Code == s.Code)) continue;
            await requirements.CreateAsync(new MandatoryTrainingRequirement
            {
                Code = s.Code, Name = s.Name, Description = s.Description,
                EvidenceSource = s.Source, ValidityMonths = s.Validity,
                BlocksIncrement = true, GraceDays = 30, DisplayOrder = s.Order,
                CreatedBy = userId, UpdatedBy = userId,
            });
            created++;
        }

        if (created == 0) return new LearningActionResult("NoChange", "The mandatory-training matrix is already installed.");

        var result = new LearningActionResult("Created", $"{created} mandatory requirement(s) installed.");
        result.Warnings.Add("Validity is seeded at 12 months for all three — check that against your own policy.");
        result.Warnings.Add("HSE and anti-bribery evidence is read from hse-service and compliance-service. If either is unreachable those requirements report as UNKNOWN, not as failed, and do not block an increment.");

        await LogAsync("MandatoryTrainingRequirement", "seed", HrAuditAction.MandatoryRequirementConfigured,
            $"Seeded {created} mandatory training requirement(s).", userId);
        return result;
    }

    public async Task<LearningActionResult> SaveRequirementAsync(string? id, SaveMandatoryRequirementDto dto, string userId)
    {
        var code = (dto.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) return Err("A requirement needs a code.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A requirement needs a name.");
        if (!Enum.TryParse<TrainingEvidenceSource>(dto.EvidenceSource, true, out var evidenceSource))
            return Err("The evidence source must be Hr, Hse or Compliance.");
        if (dto.ValidityMonths is <= 0) return Err("Validity must be a positive number of months, or left blank for no expiry.");
        if (dto.GraceDays < 0) return Err("The grace period cannot be negative.");

        MandatoryTrainingRequirement req;
        if (string.IsNullOrWhiteSpace(id))
        {
            if (await requirements.Query().AnyAsync(r => r.Code == code))
                return Err($"Requirement '{code}' already exists.");
            req = new MandatoryTrainingRequirement { Code = code, CreatedBy = userId, UpdatedBy = userId };
        }
        else
        {
            var found = await requirements.GetByIdAsync(id!);
            if (found is null || found.IsDeleted) return Err("Requirement not found.");
            req = found;
        }

        req.Name = dto.Name.Trim();
        req.Description = dto.Description;
        req.EvidenceSource = evidenceSource;
        req.ValidityMonths = dto.ValidityMonths;
        req.BlocksIncrement = dto.BlocksIncrement;
        req.GraceDays = dto.GraceDays;
        req.DepartmentId = dto.DepartmentId;
        req.DisplayOrder = dto.DisplayOrder;
        if (dto.IsActive.HasValue) req.IsActive = dto.IsActive.Value;
        Touch(req, userId);

        var saved = string.IsNullOrWhiteSpace(id)
            ? await requirements.CreateAsync(req)
            : await requirements.UpdateAsync(req);

        await LogAsync("MandatoryTrainingRequirement", saved.Id, HrAuditAction.MandatoryRequirementConfigured,
            $"Requirement {saved.Code} saved — evidence from {saved.EvidenceSource}, valid {saved.ValidityMonths?.ToString() ?? "indefinitely"} month(s), blocks increment: {saved.BlocksIncrement}.", userId);
        return new LearningActionResult(string.IsNullOrWhiteSpace(id) ? "Created" : "Updated", $"{saved.Name} saved.", saved.Id);
    }

    public async Task<List<EmployeeComplianceDto>> GetComplianceAsync(string? employeeId, CancellationToken ct = default)
    {
        var reqs = await requirements.Query().AsNoTracking().Where(r => r.IsActive)
            .OrderBy(r => r.DisplayOrder).ToListAsync(ct);
        if (reqs.Count == 0) return [];

        var q = employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status));
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(e => e.Id == employeeId);
        var staff = await q.OrderBy(e => e.EmployeeNumber).ToListAsync(ct);
        if (staff.Count == 0) return [];

        var userIds = staff.Where(e => !string.IsNullOrWhiteSpace(e.UserId)).Select(e => e.UserId!).ToList();

        // Read each external source ONCE for the whole set, not once per employee.
        var needsHse = reqs.Any(r => r.EvidenceSource == TrainingEvidenceSource.Hse);
        var needsCompliance = reqs.Any(r => r.EvidenceSource == TrainingEvidenceSource.Compliance);
        var hse = needsHse && userIds.Count > 0 ? await evidence.ListHseTrainingAsync(userIds, ct) : (needsHse ? null : []);
        var abc = needsCompliance && userIds.Count > 0 ? await evidence.ListAntiBriberyTrainingAsync(userIds, ct) : (needsCompliance ? null : []);

        // HR's own evidence: the latest training event carrying each requirement's code.
        var hrCodes = reqs.Where(r => r.EvidenceSource == TrainingEvidenceSource.Hr).Select(r => r.Code).ToList();
        var hrEvents = hrCodes.Count == 0 ? [] : await trainingEvents.Query().AsNoTracking()
            .Where(t => t.MandatoryTrainingCode != null && hrCodes.Contains(t.MandatoryTrainingCode))
            .ToListAsync(ct);
        var hrEventIds = hrEvents.Select(t => t.Id).ToList();
        var hrLogs = hrEventIds.Count == 0 ? [] : await hoursLog.Query().AsNoTracking()
            .Where(l => hrEventIds.Contains(l.TrainingEventId)).ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var result = new List<EmployeeComplianceDto>();

        foreach (var e in staff)
        {
            var row = new EmployeeComplianceDto
            {
                EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.FullName,
                DepartmentId = e.DepartmentId, HasLoginAccount = !string.IsNullOrWhiteSpace(e.UserId),
            };

            foreach (var req in reqs.Where(r => r.DepartmentId is null || r.DepartmentId == e.DepartmentId))
            {
                var status = req.EvidenceSource switch
                {
                    TrainingEvidenceSource.Hse => FromEvidence(req, hse, e, today),
                    TrainingEvidenceSource.Compliance => FromEvidence(req, abc, e, today),
                    _ => FromHrLog(req, hrEvents, hrLogs, e, today),
                };
                row.Requirements.Add(status);
            }

            row.Breached = row.Requirements.Count(r => r.State == nameof(MandatoryTrainingState.Breached));
            row.Unknown = row.Requirements.Count(r => r.State == nameof(MandatoryTrainingState.Unknown));
            row.AnyBlocksIncrement = row.Requirements.Any(r => r.BlocksIncrement);
            result.Add(row);
        }
        return result;
    }

    /// <summary>Status from an external source. A null list means the source could not be read — reported as
    /// unknown, which never blocks anything.</summary>
    private static MandatoryStatusDto FromEvidence(MandatoryTrainingRequirement req, List<TrainingEvidenceDto>? records, Employee e, DateTime today)
    {
        if (records is null)
            return Status(req, MandatoryTrainingState.Unknown, null, null, today,
                $"{req.EvidenceSource}-service could not be read, so this could not be checked.");
        if (string.IsNullOrWhiteSpace(e.UserId))
            return Status(req, MandatoryTrainingState.Unknown, null, null, today,
                "This employee has no login account, so there is no record to look up.");

        var latest = records.Where(r => r.EmployeeUserId == e.UserId)
            .OrderByDescending(r => r.CompletedOn).FirstOrDefault();
        if (latest is null)
            return Status(req, MandatoryTrainingState.NeverCompleted, null, null, today, "No completion on record.");

        var expiry = latest.ExpiresOn ?? Expiry(latest.CompletedOn, req.ValidityMonths);
        return Status(req, Classify(expiry, req.GraceDays, today), latest.CompletedOn, expiry, today,
            $"Completed {latest.CompletedOn:dd MMM yyyy}{(latest.Course is { Length: > 0 } ? $" ({latest.Course})" : "")}.");
    }

    private static MandatoryStatusDto FromHrLog(MandatoryTrainingRequirement req, List<TrainingEvent> events,
        List<TrainingHoursLog> logs, Employee e, DateTime today)
    {
        var eventIds = events.Where(t => t.MandatoryTrainingCode == req.Code).Select(t => t.Id).ToHashSet();
        var latest = logs.Where(l => l.EmployeeId == e.Id && eventIds.Contains(l.TrainingEventId))
            .OrderByDescending(l => l.TrainingDate).FirstOrDefault();
        if (latest is null)
            return Status(req, MandatoryTrainingState.NeverCompleted, null, null, today, "No completion on record.");

        var expiry = Expiry(latest.TrainingDate, req.ValidityMonths);
        return Status(req, Classify(expiry, req.GraceDays, today), latest.TrainingDate, expiry, today,
            $"Completed {latest.TrainingDate:dd MMM yyyy} ({latest.TrainingTitle}).");
    }

    private static DateTime? Expiry(DateTime completedOn, int? validityMonths)
        => validityMonths is > 0 ? completedOn.Date.AddMonths(validityMonths.Value) : null;

    private static MandatoryTrainingState Classify(DateTime? expiry, int graceDays, DateTime today)
    {
        if (expiry is null) return MandatoryTrainingState.Valid;         // never expires
        var days = (today - expiry.Value.Date).Days;
        if (days < 0) return days >= -DueSoonDays ? MandatoryTrainingState.DueSoon : MandatoryTrainingState.Valid;
        return days > graceDays ? MandatoryTrainingState.Breached : MandatoryTrainingState.Overdue;
    }

    private static MandatoryStatusDto Status(MandatoryTrainingRequirement req, MandatoryTrainingState state,
        DateTime? completed, DateTime? expiry, DateTime today, string detail)
    {
        // Only a real, checked lapse blocks pay. "Unknown" and "overdue but inside the grace period" do not:
        // one is a failure to ask, the other is a warning the policy itself allows for.
        var blocks = req.BlocksIncrement
            && state is MandatoryTrainingState.Breached or MandatoryTrainingState.NeverCompleted;

        return new MandatoryStatusDto
        {
            RequirementCode = req.Code,
            RequirementName = req.Name,
            EvidenceSource = req.EvidenceSource.ToString(),
            State = state.ToString(),
            CompletedOn = completed,
            ExpiresOn = expiry,
            DaysOverdue = expiry is not null && expiry.Value.Date < today ? (today - expiry.Value.Date).Days : null,
            BlocksIncrement = blocks,
            Detail = detail,
        };
    }

    public async Task<IncrementEligibilityDto?> GetIncrementEligibilityAsync(string employeeId, CancellationToken ct = default)
    {
        var employee = await employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return null;

        var dto = new IncrementEligibilityDto
        {
            EmployeeId = employee.Id, EmployeeNumber = employee.EmployeeNumber, EmployeeName = employee.FullName,
        };

        // HR-029 — mandatory training.
        var compliance = (await GetComplianceAsync(employeeId, ct)).FirstOrDefault();
        foreach (var r in compliance?.Requirements ?? [])
        {
            // A blocker has to say WHY it blocks. "Completed 01 Jun 2022" is a fact, not a reason — the person
            // reading this is deciding whether to withhold someone's pay rise.
            if (r.BlocksIncrement)
                dto.Blockers.Add(r.State == nameof(MandatoryTrainingState.NeverCompleted)
                    ? $"{r.RequirementName}: never completed."
                    : $"{r.RequirementName}: lapsed {r.DaysOverdue} day(s) ago (expired {r.ExpiresOn:dd MMM yyyy}). {r.Detail}");
            else if (r.State is nameof(MandatoryTrainingState.Unknown))
                dto.Warnings.Add($"{r.RequirementName} could not be checked — {r.Detail}");
            else if (r.State is nameof(MandatoryTrainingState.Overdue) or nameof(MandatoryTrainingState.DueSoon))
                dto.Warnings.Add($"{r.RequirementName} is {r.State.ToLowerInvariant()} ({r.Detail})");
        }

        // HR-035 — a lapsed professional certification blocks the increment too.
        var expired = await certifications.Query().AsNoTracking()
            .Where(c => c.EmployeeId == employeeId && c.ExpiryDate != null && c.ExpiryDate < DateTime.UtcNow.Date)
            .ToListAsync(ct);
        foreach (var c in expired)
            dto.Blockers.Add($"Professional certification expired: {c.CertificationName} ({c.ExpiryDate:dd MMM yyyy}).");

        // The hours target is reported but does NOT block on its own — HR-026 is a target, HR-029/035 are gates.
        var hours = (await ListHoursAsync(DateTime.UtcNow.Year, null)).FirstOrDefault(h => h.EmployeeId == employeeId);
        dto.HoursYtd = hours?.HoursYtd ?? 0m;
        dto.TargetHours = hours?.TargetHours ?? DefaultAnnualHours;
        if (dto.HoursYtd < dto.TargetHours)
            dto.Warnings.Add($"Training hours are {dto.HoursYtd:0.##} of a {dto.TargetHours} hour target.");

        dto.Eligible = dto.Blockers.Count == 0;
        return dto;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Knowledge sharing (P25)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<KnowledgeSharingSessionDto>> ListSessionsAsync(int? year, int? month)
    {
        var q = sessions.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(s => s.SessionDate.Year == year.Value);
        if (month.HasValue) q = q.Where(s => s.SessionDate.Month == month.Value);

        var list = await q.OrderByDescending(s => s.SessionDate).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(s => s.Id).ToList();
        var att = await attendance.Query().AsNoTracking()
            .Where(a => ids.Contains(a.KnowledgeSharingSessionId)).ToListAsync();

        return list.Select(s => new KnowledgeSharingSessionDto
        {
            Id = s.Id, Topic = s.Topic, Description = s.Description,
            FacilitatorEmployeeId = s.FacilitatorEmployeeId, FacilitatorName = s.FacilitatorName,
            SessionDate = s.SessionDate, DurationHours = s.DurationHours,
            AttendeeCount = s.AttendeeCount, TrainingEventId = s.TrainingEventId, Notes = s.Notes,
            Attendees = att.Where(a => a.KnowledgeSharingSessionId == s.Id).Select(a => new TrainingAttendeeDto
            {
                Id = a.Id, EmployeeId = a.EmployeeId, EmployeeNumber = a.EmployeeNumber,
                EmployeeName = a.EmployeeName, Hours = s.DurationHours, TrainingDate = s.SessionDate,
            }).ToList(),
        }).ToList();
    }

    public async Task<LearningActionResult> LogSessionAsync(SaveKnowledgeSharingDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Topic)) return Err("A session needs a topic.");
        if (dto.DurationHours <= 0) return Err("A session needs a duration in hours.");

        var facilitator = await employees.GetByIdAsync(dto.FacilitatorEmployeeId ?? string.Empty);
        if (facilitator is null || facilitator.IsDeleted) return Err("Facilitator not found.");
        if (!Active.Contains(facilitator.Status)) return Err($"{facilitator.FullName} is {facilitator.Status}.");

        var date = DateTime.SpecifyKind(dto.SessionDate.Date, DateTimeKind.Utc);
        if (date > DateTime.UtcNow.Date.AddDays(1))
            return Err("A session cannot be logged before it has happened.");

        var attendeeIds = dto.AttendeeEmployeeIds.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList();
        if (attendeeIds.Count == 0) return Err("A session needs at least one attendee.");
        var people = await employees.Query().Where(e => attendeeIds.Contains(e.Id)).ToListAsync();
        if (people.Count != attendeeIds.Count) return Err("One or more attendees are not employees on file.");

        var session = await sessions.CreateAsync(new KnowledgeSharingSession
        {
            Topic = dto.Topic.Trim(),
            Description = dto.Description,
            FacilitatorEmployeeId = facilitator.Id,
            FacilitatorName = facilitator.FullName,
            SessionDate = date,
            DurationHours = dto.DurationHours,
            AttendeeCount = people.Count,
            Notes = dto.Notes,
            CreatedBy = userId, UpdatedBy = userId,
        });

        foreach (var p in people)
            await attendance.CreateAsync(new KnowledgeSharingAttendance
            {
                KnowledgeSharingSessionId = session.Id,
                EmployeeId = p.Id, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.FullName,
                CreatedBy = userId, UpdatedBy = userId,
            });

        // P25 step 25.3 — attendees' hours go through the SAME ledger as external courses, so internal sharing
        // counts towards the annual target instead of being a parallel record nobody measures.
        var training = await LogTrainingAsync(new SaveTrainingEventDto
        {
            Title = $"Knowledge sharing: {session.Topic}",
            Provider = facilitator.FullName,
            Source = nameof(TrainingSource.KnowledgeSharing),
            TrainingDate = date,
            DurationHours = dto.DurationHours,
            Cost = 0m,
            Attendees = people.Select(p => new TrainingAttendeeInputDto { EmployeeId = p.Id }).ToList(),
        }, null, userId);

        session.TrainingEventId = training.Id;
        Touch(session, userId);
        await sessions.UpdateAsync(session);

        var result = new LearningActionResult("Logged",
            $"'{session.Topic}' logged — {people.Count} attendee(s) credited with {dto.DurationHours:0.##} hour(s) each.", session.Id);
        result.Warnings.AddRange(training.Warnings);

        await LogAsync("KnowledgeSharingSession", session.Id, HrAuditAction.KnowledgeSessionLogged,
            $"'{session.Topic}' facilitated by {facilitator.FullName} on {date:dd MMM yyyy} — {people.Count} attendee(s), {dto.DurationHours:0.##} h each.", userId);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // L&D budgets (P26)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<LdBudgetDto>> ListBudgetsAsync(int? year)
    {
        var q = budgets.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(b => b.Year == year.Value);
        var list = await q.OrderByDescending(b => b.Year).ThenBy(b => b.DepartmentName).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<LearningActionResult> SaveBudgetAsync(SaveLdBudgetDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.DepartmentId)) return Err("A budget needs a department.");
        if (dto.BudgetedAmount < 0) return Err("A budget cannot be negative.");
        var year = dto.Year ?? DateTime.UtcNow.Year;

        var budget = await budgets.Query().FirstOrDefaultAsync(b => b.DepartmentId == dto.DepartmentId && b.Year == year);
        var isNew = budget is null;
        budget ??= new LdBudget { DepartmentId = dto.DepartmentId, Year = year, CreatedBy = userId, UpdatedBy = userId };

        budget.BudgetedAmount = dto.BudgetedAmount;
        budget.Notes = dto.Notes;
        if (dto.IsActive.HasValue) budget.IsActive = dto.IsActive.Value;
        // Raising the budget re-opens the thresholds: a stamp left from the old figure would silence the
        // warning that the new figure has not yet reached.
        if (!isNew)
        {
            if (budget.ActualSpend < budget.BudgetedAmount * 0.8m) budget.Warning80SentAt = null;
            if (budget.ActualSpend < budget.BudgetedAmount) budget.Exhausted100SentAt = null;
        }
        Touch(budget, userId);

        var saved = isNew ? await budgets.CreateAsync(budget) : await budgets.UpdateAsync(budget);
        await RecomputeBudgetAsync(saved.DepartmentId, saved.Year, null, userId);

        await LogAsync("LdBudget", saved.Id, HrAuditAction.LdBudgetConfigured,
            $"L&D budget for {saved.DepartmentName ?? saved.DepartmentId} {saved.Year} set to {saved.BudgetedAmount:N2}.", userId);
        return new LearningActionResult(isNew ? "Created" : "Updated",
            $"{saved.Year} L&D budget set to {saved.BudgetedAmount:N2}.", saved.Id);
    }

    /// <summary>
    /// Recomputes a department's spend from its training events and announces the 80% and 100% thresholds once
    /// each. Recomputing rather than incrementing means a corrected cost corrects the spend.
    /// </summary>
    private async Task<List<string>> RecomputeBudgetAsync(string departmentId, int year, string? tenantSchema, string userId)
    {
        var warnings = new List<string>();
        var budget = await budgets.Query().FirstOrDefaultAsync(b => b.DepartmentId == departmentId && b.Year == year);
        if (budget is null)
        {
            warnings.Add($"No L&D budget is set for this department in {year}, so the cost is not tracked against one.");
            return warnings;
        }

        var spend = await trainingEvents.Query().AsNoTracking()
            .Where(t => t.DepartmentId == departmentId && t.TrainingDate.Year == year)
            .SumAsync(t => t.Cost);
        budget.ActualSpend = Round(spend);
        Touch(budget, userId);

        if (budget.BudgetedAmount > 0)
        {
            var used = budget.ActualSpend / budget.BudgetedAmount;
            if (used >= 1m && budget.Exhausted100SentAt is null)
            {
                budget.Exhausted100SentAt = DateTime.UtcNow;
                warnings.Add($"The {year} L&D budget for this department is fully spent ({budget.ActualSpend:N2} of {budget.BudgetedAmount:N2}) — further spend needs a reallocation.");
                await NotifyAsync(tenantSchema, "Critical",
                    $"L&D budget exhausted — {budget.DepartmentName ?? departmentId} {year}",
                    $"Spend of {budget.ActualSpend:N2} has reached the {budget.BudgetedAmount:N2} budget. No further training should be booked without a reallocation.", "hr.write");
                await LogAsync("LdBudget", budget.Id, HrAuditAction.LdBudgetThresholdReached,
                    $"L&D budget {year} for {budget.DepartmentName ?? departmentId} reached 100% ({budget.ActualSpend:N2}/{budget.BudgetedAmount:N2}).", userId);
            }
            else if (used >= 0.8m && budget.Warning80SentAt is null)
            {
                budget.Warning80SentAt = DateTime.UtcNow;
                warnings.Add($"The {year} L&D budget for this department is {used * 100:0.#}% spent.");
                await NotifyAsync(tenantSchema, "Warning",
                    $"L&D budget 80% spent — {budget.DepartmentName ?? departmentId} {year}",
                    $"Spend of {budget.ActualSpend:N2} against a {budget.BudgetedAmount:N2} budget.", "hr.write");
                await LogAsync("LdBudget", budget.Id, HrAuditAction.LdBudgetThresholdReached,
                    $"L&D budget {year} for {budget.DepartmentName ?? departmentId} reached 80%.", userId);
            }
        }

        await budgets.UpdateAsync(budget);
        return warnings;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // The L&D sweep — every red flag, run daily and idempotent
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<LearningSweepResultDto> RunLearningSweepAsync(string? tenantSchema, string userId, CancellationToken ct = default)
    {
        var result = new LearningSweepResultDto();
        var today = DateTime.UtcNow.Date;
        var year = today.Year;

        var staff = await employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status)).ToListAsync(ct);
        if (staff.Count == 0) { result.Notes.Add("No active employees."); return result; }

        // ── HR-036: the LDP deadline. Catch-up rather than calendar-triggered, for the same reason H3's
        // carry-forward is: a job that only fires ON 15 January never runs for a tenant that was down that day.
        var dueDate = new DateTime(year, LdpDueMonth, LdpDueDay, 0, 0, 0, DateTimeKind.Utc);
        var escalateDate = new DateTime(year, LdpEscalateMonth, LdpEscalateDay, 0, 0, 0, DateTimeKind.Utc);
        if (today >= dueDate)
        {
            var yearPlans = await plans.Query().Where(p => p.PlanYear == year).ToListAsync(ct);
            foreach (var e in staff)
            {
                var plan = yearPlans.FirstOrDefault(p => p.EmployeeId == e.Id);
                if (plan?.Status == LdpStatus.Approved) continue;

                if (plan is null)
                {
                    plan = await plans.CreateAsync(new LearningDevelopmentPlan
                    {
                        EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.FullName,
                        DepartmentId = e.DepartmentId, PlanYear = year, Status = LdpStatus.Draft,
                        Notes = "Opened automatically by the deadline sweep — no plan had been filed.",
                        CreatedBy = userId, UpdatedBy = userId,
                    });
                }

                if (plan.ReminderSentAt is null)
                {
                    plan.ReminderSentAt = DateTime.UtcNow;
                    Touch(plan, userId);
                    await plans.UpdateAsync(plan);
                    result.LdpRemindersSent++;
                    await NotifyAsync(tenantSchema, "Warning",
                        $"{year} learning plan overdue — {e.FullName}",
                        $"{e.EmployeeNumber} {e.FullName} has no approved {year} learning and development plan. Plans were due by {dueDate:dd MMMM}.",
                        "hr.manager", e.UserId);
                    await LogAsync("LearningDevelopmentPlan", plan.Id, HrAuditAction.LdpReminderSent,
                        $"{e.EmployeeNumber}: {year} LDP reminder sent — plan was due {dueDate:dd MMM}.", userId);
                }
                else if (today >= escalateDate && plan.EscalatedAt is null)
                {
                    plan.EscalatedAt = DateTime.UtcNow;
                    Touch(plan, userId);
                    await plans.UpdateAsync(plan);
                    result.LdpEscalations++;
                    // A distinct title per tier — the H2 lesson: ticketing dedupes on (tenant, source, title),
                    // so reusing the reminder's title would silently swallow the escalation.
                    await NotifyAsync(tenantSchema, "Critical",
                        $"ESCALATION: {year} learning plan still missing — {e.FullName}",
                        $"{e.EmployeeNumber} {e.FullName} still has no approved {year} plan, past the {escalateDate:dd MMMM} escalation date (HR-036).",
                        "hr.approve", e.UserId);
                    await LogAsync("LearningDevelopmentPlan", plan.Id, HrAuditAction.LdpEscalated,
                        $"{e.EmployeeNumber}: {year} LDP escalated to the MD.", userId);
                }
            }
        }

        // ── HR-033: zero training hours by 30 June.
        var zeroCheck = new DateTime(year, ZeroHoursCheckMonth, ZeroHoursCheckDay, 0, 0, 0, DateTimeKind.Utc);
        if (today >= zeroCheck)
        {
            var logged = await hoursLog.Query().AsNoTracking()
                .Where(l => l.TrainingDate.Year == year).Select(l => l.EmployeeId).Distinct().ToListAsync(ct);
            foreach (var e in staff.Where(e => !logged.Contains(e.Id)))
            {
                if (!await RaiseOnceAsync("ZeroHours", $"{e.Id}:{year}", e.Id,
                        $"No training hours in {year}.", userId, ct)) continue;
                result.ZeroHoursFlags++;
                await NotifyAsync(tenantSchema, "Warning",
                    $"No training hours in {year} — {e.FullName}",
                    $"{e.EmployeeNumber} {e.FullName} has logged no training at all in {year}, past the {zeroCheck:dd MMMM} checkpoint (HR-033).",
                    "hr.manager", e.UserId);
                await LogAsync("Employee", e.Id, HrAuditAction.ZeroTrainingHoursFlagged,
                    $"{e.EmployeeNumber}: zero training hours in {year} at the 30 June checkpoint.", userId);
            }
        }

        // ── HR-034: mandatory training breached beyond its grace period.
        var compliance = await GetComplianceAsync(null, ct);
        foreach (var row in compliance)
        {
            foreach (var r in row.Requirements.Where(r => r.State == nameof(MandatoryTrainingState.Breached)))
            {
                // Keyed on the expiry too, so a NEW lapse of the same training is announced again while the
                // same one is not re-announced every morning.
                var key = $"{row.EmployeeId}:{r.RequirementCode}:{r.ExpiresOn:yyyy-MM-dd}";
                if (!await RaiseOnceAsync("MandatoryBreach", key, row.EmployeeId,
                        $"{r.RequirementCode} breached by {r.DaysOverdue} day(s).", userId, ct)) continue;
                result.MandatoryBreachAlerts++;
                await NotifyAsync(tenantSchema, "Critical",
                    $"Mandatory training breached — {r.RequirementName}, {row.EmployeeName}",
                    $"{row.EmployeeNumber} {row.EmployeeName}: {r.RequirementName} lapsed {r.DaysOverdue} day(s) ago. {r.Detail} This blocks any salary increment (HR-029/HR-034).",
                    "hr.approve");
                await LogAsync("Employee", row.EmployeeId, HrAuditAction.MandatoryTrainingBreached,
                    $"{row.EmployeeNumber}: {r.RequirementCode} breached by {r.DaysOverdue} day(s).", userId);
            }
        }
        var unknown = compliance.Sum(c => c.Unknown);
        if (unknown > 0)
            result.Notes.Add($"{unknown} requirement check(s) could not be completed because an evidence source was unreachable — these are NOT treated as breaches.");

        // ── HR-030: fewer than two knowledge-sharing sessions in a completed month.
        var lastMonth = new DateTime(year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        var monthCount = await sessions.Query().AsNoTracking()
            .CountAsync(s => s.SessionDate.Year == lastMonth.Year && s.SessionDate.Month == lastMonth.Month, ct);
        if (monthCount < MonthlySessionTarget
            && await RaiseOnceAsync("KnowledgeSharingShortfall", lastMonth.ToString("yyyy-MM"), null,
                $"{monthCount} session(s) against a target of {MonthlySessionTarget}.", userId, ct))
        {
            result.KnowledgeSharingShortfalls++;
            await NotifyAsync(tenantSchema, "Warning",
                $"Knowledge sharing below target — {lastMonth:MMMM yyyy}",
                $"{monthCount} session(s) were held in {lastMonth:MMMM yyyy} against a target of {MonthlySessionTarget} (HR-030).",
                "hr.write");
            await LogAsync("KnowledgeSharingSession", lastMonth.ToString("yyyy-MM"), HrAuditAction.KnowledgeSharingShortfall,
                $"{lastMonth:MMMM yyyy}: {monthCount} knowledge-sharing session(s) against a target of {MonthlySessionTarget}.", userId);
        }

        // ── HR-031: budget thresholds, recomputed so a corrected cost is reflected.
        var yearBudgets = await budgets.Query().AsNoTracking().Where(b => b.Year == year && b.IsActive).ToListAsync(ct);
        foreach (var b in yearBudgets)
        {
            var before80 = b.Warning80SentAt is not null;
            var before100 = b.Exhausted100SentAt is not null;
            await RecomputeBudgetAsync(b.DepartmentId, year, tenantSchema, userId);
            var after = await budgets.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == b.Id, ct);
            if (!before80 && after?.Warning80SentAt is not null) result.BudgetWarnings++;
            if (!before100 && after?.Exhausted100SentAt is not null) result.BudgetsExhausted++;
        }

        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════
    private static LdpDto ToDto(LearningDevelopmentPlan p, List<LdpObjective> objs)
    {
        var today = DateTime.UtcNow.Date;
        return new LdpDto
        {
            Id = p.Id, EmployeeId = p.EmployeeId, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.EmployeeName,
            PlanYear = p.PlanYear, Status = p.Status.ToString(),
            SubmittedAt = p.SubmittedAt, ApprovedBy = p.ApprovedBy, ApprovedAt = p.ApprovedAt,
            RejectionReason = p.RejectionReason, ReminderSentAt = p.ReminderSentAt, EscalatedAt = p.EscalatedAt,
            Notes = p.Notes,
            ObjectivesCompleted = objs.Count(o => o.Status == LdpObjectiveStatus.Completed),
            Objectives = objs.OrderBy(o => o.TargetDate ?? DateTime.MaxValue).Select(o => new LdpObjectiveDto
            {
                Id = o.Id, Objective = o.Objective, Activity = o.Activity, TargetDate = o.TargetDate,
                Status = o.Status.ToString(), CompletionDate = o.CompletionDate,
                CompletedByTrainingId = o.CompletedByTrainingId, Notes = o.Notes,
                IsOverdue = o.Status != LdpObjectiveStatus.Completed && o.TargetDate is not null && o.TargetDate.Value.Date < today,
            }).ToList(),
        };
    }

    private static TrainingEventDto ToDto(TrainingEvent t, List<TrainingHoursLog> logs) => new()
    {
        Id = t.Id, Title = t.Title, Provider = t.Provider, Description = t.Description,
        Source = t.Source.ToString(), TrainingDate = t.TrainingDate, DurationHours = t.DurationHours,
        Cost = t.Cost, CurrencyCode = t.CurrencyCode,
        DepartmentId = t.DepartmentId, DepartmentName = t.DepartmentName,
        MandatoryTrainingCode = t.MandatoryTrainingCode, CertificateUrl = t.CertificateUrl,
        KnowledgeSharingSessionId = t.KnowledgeSharingSessionId,
        AttendeeCount = logs.Count,
        TotalHoursAwarded = Round(logs.Sum(l => l.Hours)),
        Attendees = logs.OrderBy(l => l.EmployeeNumber).Select(l => new TrainingAttendeeDto
        {
            Id = l.Id, EmployeeId = l.EmployeeId, EmployeeNumber = l.EmployeeNumber, EmployeeName = l.EmployeeName,
            Hours = l.Hours, TrainingDate = l.TrainingDate, TrainingTitle = l.TrainingTitle,
            LdpObjectiveId = l.LdpObjectiveId, VerifiedBy = l.VerifiedBy,
        }).ToList(),
    };

    private static MandatoryRequirementDto ToDto(MandatoryTrainingRequirement r) => new()
    {
        Id = r.Id, Code = r.Code, Name = r.Name, Description = r.Description,
        EvidenceSource = r.EvidenceSource.ToString(), ValidityMonths = r.ValidityMonths,
        BlocksIncrement = r.BlocksIncrement, GraceDays = r.GraceDays,
        DepartmentId = r.DepartmentId, IsActive = r.IsActive, DisplayOrder = r.DisplayOrder,
    };

    private static LdBudgetDto ToDto(LdBudget b) => new()
    {
        Id = b.Id, DepartmentId = b.DepartmentId, DepartmentName = b.DepartmentName, Year = b.Year,
        BudgetedAmount = b.BudgetedAmount, ActualSpend = b.ActualSpend,
        Remaining = Round(b.BudgetedAmount - b.ActualSpend),
        PercentUsed = b.BudgetedAmount > 0 ? Math.Round(b.ActualSpend / b.BudgetedAmount * 100m, 1) : 0m,
        CurrencyCode = b.CurrencyCode,
        Warning80SentAt = b.Warning80SentAt, Exhausted100SentAt = b.Exhausted100SentAt,
        IsActive = b.IsActive, Notes = b.Notes,
    };

    /// <summary>
    /// Records a red flag the first time it is seen and returns whether it is new. A daily sweep that
    /// re-announced every standing breach would fill the audit trail with identical rows and report old news
    /// as new every morning; ticketing would dedupe the alert but nothing would dedupe the record.
    /// </summary>
    private async Task<bool> RaiseOnceAsync(string flagType, string flagKey, string? employeeId, string detail, string userId, CancellationToken ct)
    {
        if (await redFlags.Query().AnyAsync(f => f.FlagType == flagType && f.FlagKey == flagKey, ct)) return false;
        await redFlags.CreateAsync(new LearningRedFlag
        {
            FlagType = flagType, FlagKey = flagKey, EmployeeId = employeeId, Detail = detail,
            RaisedAt = DateTime.UtcNow, CreatedBy = userId, UpdatedBy = userId,
        });
        return true;
    }

    private async Task NotifyAsync(string? schema, string severity, string title, string message,
        string permission, string? assignedToUserId = null)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission, assignedToUserId);
    }

    private static decimal Round(decimal value) => Money.Round(value);
    private static LearningActionResult Err(string message) => new("Error", message);
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
