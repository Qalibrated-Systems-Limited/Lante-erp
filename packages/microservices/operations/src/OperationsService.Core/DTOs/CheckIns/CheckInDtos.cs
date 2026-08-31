namespace OperationsService.Core.DTOs.CheckIns;

public class CheckInDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }

    // PR2 — optional overrides; all three default from the assignment when omitted.
    public string? ProjectId { get; set; }
    public string? ProjectTaskId { get; set; }
    /// <summary>Defaults to true. Set false for travel, rework or warranty time.</summary>
    public bool? IsBillable { get; set; }
}

public class CheckOutDto
{
    public string CheckInId { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }

    /// <summary>PR2 — last chance to mark the visit non-billable, once how it went is known.</summary>
    public bool? IsBillable { get; set; }
}

public class CheckInReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double? CheckInLatitude { get; set; }
    public double? CheckInLongitude { get; set; }
    public double? CheckOutLatitude { get; set; }
    public double? CheckOutLongitude { get; set; }
    public string? Notes { get; set; }
    public DateTime CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public int? DurationMinutes { get; set; }

    // PR2 — what the time was against, and what the check-out captured.
    public string? ProjectId { get; set; }
    public string? ProjectTaskId { get; set; }
    public bool IsBillable { get; set; } = true;
    /// <summary>The timesheet line this check-in produced, if any.</summary>
    public string? TimesheetEntryId { get; set; }
}
