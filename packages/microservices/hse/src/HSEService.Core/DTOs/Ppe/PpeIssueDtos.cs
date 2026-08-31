using HSEService.Core.DTOs.Common;
using HSEService.Core.Enums;

namespace HSEService.Core.DTOs.Ppe;

public class PpeIssueReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Item { get; set; } = string.Empty;
    public PpeCondition Condition { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? ReplacementDueAt { get; set; }
}

public class CreatePpeIssueDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string Item { get; set; } = string.Empty;
    public PpeCondition Condition { get; set; } = PpeCondition.New;
    public DateTime? ReplacementDueAt { get; set; }
}

public class UpdatePpeIssueDto
{
    public PpeCondition Condition { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? ReplacementDueAt { get; set; }
}

public class PpeIssueFilterParameters : PaginationParameters
{
    public string? EmployeeUserId { get; set; }
}
