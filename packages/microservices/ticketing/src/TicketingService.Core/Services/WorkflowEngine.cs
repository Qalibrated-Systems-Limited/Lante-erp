using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Models;

namespace TicketingService.Core.Services;

public class WorkflowEngine(
    IWorkflowRuleRepository ruleRepository,
    ITicketRepository ticketRepository,
    ITicketCommentRepository commentRepository,
    ITagRepository tagRepository,
    ITicketHistoryService historyService,
    INotificationService notificationService,
    ITicketAssignmentClient ticketAssignmentClient,
    IFleetServiceClient fleetServiceClient,
    ITicketTransitionService transitionService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<WorkflowEngine> logger) : IWorkflowEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new WorkflowActionTypeConverter(),
            new WorkflowConditionOperatorConverter(),
        },
    };

    public async Task TriggerAsync(WorkflowTriggerEvent triggerEvent, Ticket ticket, string triggeredByUserId)
    {
        var rules = (await ruleRepository.GetActiveByTriggerEventAsync(triggerEvent))
            .OrderBy(r => r.RunOrder)
            .ToList();

        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            var conditions = string.IsNullOrWhiteSpace(rule.ConditionsJson) ? [] : JsonSerializer.Deserialize<List<WorkflowCondition>>(rule.ConditionsJson, JsonOptions) ?? [];

            if (!EvaluateConditions(conditions, ticket)) continue;

            logger.LogInformation("Workflow rule '{Rule}' matched ticket {TicketId} on event {Event}",
                rule.Name, ticket.Id, triggerEvent);

            var actions = string.IsNullOrWhiteSpace(rule.ActionsJson) ? [] : JsonSerializer.Deserialize<List<WorkflowAction>>(rule.ActionsJson, JsonOptions) ?? [];
            await ExecuteActionsAsync(actions, ticket, triggeredByUserId, rule.Name);

            if (rule.StopOnMatch) break;
        }
    }

    private static bool EvaluateConditions(List<WorkflowCondition> conditions, Ticket ticket) =>
        conditions.Count == 0 || conditions.All(c => EvaluateCondition(c, ticket));

    private static bool EvaluateCondition(WorkflowCondition condition, Ticket ticket)
    {
        var fieldValue = GetFieldValue(condition.Field, ticket);

        return condition.Operator switch
        {
            WorkflowConditionOperator.Equals =>
                string.Equals(fieldValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            WorkflowConditionOperator.NotEquals =>
                !string.Equals(fieldValue, condition.Value, StringComparison.OrdinalIgnoreCase),
            WorkflowConditionOperator.Contains =>
                fieldValue?.Contains(condition.Value, StringComparison.OrdinalIgnoreCase) ?? false,
            WorkflowConditionOperator.IsEmpty => string.IsNullOrEmpty(fieldValue),
            WorkflowConditionOperator.IsNotEmpty => !string.IsNullOrEmpty(fieldValue),
            WorkflowConditionOperator.GreaterThan =>
                double.TryParse(fieldValue, out var fv) && double.TryParse(condition.Value, out var cv) && fv > cv,
            WorkflowConditionOperator.LessThan =>
                double.TryParse(fieldValue, out var fv2) && double.TryParse(condition.Value, out var cv2) && fv2 < cv2,
            _ => false
        };
    }

    private static string? GetFieldValue(string field, Ticket ticket) => field.ToLowerInvariant() switch
    {
        "priority" => ticket.Priority.ToString(),
        "status" => ticket.Status.ToString(),
        "categoryid" => ticket.CategoryId,
        "departmentid" => ticket.DepartmentId,
        "assignedtouserid" => ticket.AssignedToUserId,
        "isescalated" => ticket.IsEscalated.ToString(),
        "source" => ticket.Source.ToString(),
        "linkedentitytype" => ticket.LinkedEntityType.ToString(),
        _ => null
    };

    private async Task ExecuteActionsAsync(List<WorkflowAction> actions, Ticket ticket, string triggeredByUserId, string ruleName)
    {
        foreach (var action in actions)
        {
            try
            {
                await ExecuteActionAsync(action, ticket, triggeredByUserId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Workflow action {ActionType} failed for rule '{Rule}' on ticket {TicketId}",
                    action.Type, ruleName, ticket.Id);
            }
        }
    }

    private async Task ExecuteActionAsync(WorkflowAction action, Ticket ticket, string triggeredByUserId)
    {
        switch (action.Type)
        {
            case WorkflowActionType.AssignToUser:
                if (action.Parameters.TryGetValue("userId", out var userId))
                {
                    ticket.AssignedToUserId = userId;
                    if (ticket.Status == TicketStatus.New) ticket.Status = TicketStatus.Assigned;
                    ticket.UpdatedBy = "workflow";
                    await ticketRepository.UpdateAsync(ticket);
                    await historyService.AppendAsync(ticket.Id, "workflow", "Assigned", null, userId, "Auto-assigned by workflow rule");
                    await notificationService.NotifyAssignedAsync(ticket, userId, triggeredByUserId);
                }
                break;

            case WorkflowActionType.AssignToDepartment:
                if (action.Parameters.TryGetValue("departmentId", out var deptId))
                {
                    ticket.DepartmentId = deptId;
                    ticket.UpdatedBy = "workflow";
                    await ticketRepository.UpdateAsync(ticket);
                    await historyService.AppendAsync(ticket.Id, "workflow", "DepartmentAssigned", null, deptId, "Auto-assigned to department by workflow rule");
                }
                break;

            case WorkflowActionType.ChangeStatus:
                if (action.Parameters.TryGetValue("status", out var statusStr) &&
                    Enum.TryParse<TicketStatus>(statusStr, true, out var newStatus))
                {
                    var oldStatus = await transitionService.TransitionAsync(
                        ticket, newStatus, TransitionSource.Workflow, "workflow",
                        "Status changed by workflow rule", "StatusChanged");
                    await notificationService.NotifyStatusChangedAsync(ticket, oldStatus.ToString(), newStatus.ToString(), "workflow");
                }
                break;

            case WorkflowActionType.AddTag:
                if (action.Parameters.TryGetValue("tagId", out var tagId))
                {
                    var exists = await tagRepository.GetTicketTagAsync(ticket.Id, tagId);
                    if (exists == null)
                    {
                        await tagRepository.AddTagToTicketAsync(new TicketTag
                        {
                            TicketId = ticket.Id,
                            TagId = tagId,
                            AddedByUserId = "workflow",
                            CreatedBy = "workflow"
                        });
                    }
                }
                break;

            case WorkflowActionType.RemoveTag:
                if (action.Parameters.TryGetValue("tagId", out var removeTagId))
                    await tagRepository.RemoveTagFromTicketAsync(ticket.Id, removeTagId);
                break;

            case WorkflowActionType.AddComment:
                if (action.Parameters.TryGetValue("content", out var content))
                {
                    var isInternal = action.Parameters.TryGetValue("isInternal", out var isInternalStr)
                        && string.Equals(isInternalStr, "true", StringComparison.OrdinalIgnoreCase);

                    var comment = new TicketComment
                    {
                        TicketId = ticket.Id,
                        AuthorUserId = "workflow",
                        Content = content,
                        IsInternal = isInternal,
                        CreatedBy = "workflow"
                    };
                    await commentRepository.CreateAsync(comment);
                    await historyService.AppendAsync(ticket.Id, "workflow", "Commented", null, null,
                        isInternal ? "Internal note added by workflow" : "Comment added by workflow");
                }
                break;

            case WorkflowActionType.SendNotification:
                if (action.Parameters.TryGetValue("recipientUserId", out var recipientId) &&
                    action.Parameters.TryGetValue("message", out var message))
                {
                    await notificationService.SendAsync(recipientId, "WorkflowNotification", message, ticket.Id);
                }
                break;

            case WorkflowActionType.EscalateTo:
                if (action.Parameters.TryGetValue("userId", out var escalateToUserId))
                {
                    var reason = action.Parameters.TryGetValue("reason", out var r) ? r : "Auto-escalated by workflow rule";
                    ticket.IsEscalated = true;
                    await transitionService.TransitionAsync(
                        ticket, TicketStatus.Escalated, TransitionSource.Workflow, "workflow", reason, "Escalated");
                    await notificationService.NotifyEscalatedAsync(ticket, "workflow");
                }
                break;

            case WorkflowActionType.CreateTechnicianAssignment:
            {
                var managerUserId = action.Parameters.TryGetValue("managerUserId", out var mgr)
                    ? mgr
                    : (ticket.AssignedToUserId ?? triggeredByUserId);

                var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization
                    .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                var assignmentId = await ticketAssignmentClient.CreateAssignmentFromTicketAsync(ticket, managerUserId, token);

                if (assignmentId != null)
                {
                    await historyService.AppendAsync(ticket.Id, "workflow", "TechnicianAssignmentCreated",
                        null, assignmentId, $"Technician assignment created by workflow rule");
                }
                break;
            }

            case WorkflowActionType.CreateFleetTrip:
            {
                var requestedByUserId = action.Parameters.TryGetValue("requestedByUserId", out var reqBy)
                    ? reqBy
                    : (ticket.AssignedToUserId ?? triggeredByUserId);

                var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization
                    .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

                var tripId = await fleetServiceClient.CreateTripFromTicketAsync(ticket, requestedByUserId, token);

                if (tripId != null)
                {
                    await historyService.AppendAsync(ticket.Id, "workflow", "FleetTripCreated",
                        null, tripId, $"Fleet trip created by workflow rule");
                }
                break;
            }
        }
    }
}
