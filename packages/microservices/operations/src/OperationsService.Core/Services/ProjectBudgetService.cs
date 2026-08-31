using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>
/// PR1 — the detailed, approvable project budget, the contract rate card, and quote-vs-spend.
///
/// The approval step is the point of the whole design: a budget is drafted, submitted, and approved
/// as a whole, and approving it is what sets the project's baseline. Editing an approved budget is
/// impossible by construction — you raise a new version, and the old one is kept as Superseded.
/// </summary>
public class ProjectBudgetService(
    IGenericRepository<Project> projects,
    IGenericRepository<BudgetVersion> versions,
    IGenericRepository<BudgetLine> lines,
    IGenericRepository<ContractRate> rates,
    IGenericRepository<ProjectApproval> approvals,
    IGenericRepository<ProjectHistory> history) : IProjectBudgetService
{
    // ── Versions ─────────────────────────────────────────────────────────────

    public async Task<List<BudgetVersionDto>> GetVersionsAsync(string projectId)
    {
        var vs = await versions.Query().Where(v => v.ProjectId == projectId)
                               .OrderByDescending(v => v.VersionNo).ToListAsync();
        var all = await lines.Query().Where(l => l.ProjectId == projectId).ToListAsync();
        var byVersion = all.GroupBy(l => l.BudgetVersionId ?? "").ToDictionary(g => g.Key, g => g.ToList());
        var rateCodes = (await rates.Query().Where(r => r.ProjectId == projectId).ToListAsync())
                        .ToDictionary(r => r.Id, r => r.Code);

        return vs.Select(v => Map(v, byVersion.GetValueOrDefault(v.Id, []), rateCodes)).ToList();
    }

    public async Task<BudgetVersionDto?> GetVersionAsync(string versionId)
    {
        var v = await versions.GetByIdAsync(versionId);
        if (v is null) return null;
        var ls = await lines.Query().Where(l => l.BudgetVersionId == versionId).ToListAsync();
        var rateCodes = (await rates.Query().Where(r => r.ProjectId == v.ProjectId).ToListAsync())
                        .ToDictionary(r => r.Id, r => r.Code);
        return Map(v, ls, rateCodes);
    }

    /// <summary>
    /// Starts a new draft budget. Only one draft may be open per project — two people drafting
    /// competing budgets and each submitting is exactly the ambiguity the approval chain exists to
    /// remove.
    /// </summary>
    public async Task<BudgetVersionDto> CreateVersionAsync(string projectId, CreateBudgetVersionDto dto, string userId)
    {
        var project = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        var existing = await versions.Query().Where(v => v.ProjectId == projectId).ToListAsync();

        var open = existing.FirstOrDefault(v => v.Status is BudgetVersionStatus.Draft or BudgetVersionStatus.PendingApproval);
        if (open != null)
            throw new InvalidOperationException(
                $"Budget v{open.VersionNo} is already {(open.Status == BudgetVersionStatus.Draft ? "in draft" : "awaiting approval")}. " +
                "Finish or withdraw it before starting another.");

        var nextNo = existing.Count == 0 ? 1 : existing.Max(v => v.VersionNo) + 1;
        if (nextNo > 1 && string.IsNullOrWhiteSpace(dto.RevisionReason))
            throw new InvalidOperationException("A reason is required when revising an approved budget.");

        var version = await versions.CreateAsync(new BudgetVersion
        {
            ProjectId = projectId,
            VersionNo = nextNo,
            Status    = BudgetVersionStatus.Draft,
            RevisionReason = dto.RevisionReason?.Trim(),
            CreatedBy = userId, UpdatedBy = userId,
        });

        // Copying the approved lines means a revision starts from what was agreed, not from nothing —
        // otherwise every revision risks silently dropping scope.
        if (dto.CopyFromApproved)
        {
            var approved = existing.FirstOrDefault(v => v.Status == BudgetVersionStatus.Approved);
            if (approved != null)
            {
                var src = await lines.Query().Where(l => l.BudgetVersionId == approved.Id).ToListAsync();
                foreach (var l in src)
                    await lines.CreateAsync(new BudgetLine
                    {
                        ProjectId = projectId, BudgetVersionId = version.Id,
                        Category = l.Category, Description = l.Description,
                        Quantity = l.Quantity, UnitCostRate = l.UnitCostRate, Unit = l.Unit,
                        ContractRateId = l.ContractRateId,
                        PlannedAmount = l.PlannedAmount, QuotedAmount = l.QuotedAmount,
                        CreatedBy = userId, UpdatedBy = userId,
                    });
            }
        }

        await Recalculate(version.Id, userId);
        return (await GetVersionAsync(version.Id))!;
    }

    public async Task<BudgetVersionDto> UpsertLineAsync(string versionId, UpsertBudgetLineDto dto, string userId)
    {
        var version = await versions.GetByIdAsync(versionId)
            ?? throw new KeyNotFoundException("Budget version not found.");
        RequireEditable(version);

        // Pricing precedence: an explicit amount wins; otherwise qty x rate; otherwise the rate card.
        decimal? cost = dto.UnitCostRate;
        decimal? quotedRate = null;
        if (!string.IsNullOrWhiteSpace(dto.ContractRateId))
        {
            var rate = await rates.GetByIdAsync(dto.ContractRateId!)
                ?? throw new KeyNotFoundException("Contract rate not found.");
            if (rate.ProjectId != version.ProjectId)
                throw new InvalidOperationException("That rate belongs to a different project.");
            cost ??= rate.CostRate;
            quotedRate = rate.ClientRate;
        }

        var planned = dto.PlannedAmount
            ?? (dto.Quantity.HasValue && cost.HasValue ? dto.Quantity.Value * cost.Value : 0m);
        var quoted = dto.QuotedAmount
            ?? (dto.Quantity.HasValue && quotedRate.HasValue ? dto.Quantity.Value * quotedRate.Value : 0m);

        if (planned < 0 || quoted < 0)
            throw new InvalidOperationException("Budget amounts cannot be negative.");

        BudgetLine line;
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            line = await lines.CreateAsync(new BudgetLine
            {
                ProjectId = version.ProjectId, BudgetVersionId = versionId,
                Category = dto.Category, Description = dto.Description.Trim(),
                Quantity = dto.Quantity, UnitCostRate = cost, Unit = dto.Unit,
                ContractRateId = dto.ContractRateId,
                PlannedAmount = planned, QuotedAmount = quoted,
                CreatedBy = userId, UpdatedBy = userId,
            });
        }
        else
        {
            line = await lines.GetByIdAsync(dto.Id!) ?? throw new KeyNotFoundException("Budget line not found.");
            if (line.BudgetVersionId != versionId)
                throw new InvalidOperationException("That line belongs to a different budget version.");
            line.Category = dto.Category; line.Description = dto.Description.Trim();
            line.Quantity = dto.Quantity; line.UnitCostRate = cost; line.Unit = dto.Unit;
            line.ContractRateId = dto.ContractRateId;
            line.PlannedAmount = planned; line.QuotedAmount = quoted;
            line.UpdatedAt = DateTime.UtcNow; line.UpdatedBy = userId;
            await lines.UpdateAsync(line);
        }

        await Recalculate(versionId, userId);
        return (await GetVersionAsync(versionId))!;
    }

    public async Task<BudgetVersionDto> DeleteLineAsync(string versionId, string lineId, string userId)
    {
        var version = await versions.GetByIdAsync(versionId) ?? throw new KeyNotFoundException("Budget version not found.");
        RequireEditable(version);
        var line = await lines.GetByIdAsync(lineId) ?? throw new KeyNotFoundException("Budget line not found.");
        if (line.BudgetVersionId != versionId)
            throw new InvalidOperationException("That line belongs to a different budget version.");
        await lines.DeleteAsync(line);
        await Recalculate(versionId, userId);
        return (await GetVersionAsync(versionId))!;
    }

    public async Task<BudgetVersionDto> SubmitAsync(string versionId, string userId)
    {
        var version = await versions.GetByIdAsync(versionId) ?? throw new KeyNotFoundException("Budget version not found.");
        if (version.Status != BudgetVersionStatus.Draft)
            throw new InvalidOperationException($"Only a draft budget can be submitted; this one is {version.Status}.");

        var ls = await lines.Query().Where(l => l.BudgetVersionId == versionId).ToListAsync();
        if (ls.Count == 0)
            throw new InvalidOperationException("Add at least one budget line before submitting for approval.");

        await Recalculate(versionId, userId);
        version = (await versions.GetByIdAsync(versionId))!;
        version.Status      = BudgetVersionStatus.PendingApproval;
        version.SubmittedBy = userId;
        version.SubmittedAt = DateTime.UtcNow;
        version.UpdatedAt   = DateTime.UtcNow; version.UpdatedBy = userId;
        await versions.UpdateAsync(version);

        // Lands in the same approvals trail as the MD/Finance project gates.
        await approvals.CreateAsync(new ProjectApproval
        {
            ProjectId = version.ProjectId, ApprovalType = ApprovalType.Budget,
            Status = ApprovalStatus.Pending, RequestedBy = userId,
            Comments = $"Budget v{version.VersionNo} — planned {version.TotalPlanned:N2}, quoted {version.TotalQuoted:N2}.",
            CreatedBy = userId, UpdatedBy = userId,
        });

        return (await GetVersionAsync(versionId))!;
    }

    /// <summary>
    /// Approves the budget: supersedes the previous approved version, writes the project's planned
    /// and baseline budget, and records the approval. This is the only path that sets a baseline
    /// budget, which is what keeps "what we approved" and "what we are spending" separable.
    /// </summary>
    public async Task<BudgetVersionDto> ApproveAsync(string versionId, string userId)
    {
        var version = await versions.GetByIdAsync(versionId) ?? throw new KeyNotFoundException("Budget version not found.");
        if (version.Status != BudgetVersionStatus.PendingApproval)
            throw new InvalidOperationException($"Only a submitted budget can be approved; this one is {version.Status}.");
        if (version.SubmittedBy == userId)
            throw new InvalidOperationException("A budget cannot be approved by the person who submitted it.");

        var project = await projects.GetByIdAsync(version.ProjectId)
            ?? throw new KeyNotFoundException("Project not found.");

        var previous = await versions.Query()
            .Where(v => v.ProjectId == version.ProjectId && v.Status == BudgetVersionStatus.Approved)
            .ToListAsync();
        foreach (var p in previous)
        {
            p.Status = BudgetVersionStatus.Superseded;
            p.SupersededAt = DateTime.UtcNow;
            p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = userId;
            await versions.UpdateAsync(p);
        }

        version.Status     = BudgetVersionStatus.Approved;
        version.ApprovedBy = userId;
        version.ApprovedAt = DateTime.UtcNow;
        version.UpdatedAt  = DateTime.UtcNow; version.UpdatedBy = userId;
        await versions.UpdateAsync(version);

        var wasBaselined = project.BaselineSetAt != null;
        project.PlannedBudget = version.TotalPlanned;
        project.BaselineBudget = version.TotalPlanned;
        project.BaselineSetAt  = DateTime.UtcNow;
        // A revision that raises the ceiling clears the 100% lock; leaving it set would block spend
        // against a budget that has just been increased for exactly that reason.
        if (project.BudgetLocked && project.ActualCost + project.Committed < version.TotalPlanned)
            project.BudgetLocked = false;
        project.UpdatedAt = DateTime.UtcNow; project.UpdatedBy = userId;
        await projects.UpdateAsync(project);

        var pending = await approvals.Query()
            .Where(a => a.ProjectId == version.ProjectId && a.ApprovalType == ApprovalType.Budget
                     && a.Status == ApprovalStatus.Pending)
            .ToListAsync();
        foreach (var a in pending)
        {
            a.Status = ApprovalStatus.Approved; a.ReviewedBy = userId; a.ReviewedAt = DateTime.UtcNow;
            a.UpdatedAt = DateTime.UtcNow; a.UpdatedBy = userId;
            await approvals.UpdateAsync(a);
        }

        await history.CreateAsync(new ProjectHistory
        {
            ProjectId = version.ProjectId, Action = "BudgetApproved",
            OldValue = wasBaselined ? previous.FirstOrDefault()?.TotalPlanned.ToString("N2") : null,
            NewValue = version.TotalPlanned.ToString("N2"),
            Notes = $"Budget v{version.VersionNo} approved" +
                    (version.RevisionReason is null ? "." : $": {version.RevisionReason}"),
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });

        return (await GetVersionAsync(versionId))!;
    }

    public async Task<BudgetVersionDto> RejectAsync(string versionId, string reason, string userId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to reject a budget.");

        var version = await versions.GetByIdAsync(versionId) ?? throw new KeyNotFoundException("Budget version not found.");
        if (version.Status != BudgetVersionStatus.PendingApproval)
            throw new InvalidOperationException($"Only a submitted budget can be rejected; this one is {version.Status}.");

        version.Status = BudgetVersionStatus.Rejected;
        version.RejectedBy = userId; version.RejectedAt = DateTime.UtcNow;
        version.RejectionReason = reason.Trim();
        version.UpdatedAt = DateTime.UtcNow; version.UpdatedBy = userId;
        await versions.UpdateAsync(version);

        var pending = await approvals.Query()
            .Where(a => a.ProjectId == version.ProjectId && a.ApprovalType == ApprovalType.Budget
                     && a.Status == ApprovalStatus.Pending)
            .ToListAsync();
        foreach (var a in pending)
        {
            a.Status = ApprovalStatus.Rejected; a.ReviewedBy = userId; a.ReviewedAt = DateTime.UtcNow;
            a.RejectionReason = reason.Trim();
            a.UpdatedAt = DateTime.UtcNow; a.UpdatedBy = userId;
            await approvals.UpdateAsync(a);
        }

        return (await GetVersionAsync(versionId))!;
    }

    // ── Rate card ────────────────────────────────────────────────────────────

    public async Task<List<ContractRateDto>> GetRatesAsync(string projectId, bool activeOnly = false)
    {
        var q = rates.Query().Where(r => r.ProjectId == projectId);
        if (activeOnly) q = q.Where(r => r.IsActive);
        return (await q.OrderBy(r => r.Category).ThenBy(r => r.Code).ToListAsync()).Select(MapRate).ToList();
    }

    public async Task<ContractRateDto> AddRateAsync(string projectId, UpsertContractRateDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Code)) throw new InvalidOperationException("A rate code is required.");
        if (dto.ClientRate < 0 || dto.CostRate < 0) throw new InvalidOperationException("Rates cannot be negative.");

        var clash = await rates.Query()
            .FirstOrDefaultAsync(r => r.ProjectId == projectId && r.IsActive && r.Code.ToUpper() == dto.Code.Trim().ToUpper());
        if (clash != null)
            throw new InvalidOperationException($"An active rate with code '{clash.Code}' already exists on this project.");

        var rate = await rates.CreateAsync(new ContractRate
        {
            ProjectId = projectId,
            Code = dto.Code.Trim().ToUpperInvariant(),
            Description = dto.Description.Trim(),
            Category = dto.Category, Unit = dto.Unit,
            ClientRate = dto.ClientRate, CostRate = dto.CostRate,
            EffectiveFrom = dto.EffectiveFrom ?? DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
        return MapRate(rate);
    }

    /// <summary>
    /// Retires a rate rather than deleting it: budget lines priced under it keep explaining their
    /// own numbers, which a deleted rate could not.
    /// </summary>
    public async Task DeactivateRateAsync(string rateId, string userId)
    {
        var rate = await rates.GetByIdAsync(rateId) ?? throw new KeyNotFoundException("Contract rate not found.");
        rate.IsActive = false;
        rate.EffectiveTo = DateTime.UtcNow;
        rate.UpdatedAt = DateTime.UtcNow; rate.UpdatedBy = userId;
        await rates.UpdateAsync(rate);
    }

    // ── Quote vs spend ───────────────────────────────────────────────────────

    public async Task<ProjectCommercialsDto> GetCommercialsAsync(string projectId)
    {
        var project = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");

        // Report against the approved budget. Falling back to every line for projects that predate
        // versioning keeps historical projects readable instead of showing them as empty.
        var approved = await versions.Query()
            .FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Status == BudgetVersionStatus.Approved);

        var ls = approved is null
            ? await lines.Query().Where(l => l.ProjectId == projectId && l.BudgetVersionId == null).ToListAsync()
            : await lines.Query().Where(l => l.BudgetVersionId == approved.Id).ToListAsync();

        var rateCodes = (await rates.Query().Where(r => r.ProjectId == projectId).ToListAsync())
                        .ToDictionary(r => r.Id, r => r.Code);

        var quoted   = ls.Sum(l => l.QuotedAmount);
        var planned  = ls.Sum(l => l.PlannedAmount);
        var actual   = project.ActualCost;
        var exposure = actual + project.Committed;

        // Prefer the sum of quoted lines; fall back to the headline contract value when the budget
        // has not been priced per line yet, so margin is never reported as 100%.
        var revenue = quoted > 0 ? quoted : project.ContractValue;

        return new ProjectCommercialsDto
        {
            ProjectId = projectId, ProjectName = project.Name,
            ContractValue = project.ContractValue,
            QuotedTotal   = quoted,
            PlannedCost   = planned,
            BaselineBudget = project.BaselineBudget,
            Committed  = project.Committed,
            ActualCost = actual,
            Exposure   = exposure,
            ProjectedMargin = revenue - exposure,
            ProjectedMarginPct = revenue == 0 ? 0 : Math.Round((revenue - exposure) / revenue * 100m, 2),
            VarianceToBaseline = project.BaselineBudget is null ? null : exposure - project.BaselineBudget.Value,
            ByCategory = ls.GroupBy(l => l.Category).Select(g => new CommercialCategoryDto
            {
                Category = g.Key.ToString(),
                Quoted   = g.Sum(l => l.QuotedAmount),
                Planned  = g.Sum(l => l.PlannedAmount),
                Actual   = g.Sum(l => l.ActualAmount),
                Margin   = g.Sum(l => l.QuotedAmount) - g.Sum(l => l.ActualAmount),
                BurnPct  = g.Sum(l => l.PlannedAmount) == 0
                    ? 0 : Math.Round(g.Sum(l => l.ActualAmount) / g.Sum(l => l.PlannedAmount) * 100m, 2),
            }).OrderByDescending(c => c.Planned).ToList(),
            Lines = ls.Select(l => MapLine(l, rateCodes)).ToList(),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void RequireEditable(BudgetVersion v)
    {
        if (v.Status != BudgetVersionStatus.Draft)
            throw new InvalidOperationException(
                $"Budget v{v.VersionNo} is {v.Status} and can no longer be edited. Raise a new version to change it.");
    }

    private async Task Recalculate(string versionId, string userId)
    {
        var version = await versions.GetByIdAsync(versionId);
        if (version is null) return;
        var ls = await lines.Query().Where(l => l.BudgetVersionId == versionId).ToListAsync();
        version.TotalPlanned = ls.Sum(l => l.PlannedAmount);
        version.TotalQuoted  = ls.Sum(l => l.QuotedAmount);
        version.UpdatedAt = DateTime.UtcNow; version.UpdatedBy = userId;
        await versions.UpdateAsync(version);
    }

    private static BudgetVersionDto Map(BudgetVersion v, List<BudgetLine> ls, Dictionary<string, string> rateCodes) => new()
    {
        Id = v.Id, ProjectId = v.ProjectId, VersionNo = v.VersionNo,
        Status = v.Status.ToString(), RevisionReason = v.RevisionReason,
        TotalPlanned = v.TotalPlanned, TotalQuoted = v.TotalQuoted,
        PlannedMargin = v.TotalQuoted - v.TotalPlanned,
        PlannedMarginPct = v.TotalQuoted == 0 ? 0 : Math.Round((v.TotalQuoted - v.TotalPlanned) / v.TotalQuoted * 100m, 2),
        SubmittedBy = v.SubmittedBy, SubmittedAt = v.SubmittedAt,
        ApprovedBy = v.ApprovedBy, ApprovedAt = v.ApprovedAt,
        RejectedBy = v.RejectedBy, RejectedAt = v.RejectedAt, RejectionReason = v.RejectionReason,
        SupersededAt = v.SupersededAt,
        Lines = ls.Select(l => MapLine(l, rateCodes)).ToList(),
    };

    private static BudgetLineDetailDto MapLine(BudgetLine l, Dictionary<string, string> rateCodes) => new()
    {
        Id = l.Id, Category = l.Category.ToString(), Description = l.Description,
        Quantity = l.Quantity, UnitCostRate = l.UnitCostRate, Unit = l.Unit?.ToString(),
        ContractRateId = l.ContractRateId,
        ContractRateCode = l.ContractRateId is null ? null : rateCodes.GetValueOrDefault(l.ContractRateId),
        PlannedAmount = l.PlannedAmount, QuotedAmount = l.QuotedAmount, ActualAmount = l.ActualAmount,
        PlannedMargin = l.PlannedMargin, ActualMargin = l.ActualMargin,
    };

    private static ContractRateDto MapRate(ContractRate r) => new()
    {
        Id = r.Id, Code = r.Code, Description = r.Description,
        Category = r.Category.ToString(), Unit = r.Unit.ToString(),
        ClientRate = r.ClientRate, CostRate = r.CostRate, MarginPerUnit = r.MarginPerUnit,
        EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, IsActive = r.IsActive,
    };
}
