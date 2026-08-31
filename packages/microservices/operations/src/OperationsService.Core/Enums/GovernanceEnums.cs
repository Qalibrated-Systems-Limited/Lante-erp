namespace OperationsService.Core.Enums;

// PR3 — RAID + change control.
//
// Likelihood and impact are ordered scales, not labels: the register's whole value is being able to
// rank what to worry about first, and that needs the ordering to be a property of the type rather
// than something each caller re-derives from a string.

public enum RiskLikelihood { Low = 1, Medium = 2, High = 3 }

public enum RiskImpact { Low = 1, Medium = 2, High = 3 }

/// <summary>
/// <c>Realised</c> is the one that matters: a risk that happened is no longer a risk, and closing it
/// silently would lose the link to the issue it became. See <c>RiskEntry.RealisedAsIssueId</c>.
/// </summary>
public enum RiskStatus { Open, Mitigating, Mitigated, Closed, Realised }

public enum IssueSeverity { Low, Medium, High, Critical }

public enum IssueStatus { Open, InProgress, Resolved, Closed }

/// <summary>
/// A change request is the only sanctioned way to move an approved baseline. <c>Withdrawn</c> exists
/// so an abandoned request stays on the record — deleting it would erase the fact that a change was
/// once proposed and dropped, which is exactly the sort of thing an audit asks about.
/// </summary>
public enum ChangeRequestStatus { Draft, Submitted, Approved, Rejected, Withdrawn }

/// <summary>
/// PR3b — what a comment is attached to. Deliberately a closed set: comments are a collaboration
/// feature on the work breakdown, not a general-purpose annotation store bolted onto every entity.
/// </summary>
public enum CommentTargetType { Project, Milestone, Task }

/// <summary>
/// PR4b — recurrence cycle for standing work. Month-based rather than day-based so a quarterly visit
/// stays on the same day of the month instead of drifting by a day or two each cycle.
/// </summary>
public enum RecurrenceFrequency { Monthly, Quarterly, SemiAnnually, Annually }
