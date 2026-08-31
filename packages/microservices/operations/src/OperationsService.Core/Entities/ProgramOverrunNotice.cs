namespace OperationsService.Core.Entities;

/// <summary>
/// O9 — PROGRAM_OVERRUN_NOTICE: raised by the background sweep when a project runs more than 14 days
/// past its expected end date without completing. One per project (guarded by Project.OverrunNoticedAt).
/// </summary>
public class ProgramOverrunNotice : BaseEntity
{
    public string   ProjectId       { get; set; } = string.Empty;
    public DateTime ExpectedEndDate { get; set; }
    public int      DaysOverrun     { get; set; }
    public string   Message         { get; set; } = string.Empty;
}
