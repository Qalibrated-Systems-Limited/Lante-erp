using System.Text.Json;
using System.Text.Json.Serialization;
using TicketingService.Core.Enums;

namespace TicketingService.Core.Models;

public class WorkflowActionTypeConverter : JsonConverter<WorkflowActionType>
{
    public override WorkflowActionType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? string.Empty;
        return raw.ToLowerInvariant() switch
        {
            "assigntouser"               => WorkflowActionType.AssignToUser,
            "assigntodepartment"         => WorkflowActionType.AssignToDepartment,
            "changestatus"               => WorkflowActionType.ChangeStatus,
            "addtag"                     => WorkflowActionType.AddTag,
            "removetag"                  => WorkflowActionType.RemoveTag,
            "sendnotification"           => WorkflowActionType.SendNotification,
            "addcomment"                 => WorkflowActionType.AddComment,
            "escalate" or "escalateto"   => WorkflowActionType.EscalateTo,
            "createtechnicianassignment" => WorkflowActionType.CreateTechnicianAssignment,
            "createfleettrip"            => WorkflowActionType.CreateFleetTrip,
            _ => throw new JsonException($"Unknown WorkflowActionType: '{raw}'"),
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkflowActionType value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            WorkflowActionType.AssignToUser               => "assignToUser",
            WorkflowActionType.AssignToDepartment         => "assignToDepartment",
            WorkflowActionType.ChangeStatus               => "changeStatus",
            WorkflowActionType.AddTag                     => "addTag",
            WorkflowActionType.RemoveTag                  => "removeTag",
            WorkflowActionType.SendNotification           => "sendNotification",
            WorkflowActionType.AddComment                 => "addComment",
            WorkflowActionType.EscalateTo                 => "escalate",
            WorkflowActionType.CreateTechnicianAssignment => "createTechnicianAssignment",
            WorkflowActionType.CreateFleetTrip            => "createFleetTrip",
            _ => value.ToString(),
        };
        writer.WriteStringValue(str);
    }
}

public class WorkflowConditionOperatorConverter : JsonConverter<WorkflowConditionOperator>
{
    public override WorkflowConditionOperator Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString() ?? string.Empty;
        return raw.ToLowerInvariant() switch
        {
            "eq" or "equals"        => WorkflowConditionOperator.Equals,
            "neq" or "ne" or "notequals" => WorkflowConditionOperator.NotEquals,
            "contains"              => WorkflowConditionOperator.Contains,
            "isempty"               => WorkflowConditionOperator.IsEmpty,
            "isnotempty"            => WorkflowConditionOperator.IsNotEmpty,
            "gt" or "greaterthan"   => WorkflowConditionOperator.GreaterThan,
            "lt" or "lessthan"      => WorkflowConditionOperator.LessThan,
            _ => throw new JsonException($"Unknown WorkflowConditionOperator: '{raw}'"),
        };
    }

    public override void Write(Utf8JsonWriter writer, WorkflowConditionOperator value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            WorkflowConditionOperator.Equals      => "eq",
            WorkflowConditionOperator.NotEquals   => "neq",
            WorkflowConditionOperator.Contains    => "contains",
            WorkflowConditionOperator.IsEmpty     => "isEmpty",
            WorkflowConditionOperator.IsNotEmpty  => "isNotEmpty",
            WorkflowConditionOperator.GreaterThan => "gt",
            WorkflowConditionOperator.LessThan    => "lt",
            _ => value.ToString(),
        };
        writer.WriteStringValue(str);
    }
}
