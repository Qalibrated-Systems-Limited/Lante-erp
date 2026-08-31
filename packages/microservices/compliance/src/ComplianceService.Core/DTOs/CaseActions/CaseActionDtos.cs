using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.CaseActions;

public class CaseActionReadDto
{
    public string Id { get; set; } = string.Empty;
    public CaseActionParentType ParentType { get; set; }
    public string ParentId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string? LoggedByName { get; set; }
    public DateTime LoggedAt { get; set; }
}

public class CreateCaseActionDto
{
    public string Note { get; set; } = string.Empty;
}
