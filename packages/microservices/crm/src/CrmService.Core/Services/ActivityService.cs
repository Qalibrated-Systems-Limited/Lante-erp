using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Activity;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C7 (P7) — client interaction, tasks, visits, targets &amp; daily activity log.</summary>
public class ActivityService(
    IGenericRepository<CustomerInteraction> interactions,
    IGenericRepository<ActivityTask> tasks,
    IGenericRepository<ClientVisit> visits,
    IGenericRepository<VisitTarget> targets,
    IGenericRepository<SalesActivityLog> dailyLogs,
    IGenericRepository<Customer> customers,
    IMapper mapper) : IActivityService
{
    // ── Interactions ──
    public async Task<CustomerInteractionDto> LogInteractionAsync(string customerId, LogInteractionDto dto, string userId)
    {
        var customer = await customers.Query().FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new KeyNotFoundException($"Customer {customerId} not found.");
        var it = await interactions.CreateAsync(new CustomerInteraction
        {
            CustomerId = customerId, ContactId = dto.ContactId, InteractionType = dto.InteractionType,
            Subject = dto.Subject.Trim(), Description = dto.Description?.Trim(), Outcome = dto.Outcome?.Trim(),
            PerformedBy = userId, InteractionDate = DateTime.UtcNow, NextActionDate = dto.NextActionDate,
            CreatedBy = userId, UpdatedBy = userId,
        });
        // Logging an interaction refreshes the client and clears dormancy (P7).
        customer.LastInteractionAt = DateTime.UtcNow;
        customer.DormantSince = null;
        customer.UpdatedBy = userId; customer.UpdatedAt = DateTime.UtcNow;
        await customers.UpdateAsync(customer);
        return mapper.Map<CustomerInteractionDto>(it);
    }

    public async Task<List<CustomerInteractionDto>> GetInteractionsAsync(string customerId)
    {
        var list = await interactions.Query().AsNoTracking().Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InteractionDate).ToListAsync();
        return mapper.Map<List<CustomerInteractionDto>>(list);
    }

    // ── Tasks ──
    private static bool IsOverdue(ActivityTask t) => t.Status == ActivityTaskStatus.Open && t.DueDate < DateTime.UtcNow;

    public async Task<TaskListResult> GetTasksAsync(TaskFilterParams filter)
    {
        var q = tasks.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo)) q = q.Where(t => t.AssignedTo == filter.AssignedTo);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) q = q.Where(t => t.CustomerId == filter.CustomerId);
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<ActivityTaskStatus>(filter.Status, true, out var st))
            q = q.Where(t => t.Status == st);
        var all = await q.OrderBy(t => t.DueDate).ToListAsync();
        if (filter.Overdue == true) all = all.Where(IsOverdue).ToList();
        var open = all.Where(t => t.Status == ActivityTaskStatus.Open).ToList();
        var items = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).Select(ToTaskDto).ToList();
        return new TaskListResult(items, all.Count, open.Count, open.Count(IsOverdue));
    }

    public async Task<ActivityTaskDto> CreateTaskAsync(CreateTaskDto dto, string userId)
    {
        var t = await tasks.CreateAsync(new ActivityTask
        {
            CustomerId = dto.CustomerId, OpportunityId = dto.OpportunityId,
            AssignedTo = string.IsNullOrWhiteSpace(dto.AssignedTo) ? userId : dto.AssignedTo,
            TaskType = string.IsNullOrWhiteSpace(dto.TaskType) ? "FollowUp" : dto.TaskType.Trim(),
            Subject = dto.Subject.Trim(), DueDate = dto.DueDate, Status = ActivityTaskStatus.Open,
            CreatedBy = userId, UpdatedBy = userId,
        });
        return ToTaskDto(t);
    }

    public async Task<ActivityTaskDto> CompleteTaskAsync(string id, string userId)
    {
        var t = await FindTask(id);
        t.Status = ActivityTaskStatus.Done; t.CompletedAt = DateTime.UtcNow;
        Touch(t, userId); await tasks.UpdateAsync(t);
        return ToTaskDto(t);
    }

    public async Task<ActivityTaskDto> CancelTaskAsync(string id, string userId)
    {
        var t = await FindTask(id);
        t.Status = ActivityTaskStatus.Cancelled; Touch(t, userId); await tasks.UpdateAsync(t);
        return ToTaskDto(t);
    }

    // ── Visits ──
    public async Task<ClientVisitDto> LogVisitAsync(LogVisitDto dto, string userId)
    {
        var v = await visits.CreateAsync(new ClientVisit
        {
            EmployeeId = string.IsNullOrWhiteSpace(dto.EmployeeId) ? userId : dto.EmployeeId,
            CustomerId = dto.CustomerId, VisitDate = dto.VisitDate ?? DateTime.UtcNow,
            Purpose = dto.Purpose.Trim(), Outcome = dto.Outcome?.Trim(), NextAction = dto.NextAction?.Trim(),
            GpsLat = dto.GpsLat, GpsLng = dto.GpsLng, CreatedBy = userId, UpdatedBy = userId,
        });
        return mapper.Map<ClientVisitDto>(v);
    }

    public async Task<List<ClientVisitDto>> GetVisitsAsync(string? customerId, string? employeeId)
    {
        var q = visits.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(customerId)) q = q.Where(v => v.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(v => v.EmployeeId == employeeId);
        return mapper.Map<List<ClientVisitDto>>(await q.OrderByDescending(v => v.VisitDate).ToListAsync());
    }

    // ── Visit targets ──
    public async Task<List<VisitTargetDto>> GetVisitTargetsAsync()
        => mapper.Map<List<VisitTargetDto>>(await targets.Query().AsNoTracking().ToListAsync());

    public async Task<VisitTargetDto> SaveVisitTargetAsync(SaveVisitTargetDto dto, string userId)
    {
        var t = await targets.Query().FirstOrDefaultAsync(x => x.EmployeeId == dto.EmployeeId);
        var isNew = t is null;
        t ??= new VisitTarget { EmployeeId = dto.EmployeeId, CreatedBy = userId };
        t.EmployeeName = dto.EmployeeName; t.MinVisitsPerWeek = dto.MinVisitsPerWeek; t.MinVisitsPerMonth = dto.MinVisitsPerMonth;
        Touch(t, userId);
        if (isNew) await targets.CreateAsync(t); else await targets.UpdateAsync(t);
        return mapper.Map<VisitTargetDto>(t);
    }

    // ── Daily activity log ──
    public async Task<SalesActivityLogDto> LogDailyActivityAsync(LogDailyActivityDto dto, string userId)
    {
        var emp = string.IsNullOrWhiteSpace(dto.EmployeeId) ? userId : dto.EmployeeId;
        var day = (dto.LogDate ?? DateTime.UtcNow).Date;
        var log = await dailyLogs.Query().FirstOrDefaultAsync(l => l.EmployeeId == emp && l.LogDate == day);
        var isNew = log is null;
        log ??= new SalesActivityLog { EmployeeId = emp, LogDate = day, CreatedBy = userId };
        log.EmployeeName = dto.EmployeeName; log.CallsMade = dto.CallsMade; log.MeetingsHeld = dto.MeetingsHeld;
        log.ProposalsSent = dto.ProposalsSent; log.Visits = dto.Visits; log.Notes = dto.Notes?.Trim();
        Touch(log, userId);
        if (isNew) await dailyLogs.CreateAsync(log); else await dailyLogs.UpdateAsync(log);
        return mapper.Map<SalesActivityLogDto>(log);
    }

    public async Task<List<SalesActivityLogDto>> GetDailyActivityAsync(string employeeId, DateTime? from, DateTime? to)
    {
        var q = dailyLogs.Query().AsNoTracking().Where(l => l.EmployeeId == employeeId);
        if (from.HasValue) q = q.Where(l => l.LogDate >= from.Value.Date);
        if (to.HasValue) q = q.Where(l => l.LogDate <= to.Value.Date);
        return mapper.Map<List<SalesActivityLogDto>>(await q.OrderByDescending(l => l.LogDate).ToListAsync());
    }

    // ── Helpers ──
    private ActivityTaskDto ToTaskDto(ActivityTask t)
    {
        var dto = mapper.Map<ActivityTaskDto>(t);
        dto.IsOverdue = IsOverdue(t);
        return dto;
    }
    private async Task<ActivityTask> FindTask(string id) =>
        await tasks.Query().FirstOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException($"Task {id} not found.");
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
