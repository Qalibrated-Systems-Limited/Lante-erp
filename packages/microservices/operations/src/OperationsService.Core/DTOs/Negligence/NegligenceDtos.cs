using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Negligence;

public class ReportNegligenceDto
{
    public string  EmployeeId   { get; set; } = string.Empty;
    public string  EmployeeName { get; set; } = string.Empty;
    public string? ProjectId    { get; set; }
    public string? AssignmentId { get; set; }
    public string  Title        { get; set; } = string.Empty;
    public string  Description  { get; set; } = string.Empty;
    public NegligenceSeverity Severity { get; set; } = NegligenceSeverity.Minor;
    public DateTime OccurredAt  { get; set; }
}

public class RespondNegligenceDto
{
    public string  ResponderRole { get; set; } = string.Empty;   // HR | DepartmentHead | MD
    public string  ResponseText  { get; set; } = string.Empty;
    public string? ActionTaken   { get; set; }
    public decimal? PayrollDeductionAmount { get; set; }
    public bool    CloseIncident { get; set; }
}

public class NegligenceResponseReadDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string ResponderId { get; set; } = string.Empty;
    public string ResponderName { get; set; } = string.Empty;
    public string ResponderRole { get; set; } = string.Empty;
    public string ResponseText { get; set; } = string.Empty;
    public string? ActionTaken { get; set; }
    public decimal? PayrollDeductionAmount { get; set; }
    public DateTime RespondedAt { get; set; }
}

public class NegligenceIncidentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string IncidentNumber { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? AssignmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime ReportedAt { get; set; }
    public string ReportedBy { get; set; } = string.Empty;
    public bool LoggedLate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ResponseDeadline { get; set; }
    public DateTime? EscalatedToMdAt { get; set; }
    public bool IsRepeatOffense { get; set; }
    public bool FinalWarningIssued { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<NegligenceResponseReadDto> Responses { get; set; } = new();
}
