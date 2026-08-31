using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class CheckIn : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    public DateTime? CheckOutTime { get; set; }
    public CheckInStatus Status { get; set; } = CheckInStatus.OnSite;
    public string? Notes { get; set; }

    // PR2 — the check-in is the timesheet's source of truth for field time, so it has to say what the
    // time was spent ON. The assignment link above is kept alongside rather than replaced: an
    // assignment may exist without a project task, and the field app still navigates by assignment.
    /// <summary>Project task this time is against. Defaults from the assignment's linked task.</summary>
    public string? ProjectTaskId { get; set; }
    /// <summary>Project the derived timesheet line posts to. Defaults from the assignment's project.</summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Whether the time is chargeable to the client. Travel, rework and warranty visits are worked
    /// hours that must still be costed, but must not reach an invoice — so this is carried on the
    /// time itself rather than inferred later from the visit type.
    /// </summary>
    public bool IsBillable { get; set; } = true;

    /// <summary>Set once the check-out has produced a timesheet line, so a re-run cannot double-post.</summary>
    public string? TimesheetEntryId { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
