using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Opportunities;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C3 (P3) — opportunity management &amp; weighted pipeline. See <see cref="IOpportunityService"/>.</summary>
public class OpportunityService(
    IGenericRepository<Opportunity> opps,
    IGenericRepository<PipelineStage> stages,
    IGenericRepository<OpportunityActivity> activities,
    IGenericRepository<OpportunityCompetitor> competitors,
    IMapper mapper) : IOpportunityService
{
    // The 7 standard stages, seeded per tenant on first use (name, sequence, win-prob, type).
    private static readonly (string Name, int Seq, decimal Prob, PipelineStageType Type)[] Defaults =
    {
        ("Prospect", 1, 0.10m, PipelineStageType.Open),
        ("Qualified", 2, 0.25m, PipelineStageType.Open),
        ("Needs Analysis", 3, 0.40m, PipelineStageType.Open),
        ("Proposal Submitted", 4, 0.60m, PipelineStageType.Open),
        ("Negotiation", 5, 0.80m, PipelineStageType.Open),
        ("Won", 6, 1.00m, PipelineStageType.Won),
        ("Lost", 7, 0.00m, PipelineStageType.Lost),
    };

    private static bool IsStale(Opportunity o) =>
        o.Status == OpportunityStatus.Open && (DateTime.UtcNow - o.StageMovedAt).TotalDays > 14;

    private async Task<List<PipelineStage>> EnsureStagesAsync()
    {
        var existing = await stages.Query().OrderBy(s => s.Sequence).ToListAsync();
        if (existing.Count > 0) return existing;
        foreach (var d in Defaults)
            await stages.CreateAsync(new PipelineStage
            {
                Name = d.Name, Sequence = d.Seq, WinProbability = d.Prob, StageType = d.Type,
                CreatedBy = "system", UpdatedBy = "system",
            });
        return await stages.Query().OrderBy(s => s.Sequence).ToListAsync();
    }

    public async Task<List<PipelineStageDto>> GetStagesAsync()
        => mapper.Map<List<PipelineStageDto>>(await EnsureStagesAsync());

    public async Task<PipelineBoardResult> GetBoardAsync(string? assignedTo)
    {
        var stageList = await EnsureStagesAsync();
        var openStages = stageList.Where(s => s.StageType == PipelineStageType.Open).OrderBy(s => s.Sequence).ToList();

        var q = opps.Query().AsNoTracking().Where(o => o.Status == OpportunityStatus.Open);
        if (!string.IsNullOrWhiteSpace(assignedTo)) q = q.Where(o => o.AssignedTo == assignedTo);
        var openOpps = await q.ToListAsync();

        var columns = openStages.Select(s =>
        {
            var items = openOpps.Where(o => o.PipelineStageId == s.Id).OrderByDescending(o => o.EstimatedValue).ToList();
            return new PipelineColumnDto
            {
                Stage = mapper.Map<PipelineStageDto>(s),
                Opportunities = items.Select(ToSummary).ToList(),
                Count = items.Count,
                Value = items.Sum(o => o.EstimatedValue),
                WeightedValue = items.Sum(o => o.EstimatedValue * o.Probability),
            };
        }).ToList();

        return new PipelineBoardResult(
            columns,
            openOpps.Sum(o => o.EstimatedValue),
            openOpps.Sum(o => o.EstimatedValue * o.Probability),
            openOpps.Count);
    }

    public async Task<OpportunityListResult> GetAllAsync(OpportunityFilterParams filter)
    {
        var query = opps.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(s) || o.OpportunityNumber.ToLower().Contains(s)
                || (o.CustomerName != null && o.CustomerName.ToLower().Contains(s)));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<OpportunityStatus>(filter.Status, true, out var st))
            query = query.Where(o => o.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.PipelineStageId))
            query = query.Where(o => o.PipelineStageId == filter.PipelineStageId);
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo))
            query = query.Where(o => o.AssignedTo == filter.AssignedTo);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(o => o.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new OpportunityListResult(items.Select(ToSummary).ToList(), total);
    }

    public async Task<OpportunityDetailDto?> GetByIdAsync(string id)
    {
        var o = await opps.Query().Include(x => x.Activities).Include(x => x.Competitors).AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) return null;
        var dto = mapper.Map<OpportunityDetailDto>(o);
        dto.WeightedValue = o.EstimatedValue * o.Probability;
        dto.IsStale = IsStale(o);
        dto.Activities = dto.Activities.OrderByDescending(a => a.ActivityDate).ToList();
        return dto;
    }

    public async Task<OpportunityDetailDto> CreateAsync(CreateOpportunityDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidOperationException("Opportunity name is required.");
        var stageList = await EnsureStagesAsync();

        PipelineStage stage;
        if (!string.IsNullOrWhiteSpace(dto.PipelineStageId))
            stage = stageList.FirstOrDefault(s => s.Id == dto.PipelineStageId)
                    ?? throw new InvalidOperationException("Pipeline stage not found.");
        else
            stage = stageList.Where(s => s.StageType == PipelineStageType.Open).OrderBy(s => s.Sequence).First();

        // OpportunityNumber is unique-indexed; GenerateNumberAsync's count-then-format is racy
        // under concurrent creates, so retry with a freshly counted number on collision.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var opp = new Opportunity
            {
                OpportunityNumber = await GenerateNumberAsync(),
                Name = dto.Name.Trim(),
                CustomerId = dto.CustomerId,
                CustomerName = dto.CustomerName,
                LeadId = dto.LeadId,
                PipelineStageId = stage.Id,
                StageName = stage.Name,
                Probability = stage.WinProbability,
                AssignedTo = string.IsNullOrWhiteSpace(dto.AssignedTo) ? userId : dto.AssignedTo,
                AssignedToName = dto.AssignedToName ?? userName,
                EstimatedValue = dto.EstimatedValue,
                Source = dto.Source,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = OpportunityStatus.Open,
                StageMovedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
            };
            try
            {
                var created = await opps.CreateAsync(opp);
                return (await GetByIdAsync(created.Id))!;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }
    }

    public async Task<OpportunityDetailDto> UpdateAsync(string id, UpdateOpportunityDto dto, string userId)
    {
        var o = await FindAsync(id);
        if (dto.Name != null) o.Name = dto.Name.Trim();
        if (dto.CustomerId != null) o.CustomerId = dto.CustomerId;
        if (dto.CustomerName != null) o.CustomerName = dto.CustomerName;
        if (dto.AssignedTo != null) o.AssignedTo = dto.AssignedTo;
        if (dto.AssignedToName != null) o.AssignedToName = dto.AssignedToName;
        if (dto.EstimatedValue.HasValue) o.EstimatedValue = dto.EstimatedValue.Value;
        if (dto.ExpectedCloseDate.HasValue) o.ExpectedCloseDate = dto.ExpectedCloseDate;
        Touch(o, userId);
        await opps.UpdateAsync(o);
        return (await GetByIdAsync(id))!;
    }

    public async Task<OpportunityActionResult> AdvanceStageAsync(string id, AdvanceStageDto dto, string userId)
    {
        var o = await FindAsync(id);
        if (o.Status != OpportunityStatus.Open)
            throw new InvalidOperationException($"Cannot move a {o.Status} opportunity.");
        var stageList = await EnsureStagesAsync();
        var stage = stageList.FirstOrDefault(s => s.Id == dto.PipelineStageId)
            ?? throw new InvalidOperationException("Pipeline stage not found.");
        if (stage.StageType != PipelineStageType.Open)
            throw new InvalidOperationException("Use the Won / Lost actions to close an opportunity.");

        o.PipelineStageId = stage.Id;
        o.StageName = stage.Name;
        o.Probability = stage.WinProbability;
        o.StageMovedAt = DateTime.UtcNow;
        o.StaleAlertedAt = null;
        Touch(o, userId);
        await opps.UpdateAsync(o);
        return new OpportunityActionResult(o.Status.ToString(), o.StageName, $"Moved to {stage.Name}.");
    }

    public async Task<OpportunityActionResult> MarkWonAsync(string id, string userId)
    {
        var o = await FindAsync(id);
        if (o.Status != OpportunityStatus.Open) throw new InvalidOperationException($"Opportunity is already {o.Status}.");
        var won = (await EnsureStagesAsync()).First(s => s.StageType == PipelineStageType.Won);
        o.Status = OpportunityStatus.Won;
        o.PipelineStageId = won.Id;
        o.StageName = won.Name;
        o.Probability = 1.00m;
        o.ActualCloseDate = DateTime.UtcNow;
        o.StageMovedAt = DateTime.UtcNow;
        Touch(o, userId);
        await opps.UpdateAsync(o);
        // C5 (P6): one-click deal close creates a DEAL + CONTRACT + Finance invoice from this opportunity.
        return new OpportunityActionResult(o.Status.ToString(), o.StageName, "Opportunity won — ready for deal close.");
    }

    public async Task<OpportunityActionResult> MarkLostAsync(string id, MarkLostDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new InvalidOperationException("A lost reason is required.");
        var o = await FindAsync(id);
        if (o.Status != OpportunityStatus.Open) throw new InvalidOperationException($"Opportunity is already {o.Status}.");
        var lost = (await EnsureStagesAsync()).First(s => s.StageType == PipelineStageType.Lost);
        o.Status = OpportunityStatus.Lost;
        o.PipelineStageId = lost.Id;
        o.StageName = lost.Name;
        o.Probability = 0m;
        o.LostReason = dto.Reason.Trim();
        o.ActualCloseDate = DateTime.UtcNow;
        o.StageMovedAt = DateTime.UtcNow;
        Touch(o, userId);
        await opps.UpdateAsync(o);

        if (!string.IsNullOrWhiteSpace(dto.CompetitorName))
            await competitors.CreateAsync(new OpportunityCompetitor
            {
                OpportunityId = o.Id, CompetitorName = dto.CompetitorName.Trim(),
                Notes = dto.CompetitorNotes?.Trim(), WasSelected = true,
                CreatedBy = userId, UpdatedBy = userId,
            });

        return new OpportunityActionResult(o.Status.ToString(), o.StageName, "Opportunity marked lost.");
    }

    public async Task<OpportunityActivityDto> AddActivityAsync(string id, CreateOpportunityActivityDto dto, string userId)
    {
        var o = await FindAsync(id);
        var act = await activities.CreateAsync(new OpportunityActivity
        {
            OpportunityId = id,
            ActivityType = string.IsNullOrWhiteSpace(dto.ActivityType) ? "Note" : dto.ActivityType.Trim(),
            Subject = dto.Subject.Trim(),
            Description = dto.Description?.Trim(),
            Outcome = dto.Outcome?.Trim(),
            NextFollowUp = dto.NextFollowUp,
            PerformedBy = userId,
            ActivityDate = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
        // Logging activity counts as engagement — clear any stale flag (but not stage-movement clock).
        o.StaleAlertedAt = null;
        Touch(o, userId);
        await opps.UpdateAsync(o);
        return mapper.Map<OpportunityActivityDto>(act);
    }

    private OpportunitySummaryDto ToSummary(Opportunity o)
    {
        var dto = mapper.Map<OpportunitySummaryDto>(o);
        dto.WeightedValue = o.EstimatedValue * o.Probability;
        dto.IsStale = IsStale(o);
        return dto;
    }

    private async Task<Opportunity> FindAsync(string id) =>
        await opps.Query().FirstOrDefaultAsync(o => o.Id == id)
        ?? throw new KeyNotFoundException($"Opportunity {id} not found.");

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"OPP-{DateTime.UtcNow.Year}-";
        var count = await opps.Query().CountAsync(o => o.OpportunityNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
