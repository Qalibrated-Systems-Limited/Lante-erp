namespace CrmService.Core.Entities;

/// <summary>P10 — PIPELINE_SNAPSHOT (CRM-045). Daily weighted-pipeline snapshot, retained for trend
/// analysis only (the live MD dashboard reads live data — no stale cache).</summary>
public class PipelineSnapshot : BaseEntity
{
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow.Date;
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int OpenCount { get; set; }
    public string? ByStageJson { get; set; }   // stage → {count, weighted}
}
