using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Governance;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>PR3 — see <see cref="IProjectGovernanceService"/>.</summary>
public class ProjectGovernanceService(
    IGenericRepository<Project> projects,
    IGenericRepository<Milestone> milestones,
    IGenericRepository<RiskEntry> risks,
    IGenericRepository<ProjectIssue> issues,
    IGenericRepository<ChangeRequest> changeRequests,
    IGenericRepository<BudgetVersion> budgetVersions,
    IGenericRepository<ProjectHistory> history,
    IProjectBudgetService budgets) : IProjectGovernanceService
{
    // ── Risks ────────────────────────────────────────────────────────────────────

    public async Task<List<ProjectRiskDto>> GetRisksAsync(string projectId, bool includeClosed = false)
    {
        var q = risks.Query().Where(r => r.ProjectId == projectId && !r.IsDeleted);
        if (!includeClosed)
            q = q.Where(r => r.Status != RiskStatus.Closed && r.Status != RiskStatus.Realised);

        // Highest score first — the register exists to say what to worry about, so it should not need
        // sorting by hand every time it is opened.
        var list = await q.OrderByDescending(r => r.Score).ThenBy(r => r.CreatedAt).ToListAsync();
        return list.Select(MapRisk).ToList();
    }

    public async Task<ProjectRiskDto> AddRiskAsync(string projectId, UpsertProjectRiskDto dto, string userId)
    {
        _ = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("A risk needs a title.");

        var likelihood = ParseEnum(dto.Likelihood, RiskLikelihood.Low);
        var impact     = ParseEnum(dto.Impact, RiskImpact.Low);

        var risk = await risks.CreateAsync(new RiskEntry
        {
            ProjectId   = projectId,
            Title       = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Likelihood  = likelihood,
            Impact      = impact,
            Score       = RiskEntry.ScoreOf(likelihood, impact),
            Mitigation  = dto.Mitigation,
            Owner       = dto.Owner,
            OwnerUserId = dto.OwnerUserId,
            Status      = ParseEnum(dto.Status, RiskStatus.Open),
            ReviewDate  = dto.ReviewDate,
            CreatedBy   = userId,
            UpdatedBy   = userId,
        });
        return MapRisk(risk);
    }

    public async Task<ProjectRiskDto> UpdateRiskAsync(string riskId, UpsertProjectRiskDto dto, string userId)
    {
        var risk = await risks.GetByIdAsync(riskId)
            ?? throw new KeyNotFoundException($"Risk {riskId} not found.");
        if (risk.Status == RiskStatus.Realised)
            throw new InvalidOperationException(
                "This risk has already been realised as an issue — update the issue instead.");

        if (!string.IsNullOrWhiteSpace(dto.Title))       risk.Title = dto.Title.Trim();
        if (dto.Description != null)                     risk.Description = dto.Description.Trim();
        if (dto.Likelihood != null)                      risk.Likelihood = ParseEnum(dto.Likelihood, risk.Likelihood);
        if (dto.Impact != null)                          risk.Impact = ParseEnum(dto.Impact, risk.Impact);
        if (dto.Mitigation != null)                      risk.Mitigation = dto.Mitigation;
        if (dto.Owner != null)                           risk.Owner = dto.Owner;
        if (dto.OwnerUserId != null)                     risk.OwnerUserId = dto.OwnerUserId;
        if (dto.ReviewDate.HasValue)                     risk.ReviewDate = dto.ReviewDate;
        if (dto.Status != null)
        {
            var status = ParseEnum(dto.Status, risk.Status);
            // Realising a risk creates an issue and must go through RealiseRiskAsync — allowing it
            // here would set the status without ever producing the issue it claims to have become.
            if (status == RiskStatus.Realised)
                throw new InvalidOperationException(
                    "Use the realise action to turn a risk into an issue, so the issue is actually created.");
            risk.Status = status;
            risk.ClosedAt = status == RiskStatus.Closed ? DateTime.UtcNow : null;
        }

        risk.Score     = RiskEntry.ScoreOf(risk.Likelihood, risk.Impact);
        risk.UpdatedBy = userId;
        risk.UpdatedAt = DateTime.UtcNow;
        await risks.UpdateAsync(risk);
        return MapRisk(risk);
    }

    public async Task DeleteRiskAsync(string riskId, string userId)
    {
        var risk = await risks.GetByIdAsync(riskId)
            ?? throw new KeyNotFoundException($"Risk {riskId} not found.");
        risk.IsDeleted = true;
        risk.UpdatedBy = userId;
        risk.UpdatedAt = DateTime.UtcNow;
        await risks.UpdateAsync(risk);
    }

    public async Task<ProjectIssueDto> RealiseRiskAsync(string riskId, RealiseRiskDto dto, string userId)
    {
        var risk = await risks.GetByIdAsync(riskId)
            ?? throw new KeyNotFoundException($"Risk {riskId} not found.");
        if (risk.Status == RiskStatus.Realised)
            throw new InvalidOperationException("This risk has already been realised.");
        if (string.IsNullOrWhiteSpace(risk.ProjectId))
            throw new InvalidOperationException("Only a project risk can be realised into a project issue.");

        var issue = await CreateIssueAsync(risk.ProjectId!, new UpsertProjectIssueDto
        {
            Title       = dto.Title ?? risk.Title,
            Description = dto.Description ?? risk.Description,
            MilestoneId = dto.MilestoneId,
            // A realised risk inherits severity from its impact — the impact was the estimate of how
            // bad it would be, and it just happened.
            Severity    = dto.Severity ?? SeverityFromImpact(risk.Impact).ToString(),
            OwnerUserId = dto.OwnerUserId ?? risk.OwnerUserId,
            TargetResolutionDate = dto.TargetResolutionDate,
        }, userId, raisedFromRiskId: risk.Id);

        risk.Status            = RiskStatus.Realised;
        risk.RealisedAsIssueId = issue.Id;
        risk.ClosedAt          = DateTime.UtcNow;
        risk.UpdatedBy         = userId;
        risk.UpdatedAt         = DateTime.UtcNow;
        await risks.UpdateAsync(risk);

        return issue;
    }

    // ── Issues ───────────────────────────────────────────────────────────────────

    public async Task<List<ProjectIssueDto>> GetIssuesAsync(string projectId, bool includeClosed = false)
    {
        var q = issues.Query().Where(i => i.ProjectId == projectId && !i.IsDeleted);
        if (!includeClosed)
            q = q.Where(i => i.Status != IssueStatus.Closed);

        var list = await q.OrderByDescending(i => i.Severity).ThenBy(i => i.RaisedAt).ToListAsync();
        return list.Select(MapIssue).ToList();
    }

    public Task<ProjectIssueDto> AddIssueAsync(string projectId, UpsertProjectIssueDto dto, string userId) =>
        CreateIssueAsync(projectId, dto, userId, raisedFromRiskId: null);

    private async Task<ProjectIssueDto> CreateIssueAsync(
        string projectId, UpsertProjectIssueDto dto, string userId, string? raisedFromRiskId)
    {
        _ = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("An issue needs a title.");

        var issue = await issues.CreateAsync(new ProjectIssue
        {
            ProjectId   = projectId,
            MilestoneId = dto.MilestoneId,
            Number      = await NextNumberAsync("ISS"),
            Title       = dto.Title.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Severity    = ParseEnum(dto.Severity, IssueSeverity.Medium),
            Status      = ParseEnum(dto.Status, IssueStatus.Open),
            OwnerUserId = dto.OwnerUserId,
            RaisedAt    = DateTime.UtcNow,
            RaisedBy    = userId,
            TargetResolutionDate = dto.TargetResolutionDate,
            RaisedFromRiskId     = raisedFromRiskId,
            CreatedBy   = userId,
            UpdatedBy   = userId,
        });
        return MapIssue(issue);
    }

    public async Task<ProjectIssueDto> UpdateIssueAsync(string issueId, UpsertProjectIssueDto dto, string userId)
    {
        var issue = await issues.GetByIdAsync(issueId)
            ?? throw new KeyNotFoundException($"Issue {issueId} not found.");
        if (issue.Status == IssueStatus.Closed)
            throw new InvalidOperationException("A closed issue cannot be edited.");

        if (!string.IsNullOrWhiteSpace(dto.Title))  issue.Title = dto.Title.Trim();
        if (dto.Description != null)                issue.Description = dto.Description.Trim();
        if (dto.MilestoneId != null)                issue.MilestoneId = dto.MilestoneId;
        if (dto.Severity != null)                   issue.Severity = ParseEnum(dto.Severity, issue.Severity);
        if (dto.OwnerUserId != null)                issue.OwnerUserId = dto.OwnerUserId;
        if (dto.TargetResolutionDate.HasValue)      issue.TargetResolutionDate = dto.TargetResolutionDate;
        if (dto.Status != null)
        {
            var status = ParseEnum(dto.Status, issue.Status);
            // Resolving carries a resolution note, so it has its own action. Letting the status be
            // set here would produce resolved issues with no record of how.
            if (status is IssueStatus.Resolved or IssueStatus.Closed)
                throw new InvalidOperationException("Use the resolve action to close an issue, so the resolution is recorded.");
            issue.Status = status;
        }

        issue.UpdatedBy = userId;
        issue.UpdatedAt = DateTime.UtcNow;
        await issues.UpdateAsync(issue);
        return MapIssue(issue);
    }

    public async Task<ProjectIssueDto> ResolveIssueAsync(string issueId, ResolveIssueDto dto, string userId)
    {
        var issue = await issues.GetByIdAsync(issueId)
            ?? throw new KeyNotFoundException($"Issue {issueId} not found.");
        if (issue.Status == IssueStatus.Closed)
            throw new InvalidOperationException("This issue is already closed.");
        if (string.IsNullOrWhiteSpace(dto.Resolution))
            throw new InvalidOperationException("Say how the issue was resolved.");

        issue.Resolution = dto.Resolution.Trim();
        issue.ResolvedAt = DateTime.UtcNow;
        issue.ResolvedBy = userId;
        issue.Status     = dto.Close ? IssueStatus.Closed : IssueStatus.Resolved;
        issue.UpdatedBy  = userId;
        issue.UpdatedAt  = DateTime.UtcNow;
        await issues.UpdateAsync(issue);
        return MapIssue(issue);
    }

    // ── Change requests ──────────────────────────────────────────────────────────

    public async Task<List<ChangeRequestDto>> GetChangeRequestsAsync(string projectId)
    {
        var list = await changeRequests.Query()
            .Where(c => c.ProjectId == projectId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var versionIds = list.Where(c => c.BudgetVersionId != null).Select(c => c.BudgetVersionId!).ToList();
        var versions = versionIds.Count == 0
            ? new List<BudgetVersion>()
            : await budgetVersions.Query().Where(v => versionIds.Contains(v.Id)).ToListAsync();

        return list.Select(c => MapCr(c, versions.FirstOrDefault(v => v.Id == c.BudgetVersionId))).ToList();
    }

    public async Task<ChangeRequestDto> CreateChangeRequestAsync(string projectId, UpsertChangeRequestDto dto, string userId)
    {
        _ = await projects.GetByIdAsync(projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found.");
        Validate(dto);
        await ValidateBudgetVersionAsync(projectId, dto.BudgetVersionId);

        var cr = await changeRequests.CreateAsync(new ChangeRequest
        {
            ProjectId     = projectId,
            Number        = await NextNumberAsync("CR"),
            Title         = dto.Title.Trim(),
            Description   = dto.Description?.Trim() ?? string.Empty,
            Justification = dto.Justification.Trim(),
            ScheduleImpactDays = dto.ScheduleImpactDays,
            BudgetVersionId    = dto.BudgetVersionId,
            Status        = ChangeRequestStatus.Draft,
            RequestedBy   = userId,
            CreatedBy     = userId,
            UpdatedBy     = userId,
        });
        return MapCr(cr, null);
    }

    public async Task<ChangeRequestDto> UpdateChangeRequestAsync(string crId, UpsertChangeRequestDto dto, string userId)
    {
        var cr = await LoadDraftAsync(crId);
        Validate(dto);
        await ValidateBudgetVersionAsync(cr.ProjectId, dto.BudgetVersionId);

        cr.Title              = dto.Title.Trim();
        cr.Description        = dto.Description?.Trim() ?? string.Empty;
        cr.Justification      = dto.Justification.Trim();
        cr.ScheduleImpactDays = dto.ScheduleImpactDays;
        cr.BudgetVersionId    = dto.BudgetVersionId;
        cr.UpdatedBy          = userId;
        cr.UpdatedAt          = DateTime.UtcNow;
        await changeRequests.UpdateAsync(cr);
        return MapCr(cr, null);
    }

    public async Task<ChangeRequestDto> SubmitChangeRequestAsync(string crId, string userId)
    {
        var cr = await LoadDraftAsync(crId);
        if (cr.ScheduleImpactDays == 0 && cr.BudgetVersionId is null)
            throw new InvalidOperationException(
                "A change request must move something — set a schedule impact, link a budget version, or both.");

        // The linked budget has to be ready to approve at the moment the change request is decided,
        // and approving a budget requires it to have been submitted. Catching that here rather than
        // at decision time means the requester fixes it, not the approver — and the approver never
        // meets a half-applied change (the schedule would already have shifted before the budget hop
        // threw).
        if (cr.BudgetVersionId is not null)
        {
            var version = await budgetVersions.GetByIdAsync(cr.BudgetVersionId)
                ?? throw new KeyNotFoundException("The linked budget version no longer exists.");
            if (version.Status != BudgetVersionStatus.PendingApproval)
                throw new InvalidOperationException(
                    $"Submit budget v{version.VersionNo} for approval before submitting this change request — " +
                    $"it is currently {version.Status}.");
        }

        cr.Status      = ChangeRequestStatus.Submitted;
        cr.SubmittedAt = DateTime.UtcNow;
        cr.UpdatedBy   = userId;
        cr.UpdatedAt   = DateTime.UtcNow;
        await changeRequests.UpdateAsync(cr);
        return MapCr(cr, null);
    }

    public async Task<ChangeRequestDto> WithdrawChangeRequestAsync(string crId, string userId)
    {
        var cr = await changeRequests.GetByIdAsync(crId)
            ?? throw new KeyNotFoundException($"Change request {crId} not found.");
        if (cr.Status is ChangeRequestStatus.Approved or ChangeRequestStatus.Rejected)
            throw new InvalidOperationException($"A {cr.Status} change request cannot be withdrawn.");

        cr.Status    = ChangeRequestStatus.Withdrawn;
        cr.UpdatedBy = userId;
        cr.UpdatedAt = DateTime.UtcNow;
        await changeRequests.UpdateAsync(cr);
        return MapCr(cr, null);
    }

    public async Task<ChangeRequestDto> DecideChangeRequestAsync(string crId, DecideChangeRequestDto dto, string userId)
    {
        var cr = await changeRequests.GetByIdAsync(crId)
            ?? throw new KeyNotFoundException($"Change request {crId} not found.");
        if (cr.Status != ChangeRequestStatus.Submitted)
            throw new InvalidOperationException($"Only a submitted change request can be decided; this one is {cr.Status}.");
        // Same segregation of duties as the budget: authorising your own baseline move is the whole
        // thing change control exists to prevent.
        if (cr.RequestedBy == userId)
            throw new InvalidOperationException("A change request cannot be approved by the person who raised it.");

        if (!dto.Approved)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                throw new InvalidOperationException("Give a reason when rejecting a change request.");
            cr.Status         = ChangeRequestStatus.Rejected;
            cr.DecisionReason = dto.Reason.Trim();
            cr.DecidedBy      = userId;
            cr.DecidedAt      = DateTime.UtcNow;
            cr.UpdatedBy      = userId;
            cr.UpdatedAt      = DateTime.UtcNow;
            await changeRequests.UpdateAsync(cr);
            return MapCr(cr, null);
        }

        var project = await projects.GetByIdAsync(cr.ProjectId)
            ?? throw new KeyNotFoundException("Project not found.");

        // Snapshot what we are about to move, BEFORE moving it. This is the record that lets a
        // variance report answer "against what did we originally agree" after the baseline shifts.
        var ms = await milestones.Query()
            .Where(m => m.ProjectId == cr.ProjectId && !m.IsDeleted)
            .ToListAsync();

        cr.PreviousBaselineBudget = project.BaselineBudget;
        cr.PreviousBaselineSetAt  = project.BaselineSetAt;
        cr.PreviousMilestoneBaselines = JsonSerializer.Serialize(ms.Select(m => new
        {
            milestoneId   = m.Id,
            baselineStart = m.BaselineStart,
            baselineDue   = m.BaselineDue,
        }));

        // Schedule: shift every milestone baseline by the approved days. Only baselines move — the
        // live StartDate/DueDate are the team's working plan and are theirs to manage; re-baselining
        // is about what we agreed, not about rewriting where the work currently sits.
        var shifted = 0;
        if (cr.ScheduleImpactDays != 0)
        {
            foreach (var m in ms)
            {
                if (m.BaselineStart is null && m.BaselineDue is null) continue;
                if (m.BaselineStart.HasValue) m.BaselineStart = m.BaselineStart.Value.AddDays(cr.ScheduleImpactDays);
                if (m.BaselineDue.HasValue)   m.BaselineDue   = m.BaselineDue.Value.AddDays(cr.ScheduleImpactDays);
                m.UpdatedBy = userId;
                m.UpdatedAt = DateTime.UtcNow;
                await milestones.UpdateAsync(m);
                shifted++;
            }
        }
        cr.MilestonesShifted = shifted;

        // Budget: approving the linked version is what moves BaselineBudget. Deliberately delegated
        // rather than reimplemented here — one mechanism, so the two can never disagree.
        if (cr.BudgetVersionId is not null)
        {
            await budgets.ApproveAsync(cr.BudgetVersionId, userId);
            var approved = await budgetVersions.GetByIdAsync(cr.BudgetVersionId);
            cr.NewBaselineBudget = approved?.TotalPlanned;
        }
        else
        {
            cr.NewBaselineBudget = project.BaselineBudget;
            // A schedule-only change still re-stamps when the baseline was agreed, so the project
            // does not claim its plan of record dates from before a change that moved it.
            project.BaselineSetAt = DateTime.UtcNow;
            project.UpdatedBy = userId;
            project.UpdatedAt = DateTime.UtcNow;
            await projects.UpdateAsync(project);
        }

        cr.Status         = ChangeRequestStatus.Approved;
        cr.DecisionReason = dto.Reason?.Trim();
        cr.DecidedBy      = userId;
        cr.DecidedAt      = DateTime.UtcNow;
        cr.UpdatedBy      = userId;
        cr.UpdatedAt      = DateTime.UtcNow;
        await changeRequests.UpdateAsync(cr);

        await history.CreateAsync(new ProjectHistory
        {
            ProjectId = cr.ProjectId,
            Action    = "ChangeRequestApproved",
            OldValue  = cr.PreviousBaselineBudget?.ToString("N2"),
            NewValue  = cr.NewBaselineBudget?.ToString("N2"),
            Notes     = $"{cr.Number} approved: {cr.Title}. " +
                        $"Schedule {(cr.ScheduleImpactDays >= 0 ? "+" : "")}{cr.ScheduleImpactDays}d across {shifted} milestone(s).",
            ChangedByUserId = userId, CreatedBy = userId, UpdatedBy = userId,
        });

        return MapCr(cr, null);
    }

    public async Task<GovernanceSummaryDto> GetSummaryAsync(string projectId)
    {
        var today = DateTime.UtcNow.Date;

        var liveRisks = await risks.Query()
            .Where(r => r.ProjectId == projectId && !r.IsDeleted
                     && r.Status != RiskStatus.Closed && r.Status != RiskStatus.Realised)
            .ToListAsync();
        var liveIssues = await issues.Query()
            .Where(i => i.ProjectId == projectId && !i.IsDeleted && i.Status != IssueStatus.Closed)
            .ToListAsync();

        return new GovernanceSummaryDto
        {
            OpenRisks          = liveRisks.Count,
            HighRisks          = liveRisks.Count(r => r.Score >= 6),
            RisksOverdueReview = liveRisks.Count(r => r.ReviewDate.HasValue && r.ReviewDate.Value.Date < today),
            OpenIssues         = liveIssues.Count,
            OverdueIssues      = liveIssues.Count(i =>
                                    i.Status != IssueStatus.Resolved
                                 && i.TargetResolutionDate.HasValue
                                 && i.TargetResolutionDate.Value.Date < today),
            PendingChangeRequests = await changeRequests.Query()
                .CountAsync(c => c.ProjectId == projectId && !c.IsDeleted
                              && c.Status == ChangeRequestStatus.Submitted),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void Validate(UpsertChangeRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            throw new InvalidOperationException("A change request needs a title.");
        if (string.IsNullOrWhiteSpace(dto.Justification))
            throw new InvalidOperationException("A change request needs a justification — an unexplained baseline move is what this exists to prevent.");
    }

    private async Task<ChangeRequest> LoadDraftAsync(string crId)
    {
        var cr = await changeRequests.GetByIdAsync(crId)
            ?? throw new KeyNotFoundException($"Change request {crId} not found.");
        if (cr.Status != ChangeRequestStatus.Draft)
            throw new InvalidOperationException($"Only a draft change request can be edited. Current status: {cr.Status}.");
        return cr;
    }

    private async Task ValidateBudgetVersionAsync(string projectId, string? versionId)
    {
        if (versionId is null) return;

        var version = await budgetVersions.GetByIdAsync(versionId)
            ?? throw new KeyNotFoundException($"Budget version {versionId} not found.");
        if (version.ProjectId != projectId)
            throw new InvalidOperationException("That budget version belongs to a different project.");
        // It has to still be decidable when the change request is approved. An already-approved
        // version would mean the budget moved without the change request authorising it.
        if (version.Status is BudgetVersionStatus.Approved or BudgetVersionStatus.Superseded
                           or BudgetVersionStatus.Rejected)
            throw new InvalidOperationException(
                $"Budget version v{version.VersionNo} is {version.Status} and cannot be attached to a change request.");
    }

    private async Task<string> NextNumberAsync(string prefix)
    {
        var year = DateTime.UtcNow.Year;
        var stem = $"{prefix}-{year}-";
        var count = prefix == "CR"
            ? await changeRequests.Query().CountAsync(c => c.Number.StartsWith(stem))
            : await issues.Query().CountAsync(i => i.Number.StartsWith(stem));
        return $"{stem}{count + 1:0000}";
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static IssueSeverity SeverityFromImpact(RiskImpact impact) => impact switch
    {
        RiskImpact.High   => IssueSeverity.High,
        RiskImpact.Medium => IssueSeverity.Medium,
        _                 => IssueSeverity.Low,
    };

    /// <summary>Score bands for a consistent colour on the register: 1–2 low, 3–4 medium, 6 high, 9 critical.</summary>
    private static string SeverityBand(int score) => score switch
    {
        >= 9 => "Critical",
        >= 6 => "High",
        >= 3 => "Medium",
        _    => "Low",
    };

    private static ProjectRiskDto MapRisk(RiskEntry r) => new()
    {
        Id = r.Id, ProjectId = r.ProjectId, AssignmentId = r.AssignmentId,
        Title = r.Title, Description = r.Description,
        Likelihood = r.Likelihood.ToString(), Impact = r.Impact.ToString(),
        Score = r.Score, Severity = SeverityBand(r.Score),
        Mitigation = r.Mitigation, Owner = r.Owner, OwnerUserId = r.OwnerUserId,
        Status = r.Status.ToString(), ReviewDate = r.ReviewDate,
        ReviewOverdue = r.ReviewDate.HasValue
                     && r.ReviewDate.Value.Date < DateTime.UtcNow.Date
                     && r.Status != RiskStatus.Closed && r.Status != RiskStatus.Realised,
        RealisedAsIssueId = r.RealisedAsIssueId, ClosedAt = r.ClosedAt, CreatedAt = r.CreatedAt,
    };

    private static ProjectIssueDto MapIssue(ProjectIssue i) => new()
    {
        Id = i.Id, ProjectId = i.ProjectId, MilestoneId = i.MilestoneId,
        Number = i.Number, Title = i.Title, Description = i.Description,
        Severity = i.Severity.ToString(), Status = i.Status.ToString(),
        OwnerUserId = i.OwnerUserId, RaisedAt = i.RaisedAt, RaisedBy = i.RaisedBy,
        TargetResolutionDate = i.TargetResolutionDate,
        Overdue = i.TargetResolutionDate.HasValue
               && i.TargetResolutionDate.Value.Date < DateTime.UtcNow.Date
               && i.Status is IssueStatus.Open or IssueStatus.InProgress,
        ResolvedAt = i.ResolvedAt, ResolvedBy = i.ResolvedBy, Resolution = i.Resolution,
        RaisedFromRiskId = i.RaisedFromRiskId, CreatedAt = i.CreatedAt,
    };

    private static ChangeRequestDto MapCr(ChangeRequest c, BudgetVersion? version) => new()
    {
        Id = c.Id, ProjectId = c.ProjectId, Number = c.Number,
        Title = c.Title, Description = c.Description, Justification = c.Justification,
        ScheduleImpactDays = c.ScheduleImpactDays,
        BudgetVersionId = c.BudgetVersionId,
        BudgetVersionNo = version?.VersionNo,
        BudgetVersionTotal = version?.TotalPlanned,
        Status = c.Status.ToString(),
        RequestedBy = c.RequestedBy, SubmittedAt = c.SubmittedAt,
        DecidedBy = c.DecidedBy, DecidedAt = c.DecidedAt, DecisionReason = c.DecisionReason,
        PreviousBaselineBudget = c.PreviousBaselineBudget,
        PreviousBaselineSetAt  = c.PreviousBaselineSetAt,
        NewBaselineBudget      = c.NewBaselineBudget,
        MilestonesShifted      = c.MilestonesShifted,
        CreatedAt = c.CreatedAt,
    };
}
