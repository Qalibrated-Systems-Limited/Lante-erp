using System.Text.Json.Serialization;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Models;

public class WorkflowCondition
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("op")]
    public WorkflowConditionOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class WorkflowAction
{
    public WorkflowActionType Type { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}
