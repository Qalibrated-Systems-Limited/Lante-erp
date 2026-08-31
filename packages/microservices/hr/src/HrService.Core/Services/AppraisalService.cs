using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Appraisals;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H9 (P14–P17) — KPI scorecards and targets, the appraisal workflow, 360 feedback and improvement plans.
/// <para><b>The four steps are enforced in order</b> (HR-018). An appraisal that reached the MD without the
/// employee ever completing their self-assessment is not an appraisal, it is a verdict — so each step refuses
/// to run until the one before it has stamped its timestamp.</para>
/// <para><b>Scores and targets are COPIED onto the appraisal at each step, never read through.</b> A signed
/// appraisal has to keep showing the numbers it was signed on, even after the year-to-date figures move and
/// the scorecard is rewritten for next year.</para>
/// <para><b>360 anonymity is a property of the read model.</b> No method returns a reviewer next to their
/// score; only counts, averages and the list of people yet to respond — a non-response carries no rating, so
/// naming it gives nothing away.</para>
/// </summary>
public class AppraisalService(
    IGenericRepository<Employee> employees,
    IGenericRepository<Position> positions,
    IGenericRepository<AttendanceScorecard> attendanceScorecards,
    IGenericRepository<KpiScorecard> scorecards,
    IGenericRepository<KpiScorecardItem> scorecardItems,
    IGenericRepository<KpiTarget> targets,
    IGenericRepository<AppraisalCycle> cycles,
    IGenericRepository<Appraisal> appraisals,
    IGenericRepository<AppraisalItemScore> itemScores,
    IGenericRepository<Feedback360> feedback,
    IGenericRepository<PerformanceImprovementPlan> pips,
    IGenericRepository<HrAuditLog> audit,
    ICrmRevenueGateway crm,
    IHrAlertGateway notifier) : IAppraisalService
{
    private static readonly EmploymentStatus[] Active =
        [EmploymentStatus.OnProbation, EmploymentStatus.Active, EmploymentStatus.OnLeave, EmploymentStatus.Suspended];

    /// <summary>Over-achievement counts, but not without limit: 150% of target is the most a single item can
    /// contribute, so one runaway number cannot carry an otherwise poor year.</summary>
    private const decimal MaxQuantitativeScore = 150m;

    /// <summary>How long a PIP runs before its first review, when nobody sets a date.</summary>
    private const int DefaultPipReviewDays = 60;

    // ══════════════════════════════════════════════════════════════════════════════
    // Summary
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<AppraisalSummaryDto> GetSummaryAsync(int? year)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var today = DateTime.UtcNow.Date;

        var cards = await scorecards.Query().AsNoTracking().Where(s => s.Year == y && s.IsActive).ToListAsync();
        var cardIds = cards.Select(c => c.Id).ToList();
        var items = cardIds.Count == 0 ? [] : await scorecardItems.Query().AsNoTracking()
            .Where(i => cardIds.Contains(i.KpiScorecardId) && i.IsActive).ToListAsync();

        var staff = await employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status)).ToListAsync();
        var withTargets = await targets.Query().AsNoTracking().Where(t => t.Year == y)
            .Select(t => t.EmployeeId).Distinct().ToListAsync();

        var openCycle = await cycles.Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Year == y && c.Status == AppraisalCycleStatus.Open);
        var cycleAppraisals = openCycle is null ? [] : await appraisals.Query().AsNoTracking()
            .Where(a => a.AppraisalCycleId == openCycle.Id).ToListAsync();

        var outstanding360 = await feedback.Query().AsNoTracking().CountAsync(f => f.SubmittedAt == null);
        var activePips = await pips.Query().AsNoTracking()
            .Where(p => p.Status == PipStatus.Active || p.Status == PipStatus.Extended).ToListAsync();

        var covered = cards.Where(c => c.PositionId is not null).Select(c => c.PositionId!).ToHashSet();
        var hasCompanyDefault = cards.Any(c => c.PositionId is null);

        return new AppraisalSummaryDto
        {
            Year = y,
            Scorecards = cards.Count,
            ScorecardsWithInvalidWeights = cards.Count(c =>
                Round(items.Where(i => i.KpiScorecardId == c.Id).Sum(i => i.WeightPercent)) != 100m),
            EmployeesWithTargets = withTargets.Count,
            EmployeesWithoutScorecard = hasCompanyDefault ? 0
                : staff.Count(e => e.PositionId is null || !covered.Contains(e.PositionId)),

            OpenCycleName = openCycle?.Name,
            Appraisals = cycleAppraisals.Count,
            AwaitingSelf = cycleAppraisals.Count(a => a.Status == AppraisalStatus.PendingSelf),
            AwaitingLineManager = cycleAppraisals.Count(a => a.Status == AppraisalStatus.PendingLineManager),
            AwaitingMd = cycleAppraisals.Count(a => a.Status == AppraisalStatus.PendingMd),
            AwaitingHr = cycleAppraisals.Count(a => a.Status == AppraisalStatus.PendingHr),
            Completed = cycleAppraisals.Count(a => a.Status == AppraisalStatus.Completed),
            AverageFinalScore = cycleAppraisals.Any(a => a.FinalScore is not null)
                ? Round(cycleAppraisals.Where(a => a.FinalScore is not null).Average(a => a.FinalScore!.Value))
                : null,

            Feedback360Outstanding = outstanding360,
            ActivePips = activePips.Count,
            PipReviewsDue = activePips.Count(p => p.ReviewDate.Date <= today),
        };
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scorecards and targets (P14)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<KpiScorecardDto>> ListScorecardsAsync(int? year, bool includeInactive)
    {
        var q = scorecards.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(s => s.Year == year.Value);
        if (!includeInactive) q = q.Where(s => s.IsActive);

        var list = await q.OrderByDescending(s => s.Year).ThenBy(s => s.Name).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(s => s.Id).ToList();
        var items = await scorecardItems.Query().AsNoTracking().Where(i => ids.Contains(i.KpiScorecardId)).ToListAsync();
        var counts = await targets.Query().AsNoTracking().Where(t => ids.Contains(t.KpiScorecardId))
            .GroupBy(t => t.KpiScorecardId)
            .Select(g => new { Id = g.Key, Count = g.Select(x => x.EmployeeId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);

        return list.Select(s => ToDto(s, items.Where(i => i.KpiScorecardId == s.Id).ToList(),
            counts.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<KpiScorecardDto?> GetScorecardAsync(string id)
    {
        var card = await scorecards.Query().AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (card is null) return null;
        var items = await scorecardItems.Query().AsNoTracking().Where(i => i.KpiScorecardId == id).ToListAsync();
        var count = await targets.Query().Where(t => t.KpiScorecardId == id).Select(t => t.EmployeeId).Distinct().CountAsync();
        return ToDto(card, items, count);
    }

    public async Task<AppraisalActionResult> SaveScorecardAsync(string? id, SaveKpiScorecardDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A scorecard needs a name.");
        var year = dto.Year ?? DateTime.UtcNow.Year;
        if (dto.PipThreshold is < 0 or > 100) return Err("The PIP threshold must be between 0 and 100.");

        var clean = dto.Items.Where(i => !string.IsNullOrWhiteSpace(i.ItemName)).ToList();
        if (clean.Count == 0) return Err("A scorecard needs at least one item.");

        // P14 design note — the weights must total exactly 100. A scorecard summing to 93 does not fail
        // visibly; it produces a plausible score that is quietly 7% too low for everyone measured on it.
        var total = Round(clean.Sum(i => i.WeightPercent));
        if (total != 100m)
            return Err($"The item weights total {total:0.##}%, not 100%. Adjust them before saving.");
        if (clean.Any(i => i.WeightPercent <= 0))
            return Err("Every item needs a weight above zero — an item worth nothing is not a measure.");

        var parsed = new List<(SaveKpiScorecardItemDto Dto, KpiMeasurementType Type, KpiTargetSource Source)>();
        foreach (var i in clean)
        {
            if (!Enum.TryParse<KpiMeasurementType>(i.MeasurementType, true, out var type))
                return Err($"'{i.ItemName}': the measurement must be Quantitative or Qualitative.");
            if (!Enum.TryParse<KpiTargetSource>(i.TargetSource, true, out var source))
                return Err($"'{i.ItemName}': the source must be Manual, Attendance, Feedback360 or Revenue.");
            parsed.Add((i, type, source));
        }

        Position? position = null;
        if (!string.IsNullOrWhiteSpace(dto.PositionId))
        {
            position = await positions.GetByIdAsync(dto.PositionId!);
            if (position is null || position.IsDeleted) return Err("Position not found.");
        }

        KpiScorecard card;
        if (string.IsNullOrWhiteSpace(id))
        {
            var clash = await scorecards.Query().AsNoTracking()
                .FirstOrDefaultAsync(s => s.PositionId == dto.PositionId && s.Year == year);
            if (clash is not null)
                return Err($"A {year} scorecard already exists for {position?.Title ?? "staff with no position"} ('{clash.Name}'). Update that one instead — its id is {clash.Id}.");
            card = new KpiScorecard { Year = year, CreatedBy = userId, UpdatedBy = userId };
        }
        else
        {
            var found = await scorecards.GetByIdAsync(id!);
            if (found is null || found.IsDeleted) return Err("Scorecard not found.");
            card = found;
        }

        card.Name = dto.Name.Trim();
        card.Description = dto.Description;
        card.PositionId = position?.Id;
        card.PositionTitle = position?.Title;
        card.PipThreshold = dto.PipThreshold ?? card.PipThreshold;
        if (dto.IsActive.HasValue) card.IsActive = dto.IsActive.Value;
        Touch(card, userId);

        var saved = string.IsNullOrWhiteSpace(id) ? await scorecards.CreateAsync(card) : await scorecards.UpdateAsync(card);

        // Items are replaced wholesale. Any target already raised against a removed item keeps its row — the
        // number somebody was measured on last cycle is not ours to delete.
        var existing = await scorecardItems.Query().Where(i => i.KpiScorecardId == saved.Id).ToListAsync();
        var keptIds = clean.Where(i => !string.IsNullOrWhiteSpace(i.Id)).Select(i => i.Id!).ToHashSet();
        foreach (var stale in existing.Where(i => !keptIds.Contains(i.Id))) await scorecardItems.DeleteAsync(stale);

        var order = 0;
        foreach (var (item, type, source) in parsed)
        {
            // Whether this is an edit is decided by finding the EXISTING row, not by whether the object has an
            // id — BaseEntity hands every new entity a Guid, so an id is never evidence that a row exists.
            var entity = existing.FirstOrDefault(e => e.Id == item.Id);
            var isNew = entity is null;
            entity ??= new KpiScorecardItem { KpiScorecardId = saved.Id, CreatedBy = userId, UpdatedBy = userId };
            entity.ItemName = item.ItemName.Trim();
            entity.Description = item.Description;
            entity.WeightPercent = item.WeightPercent;
            entity.MeasurementType = type;
            entity.TargetSource = source;
            entity.Unit = item.Unit;
            entity.DisplayOrder = item.DisplayOrder == 0 ? ++order : item.DisplayOrder;
            entity.IsActive = true;
            Touch(entity, userId);

            if (isNew) await scorecardItems.CreateAsync(entity);
            else await scorecardItems.UpdateAsync(entity);
        }

        var result = new AppraisalActionResult(string.IsNullOrWhiteSpace(id) ? "Created" : "Updated",
            $"{saved.Name} saved with {clean.Count} item(s) totalling 100%.", saved.Id);
        if (parsed.Any(p => p.Source == KpiTargetSource.Revenue))
            result.Warnings.Add("Revenue items read the sales target and collected revenue from CRM, which owns both. If CRM is unreachable when a cycle opens, the item is left for manual scoring rather than scored as zero.");

        await LogAsync("KpiScorecard", saved.Id, HrAuditAction.KpiScorecardConfigured,
            $"Scorecard '{saved.Name}' ({saved.Year}) saved for {saved.PositionTitle ?? "all staff"} — {clean.Count} item(s), PIP threshold {saved.PipThreshold:0.##}.", userId);
        return result;
    }

    public async Task<AppraisalActionResult> AssignTargetsAsync(string scorecardId, string userId)
    {
        var card = await scorecards.GetByIdAsync(scorecardId);
        if (card is null || card.IsDeleted) return Err("Scorecard not found.");

        var items = await scorecardItems.Query().Where(i => i.KpiScorecardId == card.Id && i.IsActive)
            .OrderBy(i => i.DisplayOrder).ToListAsync();
        if (items.Count == 0) return Err("That scorecard has no active items.");
        if (Round(items.Sum(i => i.WeightPercent)) != 100m)
            return Err("The scorecard's weights do not total 100% — fix them before assigning targets.");

        var staff = await employees.Query()
            .Where(e => Active.Contains(e.Status)
                     && (card.PositionId == null || e.PositionId == card.PositionId))
            .OrderBy(e => e.EmployeeNumber).ToListAsync();
        if (staff.Count == 0)
            return Err(card.PositionId is null
                ? "No active employees to assign targets to."
                : $"No active employees hold {card.PositionTitle}.");

        var existing = await targets.Query().Where(t => t.KpiScorecardId == card.Id && t.Year == card.Year).ToListAsync();
        var created = 0;

        foreach (var e in staff)
            foreach (var item in items)
            {
                // The row's existence is the idempotence guard — re-running never overwrites a figure
                // somebody has already set.
                if (existing.Any(t => t.EmployeeId == e.Id && t.KpiScorecardItemId == item.Id)) continue;
                await targets.CreateAsync(new KpiTarget
                {
                    EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.FullName,
                    KpiScorecardId = card.Id, KpiScorecardItemId = item.Id, ItemName = item.ItemName,
                    Year = card.Year,
                    CreatedBy = userId, UpdatedBy = userId,
                });
                created++;
            }

        var result = new AppraisalActionResult(created == 0 ? "NoChange" : "Created",
            created == 0
                ? $"Every employee on {card.Name} already has their {card.Year} targets."
                : $"{created} target row(s) raised across {staff.Count} employee(s).", card.Id);
        if (created > 0)
            result.Warnings.Add("Targets are raised at zero — set each employee's figure before the appraisal cycle opens.");

        await LogAsync("KpiScorecard", card.Id, HrAuditAction.KpiTargetsAssigned,
            $"{created} target(s) raised on '{card.Name}' for {staff.Count} employee(s) in {card.Year}.", userId);
        return result;
    }

    public async Task<List<KpiTargetDto>> ListTargetsAsync(int? year, string? employeeId)
    {
        var q = targets.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(t => t.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(t => t.EmployeeId == employeeId);

        var list = await q.OrderBy(t => t.EmployeeNumber).ThenBy(t => t.ItemName).ToListAsync();
        if (list.Count == 0) return [];

        var itemIds = list.Select(t => t.KpiScorecardItemId).Distinct().ToList();
        var units = await scorecardItems.Query().AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Unit);

        return list.Select(t => new KpiTargetDto
        {
            Id = t.Id, EmployeeId = t.EmployeeId, EmployeeNumber = t.EmployeeNumber, EmployeeName = t.EmployeeName,
            KpiScorecardItemId = t.KpiScorecardItemId, ItemName = t.ItemName, Year = t.Year,
            TargetValue = t.TargetValue, ActualValue = t.ActualValue,
            ActualSource = t.ActualSource.ToString(), ActualUpdatedAt = t.ActualUpdatedAt,
            Unit = units.GetValueOrDefault(t.KpiScorecardItemId), Notes = t.Notes,
        }).ToList();
    }

    public async Task<AppraisalActionResult> SetTargetAsync(SetTargetDto dto, string userId)
    {
        var target = await targets.GetByIdAsync(dto.KpiTargetId ?? string.Empty);
        if (target is null || target.IsDeleted) return Err("Target not found.");
        if (dto.TargetValue is < 0 || dto.ActualValue is < 0) return Err("A target or actual cannot be negative.");

        if (dto.TargetValue.HasValue) target.TargetValue = dto.TargetValue.Value;
        if (dto.ActualValue.HasValue)
        {
            target.ActualValue = dto.ActualValue.Value;
            target.ActualSource = KpiTargetSource.Manual;
            target.ActualUpdatedAt = DateTime.UtcNow;
        }
        target.Notes = dto.Notes ?? target.Notes;
        Touch(target, userId);
        await targets.UpdateAsync(target);

        return new AppraisalActionResult("Updated",
            $"{target.EmployeeName} — {target.ItemName}: target {target.TargetValue:N2}, actual {target.ActualValue:N2}.", target.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Cycles and the appraisal workflow (P15)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<AppraisalCycleDto>> ListCyclesAsync(int? year)
    {
        var q = cycles.Query().AsNoTracking();
        if (year.HasValue) q = q.Where(c => c.Year == year.Value);
        var list = await q.OrderByDescending(c => c.Year).ThenBy(c => c.CycleType).ToListAsync();
        if (list.Count == 0) return [];

        var ids = list.Select(c => c.Id).ToList();
        var done = await appraisals.Query().AsNoTracking()
            .Where(a => ids.Contains(a.AppraisalCycleId) && a.Status == AppraisalStatus.Completed)
            .GroupBy(a => a.AppraisalCycleId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);

        return list.Select(c => new AppraisalCycleDto
        {
            Id = c.Id, Name = c.Name, CycleType = c.CycleType.ToString(), Year = c.Year,
            StartDate = c.StartDate, EndDate = c.EndDate, Status = c.Status.ToString(),
            OpenedAt = c.OpenedAt, ClosedAt = c.ClosedAt,
            AppraisalCount = c.AppraisalCount, Completed = done.GetValueOrDefault(c.Id), Notes = c.Notes,
        }).ToList();
    }

    public async Task<AppraisalActionResult> OpenCycleAsync(OpenCycleDto dto, string? tenantSchema, string userId)
    {
        if (!Enum.TryParse<AppraisalCycleType>(dto.CycleType, true, out var cycleType))
            return Err("The cycle type must be MidYear or EndOfYear.");
        var year = dto.Year ?? DateTime.UtcNow.Year;

        if (await cycles.Query().AnyAsync(c => c.Year == year && c.CycleType == cycleType))
            return Err($"The {cycleType} {year} cycle already exists.");

        var start = dto.StartDate is null
            ? new DateTime(year, cycleType == AppraisalCycleType.MidYear ? 6 : 12, 1, 0, 0, 0, DateTimeKind.Utc)
            : DateTime.SpecifyKind(dto.StartDate.Value.Date, DateTimeKind.Utc);
        var end = dto.EndDate is null ? start.AddMonths(1).AddDays(-1)
            : DateTime.SpecifyKind(dto.EndDate.Value.Date, DateTimeKind.Utc);
        if (end < start) return Err("The cycle cannot end before it starts.");

        var staff = await employees.Query().AsNoTracking().Where(e => Active.Contains(e.Status))
            .OrderBy(e => e.EmployeeNumber).ToListAsync();
        if (staff.Count == 0) return Err("No active employees to appraise.");

        var cards = await scorecards.Query().AsNoTracking().Where(s => s.Year == year && s.IsActive).ToListAsync();
        if (cards.Count == 0) return Err($"No active {year} scorecards — configure one before opening a cycle.");

        var cardIds = cards.Select(c => c.Id).ToList();
        var allItems = await scorecardItems.Query().AsNoTracking()
            .Where(i => cardIds.Contains(i.KpiScorecardId) && i.IsActive).OrderBy(i => i.DisplayOrder).ToListAsync();
        var allTargets = await targets.Query().AsNoTracking().Where(t => t.Year == year).ToListAsync();
        var positionTitles = await positions.Query().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Title);
        var attendance = await attendanceScorecards.Query().AsNoTracking().Where(a => a.Year == year).ToListAsync();
        // H11-DEC-3 — a Revenue-sourced item reads the SAME source commission uses: CRM owns the target and
        // the attainment. Read once for the whole cycle rather than per employee.
        var salesAttainment = await crm.ListSalesAttainmentAsync(year);

        var cycle = await cycles.CreateAsync(new AppraisalCycle
        {
            Name = string.IsNullOrWhiteSpace(dto.Name) ? $"{Spaced(cycleType)} {year}" : dto.Name!.Trim(),
            CycleType = cycleType, Year = year, StartDate = start, EndDate = end,
            Status = AppraisalCycleStatus.Open, OpenedAt = DateTime.UtcNow, OpenedBy = userId,
            Notes = dto.Notes, CreatedBy = userId, UpdatedBy = userId,
        });

        var skipped = new List<string>();
        var raised = 0;

        foreach (var e in staff)
        {
            // The role's scorecard, falling back to a company-wide one where a role has none.
            var card = cards.FirstOrDefault(c => c.PositionId != null && c.PositionId == e.PositionId)
                    ?? cards.FirstOrDefault(c => c.PositionId is null);
            if (card is null) { skipped.Add($"{e.EmployeeNumber} {e.FullName} — no scorecard for their role"); continue; }

            var items = allItems.Where(i => i.KpiScorecardId == card.Id).ToList();
            if (items.Count == 0) { skipped.Add($"{e.EmployeeNumber} {e.FullName} — scorecard has no items"); continue; }

            var appraisal = await appraisals.CreateAsync(new Appraisal
            {
                AppraisalCycleId = cycle.Id, CycleName = cycle.Name, Year = year,
                EmployeeId = e.Id, EmployeeNumber = e.EmployeeNumber, EmployeeName = e.FullName,
                DepartmentId = e.DepartmentId,
                PositionTitle = e.PositionId is null ? null : positionTitles.GetValueOrDefault(e.PositionId),
                KpiScorecardId = card.Id, ScorecardName = card.Name,
                // Copied, so a later change to the scorecard cannot move a verdict already reached.
                PipThreshold = card.PipThreshold,
                CreatedBy = userId, UpdatedBy = userId,
            });

            var order = 0;
            foreach (var item in items)
            {
                var target = allTargets.FirstOrDefault(t => t.EmployeeId == e.Id && t.KpiScorecardItemId == item.Id);
                var score = new AppraisalItemScore
                {
                    AppraisalId = appraisal.Id,
                    KpiTargetId = target?.Id,
                    KpiScorecardItemId = item.Id,
                    ItemName = item.ItemName,
                    WeightPercent = item.WeightPercent,
                    MeasurementType = item.MeasurementType,
                    Source = item.TargetSource,
                    Unit = item.Unit,
                    TargetValue = target?.TargetValue ?? 0m,
                    ActualValue = target?.ActualValue ?? 0m,
                    DisplayOrder = ++order,
                    CreatedBy = userId, UpdatedBy = userId,
                };

                // ATT-008 — H4's annual attendance scorecard is already a number out of 100, so an attendance
                // item scores itself. Nobody should be typing in a figure the system already computed.
                if (item.TargetSource == KpiTargetSource.Attendance)
                {
                    var att = attendance.FirstOrDefault(a => a.EmployeeId == e.Id);
                    if (att is not null)
                    {
                        score.ActualValue = att.Score;
                        score.SelfScore = att.Score;
                        score.ManagerScore = att.Score;
                        score.FinalScore = att.Score;
                        score.Comments = $"From the {year} attendance scorecard.";
                    }
                    else score.Comments = "No attendance scorecard for this year yet.";
                }

                // Revenue: target and achievement both come from CRM. An unreadable source is said plainly
                // rather than scored as zero — a zero revenue score is a career event, not a shrug.
                if (item.TargetSource == KpiTargetSource.Revenue)
                {
                    if (salesAttainment is null)
                        score.Comments = "CRM could not be read — score this item manually.";
                    else if (string.IsNullOrWhiteSpace(e.UserId))
                        score.Comments = "No login account, so CRM has no record to match — score manually.";
                    else
                    {
                        var sales = salesAttainment.FirstOrDefault(a => a.EmployeeUserId == e.UserId);
                        if (sales is null) score.Comments = $"CRM holds no {year} sales target — score manually.";
                        else
                        {
                            score.TargetValue = sales.AnnualTarget;
                            score.ActualValue = sales.RevenueAchieved;
                            var attained = sales.AnnualTarget > 0
                                ? Math.Min(Round(sales.RevenueAchieved / sales.AnnualTarget * 100m), MaxQuantitativeScore)
                                : 0m;
                            score.SelfScore = attained;
                            score.ManagerScore = attained;
                            score.FinalScore = attained;
                            score.Comments = $"From CRM: {sales.RevenueAchieved:N0} against a {sales.AnnualTarget:N0} target.";
                        }
                    }
                }

                await itemScores.CreateAsync(score);
            }
            raised++;
        }

        cycle.AppraisalCount = raised;
        Touch(cycle, userId);
        await cycles.UpdateAsync(cycle);

        var result = new AppraisalActionResult("Opened",
            $"{cycle.Name} opened — {raised} appraisal(s) raised.", cycle.Id);
        if (skipped.Count > 0)
            result.Warnings.Add($"{skipped.Count} employee(s) were skipped: {string.Join("; ", skipped.Take(5))}{(skipped.Count > 5 ? " …" : "")}");
        var missingTargets = allTargets.Count(t => t.TargetValue == 0m);
        if (missingTargets > 0)
            result.Warnings.Add($"{missingTargets} target(s) are still zero — quantitative items cannot be scored against a target of nothing.");

        await NotifyAsync(tenantSchema, "Info", $"Appraisal cycle opened — {cycle.Name}",
            $"{raised} appraisal(s) raised. Staff should complete their self-assessment by {end:dd MMM yyyy}.", "hr.read.own");
        await LogAsync("AppraisalCycle", cycle.Id, HrAuditAction.AppraisalCycleOpened,
            $"{cycle.Name} opened: {raised} appraisal(s), {skipped.Count} skipped.", userId);
        return result;
    }

    public async Task<AppraisalActionResult> CloseCycleAsync(string id, string userId)
    {
        var cycle = await cycles.GetByIdAsync(id);
        if (cycle is null || cycle.IsDeleted) return Err("Cycle not found.");
        if (cycle.Status == AppraisalCycleStatus.Closed)
            return new AppraisalActionResult("NoChange", $"{cycle.Name} is already closed.", cycle.Id);

        var open = await appraisals.Query()
            .CountAsync(a => a.AppraisalCycleId == cycle.Id && a.Status != AppraisalStatus.Completed
                                                            && a.Status != AppraisalStatus.Cancelled);
        cycle.Status = AppraisalCycleStatus.Closed;
        cycle.ClosedAt = DateTime.UtcNow;
        Touch(cycle, userId);
        await cycles.UpdateAsync(cycle);

        var result = new AppraisalActionResult("Closed", $"{cycle.Name} closed.", cycle.Id);
        if (open > 0)
            result.Warnings.Add($"{open} appraisal(s) were still in progress and remain open on the record — closing the cycle does not complete them.");
        return result;
    }

    public async Task<List<AppraisalDto>> ListAppraisalsAsync(string? cycleId, string? status, string? employeeId)
    {
        var q = appraisals.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(cycleId)) q = q.Where(a => a.AppraisalCycleId == cycleId);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(a => a.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppraisalStatus>(status, true, out var st))
            q = q.Where(a => a.Status == st);

        var list = await q.OrderByDescending(a => a.Year).ThenBy(a => a.EmployeeNumber).ToListAsync();
        return list.Select(a => ToDto(a, [])).ToList();
    }

    public async Task<AppraisalDto?> GetAppraisalAsync(string id)
    {
        var appraisal = await appraisals.Query().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (appraisal is null) return null;
        var scores = await itemScores.Query().AsNoTracking().Where(s => s.AppraisalId == id).ToListAsync();
        return ToDto(appraisal, scores);
    }

    public async Task<AppraisalActionResult> SubmitSelfAsync(string id, SubmitAppraisalStepDto dto, string userId)
    {
        var appraisal = await appraisals.GetByIdAsync(id);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (appraisal.Status != AppraisalStatus.PendingSelf)
            return Err($"That appraisal is {Awaiting(appraisal.Status)} — the self-assessment step has passed.");

        var scores = await itemScores.Query().Where(s => s.AppraisalId == id).ToListAsync();
        var applied = ApplyScores(scores, dto.ItemScores, isManager: false, userId);
        if (applied.Error is not null) return Err(applied.Error);
        foreach (var s in scores) await itemScores.UpdateAsync(s);

        appraisal.SelfScore = Weighted(scores, s => s.SelfScore);
        appraisal.SelfComments = dto.Comments;
        appraisal.TrainingNeeds = dto.TrainingNeeds;
        appraisal.SelfAssessmentSubmittedAt = DateTime.UtcNow;
        appraisal.SelfAssessmentBy = userId;
        appraisal.Status = AppraisalStatus.PendingLineManager;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        await LogAsync("Appraisal", appraisal.Id, HrAuditAction.AppraisalSelfSubmitted,
            $"{appraisal.EmployeeNumber} submitted their self-assessment for {appraisal.CycleName}: {appraisal.SelfScore:0.##}.", userId);
        return new AppraisalActionResult("Submitted",
            $"Self-assessment submitted at {appraisal.SelfScore:0.##} — now with the line manager.", appraisal.Id);
    }

    public async Task<AppraisalActionResult> ReviewAsync(string id, SubmitAppraisalStepDto dto, string userId, string? userName)
    {
        var appraisal = await appraisals.GetByIdAsync(id);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (appraisal.Status != AppraisalStatus.PendingLineManager)
            return Err($"That appraisal is {Awaiting(appraisal.Status)} — it is not ready for a line-manager review.");

        // HR-018: the employee has to have had their say first. The status guard already enforces the order,
        // but stating it explicitly means a future change to the status machine cannot quietly lose the rule.
        if (appraisal.SelfAssessmentSubmittedAt is null)
            return Err("The employee has not completed their self-assessment — a review cannot come first.");
        if (!string.IsNullOrWhiteSpace(appraisal.SelfAssessmentBy) && appraisal.SelfAssessmentBy == userId)
            return Err("You cannot review your own self-assessment — it needs the line manager.");

        var scores = await itemScores.Query().Where(s => s.AppraisalId == id).ToListAsync();
        var applied = ApplyScores(scores, dto.ItemScores, isManager: true, userId);
        if (applied.Error is not null) return Err(applied.Error);
        foreach (var s in scores) await itemScores.UpdateAsync(s);

        appraisal.LineManagerScore = Weighted(scores, s => s.FinalScore);
        appraisal.FinalScore = appraisal.LineManagerScore;
        appraisal.LineManagerComments = dto.Comments;
        if (!string.IsNullOrWhiteSpace(dto.TrainingNeeds)) appraisal.TrainingNeeds = dto.TrainingNeeds;
        appraisal.LineManagerReviewedAt = DateTime.UtcNow;
        appraisal.LineManagerBy = userId;
        appraisal.Status = AppraisalStatus.PendingMd;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        var result = new AppraisalActionResult("Reviewed",
            $"Reviewed at {appraisal.FinalScore:0.##} — now with the MD.", appraisal.Id);
        if (appraisal.FinalScore < appraisal.PipThreshold)
            result.Warnings.Add($"This score is below the {appraisal.PipThreshold:0.##} threshold — MD sign-off will raise an improvement plan.");

        await LogAsync("Appraisal", appraisal.Id, HrAuditAction.AppraisalReviewed,
            $"{appraisal.EmployeeNumber} reviewed by {userName ?? userId}: self {appraisal.SelfScore:0.##}, manager {appraisal.LineManagerScore:0.##}.", userId, userName);
        return result;
    }

    public async Task<AppraisalActionResult> SignOffAsync(string id, SubmitAppraisalStepDto dto, string? tenantSchema, string userId, string? userName)
    {
        var appraisal = await appraisals.GetByIdAsync(id);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (appraisal.Status != AppraisalStatus.PendingMd)
            return Err($"That appraisal is {Awaiting(appraisal.Status)} — it is not ready for sign-off.");
        if (appraisal.LineManagerReviewedAt is null)
            return Err("The line manager has not reviewed this yet.");
        if (!string.IsNullOrWhiteSpace(appraisal.LineManagerBy) && appraisal.LineManagerBy == userId)
            return Err("The line manager who reviewed an appraisal cannot also sign it off — it needs the MD.");

        appraisal.MdComments = dto.Comments;
        appraisal.MdSignedOffAt = DateTime.UtcNow;
        appraisal.MdSignedOffBy = userId;
        appraisal.Status = AppraisalStatus.PendingHr;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        var result = new AppraisalActionResult("SignedOff",
            $"{appraisal.EmployeeName} signed off at {appraisal.FinalScore:0.##} — now with HR to record.", appraisal.Id);

        // P17 step 17.1 — the PIP check happens HERE, on sign-off, not at HR recording. By the time HR files
        // it the decision has already been made; the plan has to start from the decision.
        if (appraisal.FinalScore is not null && appraisal.FinalScore < appraisal.PipThreshold && !appraisal.PipTriggered)
        {
            var pip = await RaisePipAsync(appraisal, tenantSchema, userId, userName);
            result.Warnings.Add($"Score {appraisal.FinalScore:0.##} is below the {appraisal.PipThreshold:0.##} threshold — an improvement plan has been raised automatically (HR-019). Review date {pip.ReviewDate:dd MMM yyyy}.");
        }

        await LogAsync("Appraisal", appraisal.Id, HrAuditAction.AppraisalSignedOff,
            $"{appraisal.EmployeeNumber} signed off by {userName ?? userId} at {appraisal.FinalScore:0.##} (threshold {appraisal.PipThreshold:0.##}).", userId, userName);
        return result;
    }

    public async Task<AppraisalActionResult> RecordAsync(string id, SubmitAppraisalStepDto dto, string userId, string? userName)
    {
        var appraisal = await appraisals.GetByIdAsync(id);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (appraisal.Status != AppraisalStatus.PendingHr)
            return Err($"That appraisal is {Awaiting(appraisal.Status)} — it is not ready to be recorded.");
        if (appraisal.MdSignedOffAt is null) return Err("The MD has not signed this off yet.");

        appraisal.HrComments = dto.Comments;
        if (!string.IsNullOrWhiteSpace(dto.TrainingNeeds)) appraisal.TrainingNeeds = dto.TrainingNeeds;
        appraisal.HrRecordedAt = DateTime.UtcNow;
        appraisal.HrRecordedBy = userId;
        appraisal.Status = AppraisalStatus.Completed;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        var result = new AppraisalActionResult("Recorded",
            $"{appraisal.EmployeeName}'s appraisal is complete at {appraisal.FinalScore:0.##}.", appraisal.Id);
        if (!string.IsNullOrWhiteSpace(appraisal.TrainingNeeds))
            result.Warnings.Add($"Training needs recorded: {appraisal.TrainingNeeds} — add these to the employee's learning plan (HR-020).");

        await LogAsync("Appraisal", appraisal.Id, HrAuditAction.AppraisalRecorded,
            $"{appraisal.EmployeeNumber} recorded by {userName ?? userId} at {appraisal.FinalScore:0.##}.", userId, userName);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // 360 feedback (P16)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<Feedback360SummaryDto?> GetFeedbackAsync(string appraisalId)
    {
        var appraisal = await appraisals.Query().AsNoTracking().FirstOrDefaultAsync(a => a.Id == appraisalId);
        if (appraisal is null) return null;

        var rows = await feedback.Query().AsNoTracking().Where(f => f.AppraisalId == appraisalId).ToListAsync();
        var submitted = rows.Where(f => f.SubmittedAt is not null && f.OverallScore is not null).ToList();

        decimal? Average(Feedback360ReviewerType type)
        {
            var of = submitted.Where(f => f.ReviewerType == type).ToList();
            return of.Count == 0 ? null : Round(of.Average(f => f.OverallScore!.Value));
        }

        var peer = Average(Feedback360ReviewerType.Peer);
        var sub = Average(Feedback360ReviewerType.Subordinate);
        var lm = Average(Feedback360ReviewerType.LineManager);
        // Each group counts once, so a large peer set cannot drown out a single subordinate.
        var parts = new[] { peer, sub, lm }.Where(v => v is not null).Select(v => v!.Value).ToList();

        return new Feedback360SummaryDto
        {
            AppraisalId = appraisalId,
            EmployeeName = appraisal.EmployeeName,
            Requested = rows.Count,
            Submitted = submitted.Count,
            PeerAverage = peer, SubordinateAverage = sub, LineManagerScore = lm,
            Aggregate = parts.Count == 0 ? null : Round(parts.Average()),
            // Names only — never paired with a score, because a non-response carries none.
            AwaitingResponse = rows.Where(f => f.SubmittedAt is null).Select(f => f.ReviewerName ?? "unnamed").ToList(),
            Comments = submitted.Where(f => !string.IsNullOrWhiteSpace(f.Comments)).Select(f => f.Comments!).ToList(),
        };
    }

    public async Task<AppraisalActionResult> RequestFeedbackAsync(string appraisalId, Feedback360RequestDto dto, string? tenantSchema, string userId)
    {
        var appraisal = await appraisals.GetByIdAsync(appraisalId);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (appraisal.Status is AppraisalStatus.Completed or AppraisalStatus.Cancelled)
            return Err("That appraisal is closed — feedback can no longer be gathered for it.");

        var wanted = dto.Reviewers.Where(r => !string.IsNullOrWhiteSpace(r.ReviewerEmployeeId)).ToList();
        if (wanted.Count == 0) return Err("Pick at least one reviewer.");
        if (wanted.Any(r => r.ReviewerEmployeeId == appraisal.EmployeeId))
            return Err("Someone cannot be a reviewer on their own 360.");

        var ids = wanted.Select(r => r.ReviewerEmployeeId).Distinct().ToList();
        var people = await employees.Query().AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();
        if (people.Count != ids.Count) return Err("One or more reviewers are not employees on file.");

        var existing = await feedback.Query().Where(f => f.AppraisalId == appraisalId).ToListAsync();
        var added = 0;
        foreach (var r in wanted)
        {
            if (!Enum.TryParse<Feedback360ReviewerType>(r.ReviewerType, true, out var type))
                return Err($"The reviewer type must be Peer, Subordinate or LineManager.");
            if (existing.Any(f => f.ReviewerEmployeeId == r.ReviewerEmployeeId)) continue;

            var person = people.First(p => p.Id == r.ReviewerEmployeeId);
            await feedback.CreateAsync(new Feedback360
            {
                AppraisalId = appraisalId, EmployeeId = appraisal.EmployeeId,
                ReviewerEmployeeId = person.Id, ReviewerName = person.FullName, ReviewerType = type,
                RequestedAt = DateTime.UtcNow, CreatedBy = userId, UpdatedBy = userId,
            });
            added++;
        }

        if (added == 0) return new AppraisalActionResult("NoChange", "Every reviewer named has already been asked.", appraisalId);

        await NotifyAsync(tenantSchema, "Info", $"360 feedback requested — {appraisal.EmployeeName}",
            $"{added} reviewer(s) have been asked for feedback on {appraisal.EmployeeName}'s {appraisal.CycleName} appraisal.", "hr.read.own");
        await LogAsync("Appraisal", appraisalId, HrAuditAction.Feedback360Requested,
            $"{added} 360 reviewer(s) requested for {appraisal.EmployeeNumber}.", userId);
        return new AppraisalActionResult("Requested", $"{added} reviewer(s) asked for feedback.", appraisalId);
    }

    public async Task<AppraisalActionResult> SubmitFeedbackAsync(string appraisalId, SubmitFeedback360Dto dto, string userId)
    {
        var appraisal = await appraisals.GetByIdAsync(appraisalId);
        if (appraisal is null || appraisal.IsDeleted) return Err("Appraisal not found.");
        if (dto.OverallScore is < 0 or > 100) return Err("A 360 score must be between 0 and 100.");

        // The reviewer is identified by their own employee record, so nobody can submit on another's behalf.
        var me = await employees.Query().AsNoTracking().FirstOrDefaultAsync(e => e.UserId == userId);
        var row = me is null
            ? null
            : await feedback.Query().FirstOrDefaultAsync(f => f.AppraisalId == appraisalId && f.ReviewerEmployeeId == me.Id);
        if (row is null) return Err("You have not been asked for feedback on this appraisal.");
        if (row.SubmittedAt is not null) return Err("You have already given feedback on this appraisal.");

        row.OverallScore = dto.OverallScore;
        row.Comments = dto.Comments;
        row.ScoresJson = dto.ScoresJson;
        row.SubmittedAt = DateTime.UtcNow;
        Touch(row, userId);
        await feedback.UpdateAsync(row);

        // Roll the aggregate onto the appraisal and into any Feedback360-sourced item (P16 step 16.6).
        var summary = await GetFeedbackAsync(appraisalId);
        appraisal.Feedback360Score = summary?.Aggregate;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        if (summary?.Aggregate is not null)
        {
            var linked = await itemScores.Query()
                .Where(s => s.AppraisalId == appraisalId && s.Source == KpiTargetSource.Feedback360).ToListAsync();
            foreach (var s in linked)
            {
                s.ActualValue = summary.Aggregate.Value;
                s.SelfScore ??= summary.Aggregate.Value;
                s.ManagerScore = summary.Aggregate.Value;
                s.FinalScore = summary.Aggregate.Value;
                s.Comments = $"360 aggregate from {summary.Submitted} reviewer(s).";
                Touch(s, userId);
                await itemScores.UpdateAsync(s);
            }
        }

        await LogAsync("Appraisal", appraisalId, HrAuditAction.Feedback360Submitted,
            $"360 feedback submitted for {appraisal.EmployeeNumber} ({summary?.Submitted} of {summary?.Requested} in).", userId);
        return new AppraisalActionResult("Submitted",
            $"Thank you — {summary?.Submitted} of {summary?.Requested} reviewer(s) have now responded. Individual scores stay anonymous.", appraisalId);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Improvement plans (P17)
    // ══════════════════════════════════════════════════════════════════════════════
    public async Task<List<PipDto>> ListPipsAsync(string? status, string? employeeId)
    {
        var q = pips.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(p => p.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PipStatus>(status, true, out var st))
            q = q.Where(p => p.Status == st);

        var list = await q.OrderByDescending(p => p.StartDate).ToListAsync();
        var today = DateTime.UtcNow.Date;
        return list.Select(p => ToDto(p, today)).ToList();
    }

    public async Task<AppraisalActionResult> UpdatePipAsync(string id, SavePipDto dto, string userId)
    {
        var pip = await pips.GetByIdAsync(id);
        if (pip is null || pip.IsDeleted) return Err("Improvement plan not found.");
        if (pip.Status is PipStatus.Completed or PipStatus.EscalatedToDisciplinary)
            return Err($"That plan is {Spaced(pip.Status)} and can no longer be edited.");

        if (dto.ReviewDate is not null)
        {
            var review = DateTime.SpecifyKind(dto.ReviewDate.Value.Date, DateTimeKind.Utc);
            if (review <= pip.StartDate.Date) return Err("The review date must be after the plan starts.");
            pip.ReviewDate = review;
        }
        if (dto.SecondReviewDate is not null)
        {
            var second = DateTime.SpecifyKind(dto.SecondReviewDate.Value.Date, DateTimeKind.Utc);
            if (second <= pip.ReviewDate.Date) return Err("The second review must come after the first.");
            pip.SecondReviewDate = second;
        }
        pip.Objectives = dto.Objectives ?? pip.Objectives;
        pip.SupportProvided = dto.SupportProvided ?? pip.SupportProvided;
        Touch(pip, userId);
        await pips.UpdateAsync(pip);

        var result = new AppraisalActionResult("Updated", $"{pip.EmployeeName}'s improvement plan updated.", pip.Id);
        if (string.IsNullOrWhiteSpace(pip.Objectives))
            result.Warnings.Add("This plan still has no objectives — a PIP without them cannot be judged either way.");
        return result;
    }

    public async Task<AppraisalActionResult> ClosePipAsync(string id, ClosePipDto dto, string? tenantSchema, string userId, string? userName)
    {
        var pip = await pips.GetByIdAsync(id);
        if (pip is null || pip.IsDeleted) return Err("Improvement plan not found.");
        if (pip.Status is PipStatus.Completed or PipStatus.EscalatedToDisciplinary)
            return Err($"That plan is already {Spaced(pip.Status)}.");
        if (!Enum.TryParse<PipStatus>(dto.Outcome, true, out var outcome)
            || outcome is PipStatus.Active)
            return Err("The outcome must be Completed, Extended or EscalatedToDisciplinary.");
        if (string.IsNullOrWhiteSpace(dto.Notes)) return Err("Closing an improvement plan needs a note on the outcome.");

        if (outcome == PipStatus.Extended)
        {
            if (dto.NewReviewDate is null) return Err("Extending a plan needs a new review date.");
            var next = DateTime.SpecifyKind(dto.NewReviewDate.Value.Date, DateTimeKind.Utc);
            if (next <= DateTime.UtcNow.Date) return Err("The new review date must be in the future.");
            pip.SecondReviewDate = next;
            pip.Status = PipStatus.Extended;
        }
        else
        {
            pip.Status = outcome;
            pip.ClosedAt = DateTime.UtcNow;
            pip.ClosedBy = userId;
        }
        pip.Outcome = dto.Notes.Trim();
        Touch(pip, userId);
        await pips.UpdateAsync(pip);

        var result = new AppraisalActionResult(outcome.ToString(),
            outcome switch
            {
                PipStatus.Completed => $"{pip.EmployeeName}'s improvement plan closed — improvement recorded.",
                PipStatus.Extended => $"{pip.EmployeeName}'s plan extended to {pip.SecondReviewDate:dd MMM yyyy}.",
                _ => $"{pip.EmployeeName}'s plan escalated to the disciplinary process.",
            }, pip.Id);

        if (outcome == PipStatus.EscalatedToDisciplinary)
        {
            result.Warnings.Add("Disciplinary case management is H10 — this escalation is recorded here and alerted, but no case has been opened yet.");
            await NotifyAsync(tenantSchema, "Critical",
                $"Improvement plan escalated — {pip.EmployeeName}",
                $"{pip.EmployeeNumber} {pip.EmployeeName}'s improvement plan closed with no improvement (score {pip.TriggerScore:0.##} against a {pip.Threshold:0.##} threshold). {pip.Outcome}",
                "hr.approve");
        }

        await LogAsync("PerformanceImprovementPlan", pip.Id, HrAuditAction.PipClosed,
            $"{pip.EmployeeNumber}: improvement plan {Spaced(outcome)} by {userName ?? userId}. {pip.Outcome}", userId, userName);
        return result;
    }

    /// <summary>P17 steps 17.2–17.3 — the plan a low score raises, with HR and the line manager told at once.</summary>
    private async Task<PerformanceImprovementPlan> RaisePipAsync(Appraisal appraisal, string? tenantSchema, string userId, string? userName)
    {
        var today = DateTime.UtcNow.Date;
        var pip = await pips.CreateAsync(new PerformanceImprovementPlan
        {
            EmployeeId = appraisal.EmployeeId,
            EmployeeNumber = appraisal.EmployeeNumber,
            EmployeeName = appraisal.EmployeeName,
            AppraisalId = appraisal.Id,
            TriggerScore = appraisal.FinalScore ?? 0m,
            // Copied from the appraisal, which copied it from the scorecard — so the plan stays explicable
            // against the rule that was actually in force.
            Threshold = appraisal.PipThreshold,
            StartDate = DateTime.SpecifyKind(today, DateTimeKind.Utc),
            ReviewDate = DateTime.SpecifyKind(today.AddDays(DefaultPipReviewDays), DateTimeKind.Utc),
            Objectives = string.IsNullOrWhiteSpace(appraisal.TrainingNeeds) ? null
                : $"From the appraisal: {appraisal.TrainingNeeds}",
            CreatedBy = userId, UpdatedBy = userId,
        });

        appraisal.PipTriggered = true;
        appraisal.PipId = pip.Id;
        Touch(appraisal, userId);
        await appraisals.UpdateAsync(appraisal);

        await NotifyAsync(tenantSchema, "Warning",
            $"Improvement plan raised — {appraisal.EmployeeName} ({appraisal.CycleName})",
            $"{appraisal.EmployeeNumber} {appraisal.EmployeeName} scored {appraisal.FinalScore:0.##} against a threshold of {appraisal.PipThreshold:0.##}. HR and the line manager should agree objectives and support before the review on {pip.ReviewDate:dd MMM yyyy}.",
            "hr.manager");
        await LogAsync("PerformanceImprovementPlan", pip.Id, HrAuditAction.PipRaised,
            $"{appraisal.EmployeeNumber}: improvement plan raised automatically at {appraisal.FinalScore:0.##} (threshold {appraisal.PipThreshold:0.##}), review {pip.ReviewDate:dd MMM yyyy}.", userId, userName);
        return pip;
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Scoring
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies one step's entries to the item scores. System-sourced items are left alone — an attendance or
    /// 360 figure the system computed must not be quietly overwritten by whoever is filling the form.
    /// </summary>
    private static (string? Error, int Applied) ApplyScores(
        List<AppraisalItemScore> scores, List<ItemScoreInputDto> inputs, bool isManager, string userId)
    {
        var applied = 0;
        foreach (var input in inputs)
        {
            var score = scores.FirstOrDefault(s => s.Id == input.ItemScoreId);
            if (score is null) continue;
            // System-sourced items are not the form-filler's to change. Revenue is only protected once CRM
            // actually supplied a figure — otherwise the item would be unscoreable when CRM is unreachable.
            if (score.Source is KpiTargetSource.Attendance or KpiTargetSource.Feedback360) continue;
            if (score.Source == KpiTargetSource.Revenue && score.TargetValue > 0) continue;

            if (input.Score is < 0 or > 100)
                return ($"'{score.ItemName}': a score must be between 0 and 100.", applied);
            if (input.ActualValue is < 0)
                return ($"'{score.ItemName}': an actual cannot be negative.", applied);

            if (input.ActualValue.HasValue) score.ActualValue = input.ActualValue.Value;

            // A quantitative item scores itself from the numbers; an explicit score overrides only when the
            // target is zero and there is nothing to measure against.
            var derived = score.MeasurementType == KpiMeasurementType.Quantitative && score.TargetValue > 0
                ? Math.Min(Round(score.ActualValue / score.TargetValue * 100m), MaxQuantitativeScore)
                : input.Score;

            if (isManager) score.ManagerScore = derived ?? input.Score;
            else score.SelfScore = derived ?? input.Score;

            score.FinalScore = isManager ? score.ManagerScore : score.FinalScore ?? score.SelfScore;
            if (!string.IsNullOrWhiteSpace(input.Comments)) score.Comments = input.Comments;
            Touch(score, userId);
            applied++;
        }
        return (null, applied);
    }

    /// <summary>Weighted average across the items, on the weights actually present. If some items are unscored
    /// the result reflects only what WAS scored, rather than silently treating a blank as zero.</summary>
    private static decimal? Weighted(List<AppraisalItemScore> scores, Func<AppraisalItemScore, decimal?> pick)
    {
        var scored = scores.Where(s => pick(s) is not null).ToList();
        if (scored.Count == 0) return null;
        var weight = scored.Sum(s => s.WeightPercent);
        if (weight <= 0) return null;
        return Round(scored.Sum(s => pick(s)!.Value * s.WeightPercent) / weight);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════
    private static KpiScorecardDto ToDto(KpiScorecard s, List<KpiScorecardItem> items, int withTargets)
    {
        var active = items.Where(i => i.IsActive).ToList();
        var total = Round(active.Sum(i => i.WeightPercent));
        return new KpiScorecardDto
        {
            Id = s.Id, Name = s.Name, Description = s.Description,
            PositionId = s.PositionId, PositionTitle = s.PositionTitle,
            Year = s.Year, PipThreshold = s.PipThreshold, IsActive = s.IsActive,
            TotalWeight = total, WeightsValid = total == 100m, EmployeesWithTargets = withTargets,
            Items = items.OrderBy(i => i.DisplayOrder).Select(i => new KpiScorecardItemDto
            {
                Id = i.Id, ItemName = i.ItemName, Description = i.Description,
                WeightPercent = i.WeightPercent, MeasurementType = i.MeasurementType.ToString(),
                TargetSource = i.TargetSource.ToString(), Unit = i.Unit,
                DisplayOrder = i.DisplayOrder, IsActive = i.IsActive,
                SourceNote = i.TargetSource switch
                {
                    KpiTargetSource.Attendance => "Scored automatically from the annual attendance scorecard.",
                    KpiTargetSource.Feedback360 => "Scored automatically from the 360 aggregate.",
                    KpiTargetSource.Revenue => "Scored automatically from CRM's sales target and collected revenue.",
                    _ => null,
                },
            }).ToList(),
        };
    }

    private static AppraisalDto ToDto(Appraisal a, List<AppraisalItemScore> scores) => new()
    {
        Id = a.Id, AppraisalCycleId = a.AppraisalCycleId, CycleName = a.CycleName, Year = a.Year,
        EmployeeId = a.EmployeeId, EmployeeNumber = a.EmployeeNumber, EmployeeName = a.EmployeeName,
        PositionTitle = a.PositionTitle, ScorecardName = a.ScorecardName, PipThreshold = a.PipThreshold,
        Status = a.Status.ToString(),
        SelfAssessmentSubmittedAt = a.SelfAssessmentSubmittedAt, LineManagerReviewedAt = a.LineManagerReviewedAt,
        MdSignedOffAt = a.MdSignedOffAt, HrRecordedAt = a.HrRecordedAt,
        SelfScore = a.SelfScore, LineManagerScore = a.LineManagerScore,
        Feedback360Score = a.Feedback360Score, FinalScore = a.FinalScore,
        SelfComments = a.SelfComments, LineManagerComments = a.LineManagerComments,
        MdComments = a.MdComments, HrComments = a.HrComments, TrainingNeeds = a.TrainingNeeds,
        PipTriggered = a.PipTriggered, PipId = a.PipId,
        AwaitingLabel = Awaiting(a.Status),
        ItemScores = scores.OrderBy(s => s.DisplayOrder).Select(s => new AppraisalItemScoreDto
        {
            Id = s.Id, KpiScorecardItemId = s.KpiScorecardItemId, ItemName = s.ItemName,
            WeightPercent = s.WeightPercent, MeasurementType = s.MeasurementType.ToString(),
            Source = s.Source.ToString(), Unit = s.Unit,
            TargetValue = s.TargetValue, ActualValue = s.ActualValue,
            SelfScore = s.SelfScore, ManagerScore = s.ManagerScore, FinalScore = s.FinalScore,
            Comments = s.Comments, DisplayOrder = s.DisplayOrder,
            ScoringNote = s.Source switch
            {
                KpiTargetSource.Attendance => "System-scored from attendance.",
                KpiTargetSource.Feedback360 => "System-scored from the 360 aggregate.",
                KpiTargetSource.Revenue when s.TargetValue > 0 => "System-scored from CRM revenue against target.",
                _ => s.MeasurementType == KpiMeasurementType.Quantitative && s.TargetValue > 0
                    ? "Scored from actual against target."
                    : null,
            },
        }).ToList(),
    };

    private static PipDto ToDto(PerformanceImprovementPlan p, DateTime today) => new()
    {
        Id = p.Id, EmployeeId = p.EmployeeId, EmployeeNumber = p.EmployeeNumber, EmployeeName = p.EmployeeName,
        AppraisalId = p.AppraisalId, TriggerScore = p.TriggerScore, Threshold = p.Threshold,
        Objectives = p.Objectives, SupportProvided = p.SupportProvided,
        StartDate = p.StartDate, ReviewDate = p.ReviewDate, SecondReviewDate = p.SecondReviewDate,
        Status = p.Status.ToString(), Outcome = p.Outcome, ClosedAt = p.ClosedAt,
        LinkedLdpObjectiveId = p.LinkedLdpObjectiveId,
        IsReviewDue = p.Status is PipStatus.Active or PipStatus.Extended
                   && (p.SecondReviewDate ?? p.ReviewDate).Date <= today,
    };

    private static string Awaiting(AppraisalStatus status) => status switch
    {
        AppraisalStatus.PendingSelf => "awaiting the employee's self-assessment",
        AppraisalStatus.PendingLineManager => "awaiting the line manager",
        AppraisalStatus.PendingMd => "awaiting MD sign-off",
        AppraisalStatus.PendingHr => "awaiting HR to record it",
        AppraisalStatus.Completed => "complete",
        _ => "cancelled",
    };

    /// <summary>"MidYear" → "Mid year", "EscalatedToDisciplinary" → "escalated to disciplinary".</summary>
    private static string Spaced(Enum value)
    {
        var text = value.ToString();
        var chars = text.SelectMany((c, i) => i > 0 && char.IsUpper(c) ? [' ', char.ToLowerInvariant(c)] : new[] { c });
        return new string(chars.ToArray());
    }

    private async Task NotifyAsync(string? schema, string severity, string title, string message, string permission)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, "HR", severity, title, message, permission);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static AppraisalActionResult Err(string message) => new("Error", message);
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
