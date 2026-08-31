namespace CrmService.Core.Enums;

// P3 — opportunity outcome. Open moves through the pipeline stages; Won triggers deal close (P6/C5);
// Lost requires a reason + winning competitor.
public enum OpportunityStatus
{
    Open,
    Won,
    Lost,
}

// Pipeline stages are company-configurable (CRM-suggestion). Type distinguishes the terminal
// Won/Lost stages from the open working stages for weighted-pipeline math.
public enum PipelineStageType
{
    Open,
    Won,
    Lost,
}
