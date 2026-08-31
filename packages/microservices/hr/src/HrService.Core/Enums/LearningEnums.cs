namespace HrService.Core.Enums;

/// <summary>H7 (P22) — an LDP's life. Only an approved plan counts as filed for the 31 January deadline.</summary>
public enum LdpStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
}

/// <summary>H7 (P22) — an objective closes when a training event is logged against it.</summary>
public enum LdpObjectiveStatus
{
    Planned,
    InProgress,
    Completed,
    /// <summary>Deliberately dropped — kept so the year's plan still reads honestly.</summary>
    Abandoned,
}

/// <summary>H7 (P23/P25) — where a training event came from. Knowledge-sharing hours count towards the same
/// annual target as external courses (P25 step 25.3), so they are a source, not a separate ledger.</summary>
public enum TrainingSource
{
    External,
    Internal,
    KnowledgeSharing,
    OnTheJob,
}

/// <summary>
/// H7 (HR-DEC-5) — where the evidence for a mandatory training lives.
/// <para>HR owns the requirement but not the record: HSE completion is hse's, anti-bribery is compliance's.
/// Only <see cref="Hr"/> requirements are satisfied from HR's own training log, which today means Data
/// Protection — the one mandatory training with no other home.</para>
/// </summary>
public enum TrainingEvidenceSource
{
    /// <summary>HR's own training log.</summary>
    Hr,
    /// <summary>hse-service HseTrainingRecord.</summary>
    Hse,
    /// <summary>compliance-service AntiBriberyTraining.</summary>
    Compliance,
}

/// <summary>H7 (HR-029/034) — how one employee stands against one mandatory requirement.</summary>
public enum MandatoryTrainingState
{
    /// <summary>Completed and still in date.</summary>
    Valid,
    /// <summary>In date but inside the renewal window.</summary>
    DueSoon,
    /// <summary>Lapsed, still within the grace period.</summary>
    Overdue,
    /// <summary>Lapsed beyond the grace period — the HR-034 red flag.</summary>
    Breached,
    /// <summary>No completion on record at all.</summary>
    NeverCompleted,
    /// <summary>The evidence could not be read — a different thing from "not done", and never treated as one.</summary>
    Unknown,
}
