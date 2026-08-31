using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Tickets;

public class ChangeStatusDto
{
    public TicketStatus NewStatus { get; set; }
    public string? Notes { get; set; }
}

public class AssignTicketDto
{
    public string AssignedToUserId { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public string? DepartmentId { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; } = true;
}

public class ResolveTicketDto
{
    public string ResolutionNotes { get; set; } = string.Empty;
    /// D3-3 — root-cause analysis, mandatory to resolve (separate from ResolutionNotes).
    public string RootCause { get; set; } = string.Empty;
}

/// D3-1 — link an existing ticket as a child of this one.
public class LinkChildTicketDto
{
    public string ChildTicketId { get; set; } = string.Empty;
}

public class EscalateTicketDto
{
    public EscalationLevel EscalationLevel { get; set; }
    public string EscalatedToUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AssignToDepartmentDto
{
    public string DepartmentId { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class TicketFilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public string? CategoryId { get; set; }
    public string? DepartmentId { get; set; }
    public List<string>? DepartmentIds { get; set; }
    public string? AssignedToUserId { get; set; }
    public bool? AssignedToMe { get; set; }
    public bool? CreatedByMe { get; set; }
    public bool SortDescending { get; set; } = true;
}
