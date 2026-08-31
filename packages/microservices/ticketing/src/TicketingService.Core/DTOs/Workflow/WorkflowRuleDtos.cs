using TicketingService.Core.Enums;
using TicketingService.Core.Models;

namespace TicketingService.Core.DTOs.Workflow;

public class CreateWorkflowRuleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowTriggerEvent TriggerEvent { get; set; }
    public List<WorkflowCondition> Conditions { get; set; } = new();
    public List<WorkflowAction> Actions { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public int RunOrder { get; set; } = 0;
    public bool StopOnMatch { get; set; } = false;
}

public class UpdateWorkflowRuleDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<WorkflowCondition>? Conditions { get; set; }
    public List<WorkflowAction>? Actions { get; set; }
    public bool? IsActive { get; set; }
    public int? RunOrder { get; set; }
    public bool? StopOnMatch { get; set; }
}

public class WorkflowRuleReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkflowTriggerEvent TriggerEvent { get; set; }
    public List<WorkflowCondition> Conditions { get; set; } = new();
    public List<WorkflowAction> Actions { get; set; } = new();
    public bool IsActive { get; set; }
    public int RunOrder { get; set; }
    public bool StopOnMatch { get; set; }
    public DateTime CreatedAt { get; set; }
}
