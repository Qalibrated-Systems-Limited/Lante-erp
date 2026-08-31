using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Negligence;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O9 — see <see cref="INegligenceService"/>.</summary>
public class NegligenceService(
    IGenericRepository<NegligenceIncident> incidents,
    IGenericRepository<NegligenceResponse> responses,
    IHrGateway hr,
    IMapper mapper) : INegligenceService
{
    public async Task<NegligenceIncidentReadDto?> GetByIdAsync(string id)
    {
        var i = await incidents.Query()
            .Include(x => x.Responses.Where(r => !r.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return i is null ? null : mapper.Map<NegligenceIncidentReadDto>(i);
    }

    public async Task<PaginatedResult<NegligenceIncidentReadDto>> GetAllAsync(int page, int pageSize, string? employeeId, string? status)
    {
        var query = incidents.Query().Include(x => x.Responses.Where(r => !r.IsDeleted)).Where(x => !x.IsDeleted);
        if (!string.IsNullOrEmpty(employeeId)) query = query.Where(x => x.EmployeeId == employeeId);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<NegligenceStatus>(status, true, out var st))
            query = query.Where(x => x.Status == st);
        query = query.OrderByDescending(x => x.ReportedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PaginatedResult<NegligenceIncidentReadDto>
        {
            Items = mapper.Map<List<NegligenceIncidentReadDto>>(items),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<NegligenceIncidentReadDto> ReportAsync(ReportNegligenceDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.EmployeeId))
            throw new InvalidOperationException("The employee involved is required.");

        var now = DateTime.UtcNow;
        var occurred = dto.OccurredAt == default ? now : dto.OccurredAt;

        // Repeat offense: another incident for this employee within the last 12 months.
        var since = now.AddMonths(-12);
        var prior = await incidents.Query()
            .CountAsync(x => x.EmployeeId == dto.EmployeeId && !x.IsDeleted && x.OccurredAt >= since);
        var isRepeat = prior >= 1;

        var incident = await incidents.CreateAsync(new NegligenceIncident
        {
            IncidentNumber   = await GenerateNumberAsync(),
            EmployeeId       = dto.EmployeeId,
            EmployeeName     = dto.EmployeeName,
            ProjectId        = dto.ProjectId,
            AssignmentId     = dto.AssignmentId,
            Title            = dto.Title,
            Description      = dto.Description,
            Severity         = dto.Severity,
            OccurredAt       = occurred,
            ReportedAt       = now,
            ReportedBy       = userId,
            LoggedLate       = now > occurred.AddHours(24),   // 24h logging window
            Status           = NegligenceStatus.Logged,
            ResponseDeadline = now.AddDays(5),                 // 5-day response window
            IsRepeatOffense    = isRepeat,
            FinalWarningIssued = isRepeat,                      // 2nd within 12 months → final warning
            CreatedBy        = userId,
            UpdatedBy        = userId,
        });

        return (await GetByIdAsync(incident.Id))!;
    }

    public async Task<NegligenceIncidentReadDto> RespondAsync(string id, RespondNegligenceDto dto, string userId, string userName)
    {
        var incident = await incidents.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Negligence incident {id} not found.");
        if (incident.Status == NegligenceStatus.Closed)
            throw new InvalidOperationException("This incident is closed.");
        if (string.IsNullOrWhiteSpace(dto.ResponseText))
            throw new InvalidOperationException("A response is required.");

        await responses.CreateAsync(new NegligenceResponse
        {
            IncidentId    = incident.Id,
            ResponderId   = userId,
            ResponderName = userName,
            ResponderRole = dto.ResponderRole,
            ResponseText  = dto.ResponseText,
            ActionTaken   = dto.ActionTaken,
            PayrollDeductionAmount = dto.PayrollDeductionAmount,
            RespondedAt   = DateTime.UtcNow,
            CreatedBy     = userId,
            UpdatedBy     = userId,
        });

        incident.Status = dto.CloseIncident ? NegligenceStatus.Closed : NegligenceStatus.Responded;
        if (dto.CloseIncident) incident.ClosedAt = DateTime.UtcNow;
        incident.UpdatedBy = userId;
        incident.UpdatedAt = DateTime.UtcNow;
        await incidents.UpdateAsync(incident);

        // O9 — a payroll deduction routes to HR (config-gated seam; best-effort).
        if (dto.PayrollDeductionAmount is > 0m)
        {
            try
            {
                await hr.PostPayrollDeductionAsync(new PayrollDeduction(
                    incident.EmployeeId, incident.IncidentNumber, dto.PayrollDeductionAmount.Value,
                    $"Negligence: {incident.Title}"));
            }
            catch (Exception) { /* deduction posting is best-effort; the response is recorded */ }
        }

        return (await GetByIdAsync(incident.Id))!;
    }

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"NEG-{DateTime.UtcNow.Year}-";
        var count = await incidents.Query().CountAsync(x => x.IncidentNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}
