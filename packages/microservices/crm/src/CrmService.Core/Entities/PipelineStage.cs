using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P3 — PIPELINE_STAGE. Company-configurable sales-pipeline stages, each with a default win
/// probability used for weighted-pipeline forecasting. Seeded with the 7 standard stages per tenant
/// on first use. WinProbability is a fraction 0..1.
/// </summary>
public class PipelineStage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public decimal WinProbability { get; set; }   // 0.00 .. 1.00
    public PipelineStageType StageType { get; set; } = PipelineStageType.Open;
    public bool IsActive { get; set; } = true;
}
