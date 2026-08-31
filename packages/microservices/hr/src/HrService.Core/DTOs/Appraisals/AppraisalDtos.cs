namespace HrService.Core.DTOs.Appraisals;

/// <summary>The result shape every H9 write returns, matching H5–H8.</summary>
public record AppraisalActionResult(string Status, string Message, string? Id = null)
{
    public List<string> Warnings { get; init; } = [];
}

// ── Scorecards (P14) ──
public class KpiScorecardItemDto
{
    public string Id { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WeightPercent { get; set; }
    public string MeasurementType { get; set; } = string.Empty;
    public string TargetSource { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Set when the source is declared but nothing feeds it yet.</summary>
    public string? SourceNote { get; set; }
}

public class KpiScorecardDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public int Year { get; set; }
    public decimal PipThreshold { get; set; }
    public bool IsActive { get; set; }
    public List<KpiScorecardItemDto> Items { get; set; } = [];
    public decimal TotalWeight { get; set; }
    /// <summary>False until the active items total exactly 100 — a scorecard that does not is not usable.</summary>
    public bool WeightsValid { get; set; }
    public int EmployeesWithTargets { get; set; }
}

public class SaveKpiScorecardDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PositionId { get; set; }
    public int? Year { get; set; }
    public decimal? PipThreshold { get; set; }
    public bool? IsActive { get; set; }
    public List<SaveKpiScorecardItemDto> Items { get; set; } = [];
}

public class SaveKpiScorecardItemDto
{
    public string? Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WeightPercent { get; set; }
    /// <summary>Quantitative | Qualitative.</summary>
    public string MeasurementType { get; set; } = "Qualitative";
    /// <summary>Manual | Attendance | Feedback360 | Revenue.</summary>
    public string TargetSource { get; set; } = "Manual";
    public string? Unit { get; set; }
    public int DisplayOrder { get; set; }
}

// ── Targets (P14 step 14.4) ──
public class KpiTargetDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string KpiScorecardItemId { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int Year { get; set; }
    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }
    public string ActualSource { get; set; } = string.Empty;
    public DateTime? ActualUpdatedAt { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
}

public class SetTargetDto
{
    public string KpiTargetId { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string? Notes { get; set; }
}

// ── Cycles and appraisals (P15) ──
public class AppraisalCycleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CycleType { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int AppraisalCount { get; set; }
    public int Completed { get; set; }
    public string? Notes { get; set; }
}

public class OpenCycleDto
{
    public string? Name { get; set; }
    /// <summary>MidYear | EndOfYear.</summary>
    public string CycleType { get; set; } = "MidYear";
    public int? Year { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Notes { get; set; }
}

public class AppraisalItemScoreDto
{
    public string Id { get; set; } = string.Empty;
    public string? KpiScorecardItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public string MeasurementType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }
    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? Comments { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>"System-scored from attendance" — so nobody wonders why a box is read-only.</summary>
    public string? ScoringNote { get; set; }
}

public class AppraisalDto
{
    public string Id { get; set; } = string.Empty;
    public string AppraisalCycleId { get; set; } = string.Empty;
    public string? CycleName { get; set; }
    public int Year { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? PositionTitle { get; set; }
    public string? ScorecardName { get; set; }
    public decimal PipThreshold { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime? SelfAssessmentSubmittedAt { get; set; }
    public DateTime? LineManagerReviewedAt { get; set; }
    public DateTime? MdSignedOffAt { get; set; }
    public DateTime? HrRecordedAt { get; set; }

    public decimal? SelfScore { get; set; }
    public decimal? LineManagerScore { get; set; }
    public decimal? Feedback360Score { get; set; }
    public decimal? FinalScore { get; set; }

    public string? SelfComments { get; set; }
    public string? LineManagerComments { get; set; }
    public string? MdComments { get; set; }
    public string? HrComments { get; set; }
    public string? TrainingNeeds { get; set; }

    public bool PipTriggered { get; set; }
    public string? PipId { get; set; }
    public List<AppraisalItemScoreDto> ItemScores { get; set; } = [];
    /// <summary>Who the appraisal is waiting on, in words.</summary>
    public string AwaitingLabel { get; set; } = string.Empty;
}

public class SubmitAppraisalStepDto
{
    /// <summary>Per-item scores. Ignored for system-scored items.</summary>
    public List<ItemScoreInputDto> ItemScores { get; set; } = [];
    public string? Comments { get; set; }
    /// <summary>HR-020 — development needs, which the LDP can pick up.</summary>
    public string? TrainingNeeds { get; set; }
}

public class ItemScoreInputDto
{
    public string ItemScoreId { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    /// <summary>For quantitative manual items — the achieved figure.</summary>
    public decimal? ActualValue { get; set; }
    public string? Comments { get; set; }
}

// ── 360 feedback (P16) ──
public class Feedback360RequestDto
{
    public List<Reviewer360Dto> Reviewers { get; set; } = [];
}

public class Reviewer360Dto
{
    public string ReviewerEmployeeId { get; set; } = string.Empty;
    /// <summary>Peer | Subordinate | LineManager.</summary>
    public string ReviewerType { get; set; } = "Peer";
}

public class SubmitFeedback360Dto
{
    public decimal OverallScore { get; set; }
    public string? Comments { get; set; }
    public string? ScoresJson { get; set; }
}

/// <summary>
/// The 360 round as HR sees it. <b>No reviewer is ever paired with a score here</b> — individual ratings are
/// anonymous to the employee (P16), so the read model exposes response counts and averages only.
/// </summary>
public class Feedback360SummaryDto
{
    public string AppraisalId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int Requested { get; set; }
    public int Submitted { get; set; }
    public decimal? PeerAverage { get; set; }
    public decimal? SubordinateAverage { get; set; }
    public decimal? LineManagerScore { get; set; }
    public decimal? Aggregate { get; set; }
    /// <summary>Reviewers who have not responded — HR needs to chase them, and a non-response carries no
    /// score, so naming it reveals nothing about anybody's rating.</summary>
    public List<string> AwaitingResponse { get; set; } = [];
    public List<string> Comments { get; set; } = [];
}

// ── PIP (P17) ──
public class PipDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? AppraisalId { get; set; }
    public decimal TriggerScore { get; set; }
    public decimal Threshold { get; set; }
    public string? Objectives { get; set; }
    public string? SupportProvided { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ReviewDate { get; set; }
    public DateTime? SecondReviewDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? LinkedLdpObjectiveId { get; set; }
    public bool IsReviewDue { get; set; }
}

public class SavePipDto
{
    public string? Objectives { get; set; }
    public string? SupportProvided { get; set; }
    public DateTime? ReviewDate { get; set; }
    public DateTime? SecondReviewDate { get; set; }
}

public class ClosePipDto
{
    /// <summary>Completed | Extended | EscalatedToDisciplinary.</summary>
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    /// <summary>Required when extending.</summary>
    public DateTime? NewReviewDate { get; set; }
}

// ── Summary ──
public class AppraisalSummaryDto
{
    public int Year { get; set; }
    public int Scorecards { get; set; }
    public int ScorecardsWithInvalidWeights { get; set; }
    public int EmployeesWithTargets { get; set; }
    public int EmployeesWithoutScorecard { get; set; }

    public string? OpenCycleName { get; set; }
    public int Appraisals { get; set; }
    public int AwaitingSelf { get; set; }
    public int AwaitingLineManager { get; set; }
    public int AwaitingMd { get; set; }
    public int AwaitingHr { get; set; }
    public int Completed { get; set; }
    public decimal? AverageFinalScore { get; set; }

    public int Feedback360Outstanding { get; set; }
    public int ActivePips { get; set; }
    public int PipReviewsDue { get; set; }
}
