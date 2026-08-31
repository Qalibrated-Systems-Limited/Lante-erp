using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Assignments;

public class CreateAssignmentDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentSourceType SourceType { get; set; } = AssignmentSourceType.Standalone;
    public string? LinkedTicketId { get; set; }
    public string? LinkedProjectTaskId { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string? DepartmentType { get; set; }
    public AssignmentPriority Priority { get; set; } = AssignmentPriority.Normal;
    public NatureOfVisit NatureOfVisit { get; set; }
    public string? ServiceType { get; set; }
    public DateTime? Deadline { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string? Notes { get; set; }
    public List<string> TechnicianIds { get; set; } = [];
    public List<string> TechnicianNames { get; set; } = [];
    public string? ServiceRequestId       { get; set; }
    public string? ServiceRequestDataJson { get; set; }
}

public class UpdateAssignmentDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public AssignmentPriority? Priority { get; set; }
    public NatureOfVisit? NatureOfVisit { get; set; }
    public string? ServiceType { get; set; }
    public DateTime? Deadline { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string? Notes { get; set; }
}

public class AssignmentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? LinkedTicketId { get; set; }
    public string? LinkedProjectTaskId { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public string DepartmentType { get; set; } = string.Empty;
    public string ManagerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string NatureOfVisit { get; set; } = string.Empty;
    public string? ServiceType { get; set; }
    public DateTime? Deadline { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string? Notes { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LinkedProjectId { get; set; }
    public string? LinkedMilestoneId { get; set; }
    public DateTime? LinkedAt { get; set; }
    public string? LinkedBy { get; set; }
    public string? ArchiveReason { get; set; }
    public string? ServiceRequestId       { get; set; }
    public string? ServiceRequestDataJson { get; set; }
    public List<AssignedTechnicianDto> Technicians { get; set; } = [];
}

public class AssignedTechnicianDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public class AssignmentFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public string? Search { get; set; }
    public string? DepartmentId { get; set; }
    public List<string>? DepartmentIds { get; set; }
    public string? TechnicianId { get; set; }
    public string? ManagerId { get; set; }
    public int? Status { get; set; }
    public int? SourceType { get; set; }
    public bool SortDescending { get; set; } = true;
    public bool? AwaitingLinkOnly { get; set; }
}

public class CancelAssignmentDto
{
    public string Reason { get; set; } = string.Empty;
}

public class LinkToProjectDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string? MilestoneId { get; set; }
}

public class ArchiveStandaloneDto
{
    public string Reason { get; set; } = string.Empty;
}
