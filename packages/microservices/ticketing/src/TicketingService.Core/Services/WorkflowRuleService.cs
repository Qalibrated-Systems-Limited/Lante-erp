using System.Text.Json;
using TicketingService.Core.DTOs.Workflow;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;
using TicketingService.Core.Models;

namespace TicketingService.Core.Services;

public class WorkflowRuleService(IWorkflowRuleRepository repository) : IWorkflowRuleService
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

    public async Task<IEnumerable<WorkflowRuleReadDto>> GetAllAsync()
    {
        var rules = await repository.GetAllAsync();
        return rules.Select(ToReadDto);
    }

    public async Task<WorkflowRuleReadDto?> GetByIdAsync(string id)
    {
        var rule = await repository.GetByIdAsync(id);
        return rule == null ? null : ToReadDto(rule);
    }

    public async Task<WorkflowRuleReadDto> CreateAsync(CreateWorkflowRuleDto dto, string createdByUserId)
    {
        var rule = new WorkflowRule
        {
            Name = dto.Name,
            Description = dto.Description,
            TriggerEvent = dto.TriggerEvent,
            ConditionsJson = JsonSerializer.Serialize(dto.Conditions),
            ActionsJson = JsonSerializer.Serialize(dto.Actions),
            IsActive = dto.IsActive,
            RunOrder = dto.RunOrder,
            StopOnMatch = dto.StopOnMatch,
            CreatedBy = createdByUserId,
            UpdatedBy = createdByUserId
        };

        var created = await repository.CreateAsync(rule);
        return ToReadDto(created);
    }

    public async Task<WorkflowRuleReadDto> UpdateAsync(string id, UpdateWorkflowRuleDto dto, string updatedByUserId)
    {
        var rule = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Workflow rule {id} not found.");

        if (dto.Name != null) rule.Name = dto.Name;
        if (dto.Description != null) rule.Description = dto.Description;
        if (dto.Conditions != null) rule.ConditionsJson = JsonSerializer.Serialize(dto.Conditions);
        if (dto.Actions != null) rule.ActionsJson = JsonSerializer.Serialize(dto.Actions);
        if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;
        if (dto.RunOrder.HasValue) rule.RunOrder = dto.RunOrder.Value;
        if (dto.StopOnMatch.HasValue) rule.StopOnMatch = dto.StopOnMatch.Value;
        rule.UpdatedBy = updatedByUserId;

        var updated = await repository.UpdateAsync(rule);
        return ToReadDto(updated);
    }

    public Task<bool> DeleteAsync(string id) => repository.DeleteAsync(id);

    public async Task SetActiveAsync(string id, bool isActive, string updatedByUserId)
    {
        var rule = await repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Workflow rule {id} not found.");

        rule.IsActive = isActive;
        rule.UpdatedBy = updatedByUserId;
        await repository.UpdateAsync(rule);
    }

    private static WorkflowRuleReadDto ToReadDto(WorkflowRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Description = rule.Description,
        TriggerEvent = rule.TriggerEvent,
        Conditions = string.IsNullOrWhiteSpace(rule.ConditionsJson) ? [] : JsonSerializer.Deserialize<List<WorkflowCondition>>(rule.ConditionsJson, JsonOptions) ?? [],
        Actions    = string.IsNullOrWhiteSpace(rule.ActionsJson)    ? [] : JsonSerializer.Deserialize<List<WorkflowAction>>(rule.ActionsJson, JsonOptions) ?? [],
        IsActive = rule.IsActive,
        RunOrder = rule.RunOrder,
        StopOnMatch = rule.StopOnMatch,
        CreatedAt = rule.CreatedAt
    };
}
