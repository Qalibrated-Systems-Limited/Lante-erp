using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class WorkflowRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowTriggerEvent TriggerEvent { get; set; }
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int RunOrder { get; set; } = 0;
    public bool StopOnMatch { get; set; } = false;
}
