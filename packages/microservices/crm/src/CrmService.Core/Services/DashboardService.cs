using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Dashboards;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C9 (P9/P10) — SE performance &amp; MD pipeline dashboards (live-computed) + sales targets.
/// Revenue proxy = closed-deal contract value (real source = Finance INVOICE, O10 seam).</summary>
public class DashboardService(
    IGenericRepository<Opportunity> opps,
    IGenericRepository<PipelineStage> stages,
    IGenericRepository<Lead> leads,
    IGenericRepository<Deal> deals,
    IGenericRepository<ActivityTask> tasks,
    IGenericRepository<TenderBidBond> bidBonds,
    IGenericRepository<Customer> customers,
    IGenericRepository<SalesTarget> targets,
    IMapper mapper) : IDashboardService
{
    public async Task<MdDashboardDto> GetMdPipelineAsync()
    {
        var stageList = await stages.Query().AsNoTracking().OrderBy(s => s.Sequence).ToListAsync();
        var stageName = stageList.ToDictionary(s => s.Id, s => s.Name);
        var openStages = stageList.Where(s => s.StageType == PipelineStageType.Open).ToList();

        var allOpps = await opps.Query().AsNoTracking().ToListAsync();
        var open = allOpps.Where(o => o.Status == OpportunityStatus.Open).ToList();

        var byStage = openStages.Select(s =>
        {
            var items = open.Where(o => o.PipelineStageId == s.Id).ToList();
            return new PipelineStageBucket(s.Id, s.Name, items.Count,
                items.Sum(o => o.EstimatedValue), items.Sum(o => o.EstimatedValue * o.Probability));
        }).ToList();

        var cutoff12 = DateTime.UtcNow.AddMonths(-12);
        var won = allOpps.Count(o => o.Status == OpportunityStatus.Won && (o.ActualCloseDate ?? o.UpdatedAt) >= cutoff12);
        var lost = allOpps.Count(o => o.Status == OpportunityStatus.Lost && (o.ActualCloseDate ?? o.UpdatedAt) >= cutoff12);

        var topClients = open.Where(o => !string.IsNullOrWhiteSpace(o.CustomerName))
            .GroupBy(o => o.CustomerName!)
            .Select(g => new TopClient(g.Key, g.Sum(o => o.EstimatedValue), g.Count()))
            .OrderByDescending(t => t.OpenValue).Take(10).ToList();

        var staleOpps = open.Count(o => o.StaleAlertedAt != null || (DateTime.UtcNow - o.StageMovedAt).TotalDays > 14);
        var overdueTasks = await tasks.Query().CountAsync(t => t.Status == ActivityTaskStatus.Open && t.DueDate < DateTime.UtcNow);
        var expiringBonds = await bidBonds.Query().CountAsync(b => b.Status == BidBondStatus.Active && b.ValidityDate > DateTime.UtcNow && b.ValidityDate <= DateTime.UtcNow.AddDays(14));
        var dormant = await customers.Query().CountAsync(c => c.DormantSince != null);

        return new MdDashboardDto
        {
            OpenCount = open.Count,
            TotalPipelineValue = open.Sum(o => o.EstimatedValue),
            WeightedPipelineValue = open.Sum(o => o.EstimatedValue * o.Probability),
            WonLast12mo = won, LostLast12mo = lost,
            WinRate = won + lost == 0 ? 0 : Math.Round((decimal)won / (won + lost) * 100, 1),
            ByStage = byStage, TopClients = topClients,
            Risk = new PipelineRisk(staleOpps, overdueTasks, expiringBonds, dormant),
        };
    }

    public async Task<List<SePerformanceDto>> GetSePerformanceAsync(string? periodLabel)
    {
        var year = (periodLabel ?? DateTime.UtcNow.Year.ToString()).Split('-')[0];
        var allLeads = await leads.Query().AsNoTracking().ToListAsync();
        var allOpps = await opps.Query().AsNoTracking().ToListAsync();
        // The `period` argument filtered the target but not the revenue, so this reported every closed deal
        // ever won against a single year's target — attainment climbed forever and never reset in January.
        // HR reads this endpoint to compute commission, so the unfiltered figure was also being paid on.
        var yearNum = int.TryParse(year, out var parsedYear) ? parsedYear : DateTime.UtcNow.Year;
        var allDeals = (await deals.Query().AsNoTracking().Where(d => d.Status == DealStatus.Closed).ToListAsync())
            .Where(d => (d.ClosedAt ?? d.DealDate).Year == yearNum).ToList();
        var annualTargets = await targets.Query().AsNoTracking()
            .Where(t => t.PeriodType == TargetPeriodType.Annual && t.PeriodLabel == year).ToListAsync();

        var ses = allLeads.Select(l => l.AssignedTo)
            .Concat(allOpps.Select(o => o.AssignedTo))
            .Concat(allDeals.Where(d => d.WonBy != null).Select(d => d.WonBy!))
            .Concat(annualTargets.Select(t => t.EmployeeId))
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();

        var result = new List<SePerformanceDto>();
        foreach (var se in ses)
        {
            var won = allOpps.Count(o => o.AssignedTo == se && o.Status == OpportunityStatus.Won);
            var lost = allOpps.Count(o => o.AssignedTo == se && o.Status == OpportunityStatus.Lost);
            var revenue = allDeals.Where(d => d.WonBy == se).Sum(d => d.ContractValue);
            var target = annualTargets.FirstOrDefault(t => t.EmployeeId == se);
            var tgt = target?.RevenueTarget ?? 0;
            var attain = tgt == 0 ? 0 : Math.Round(revenue / tgt * 100, 1);
            var rag = tgt == 0 ? "Grey" : attain >= 100 ? "Green" : attain >= 85 ? "Amber" : "Red";
            result.Add(new SePerformanceDto
            {
                EmployeeId = se,
                EmployeeName = target?.EmployeeName ?? allOpps.FirstOrDefault(o => o.AssignedTo == se)?.AssignedToName,
                LeadsGenerated = allLeads.Count(l => l.AssignedTo == se),
                LeadsConverted = allLeads.Count(l => l.AssignedTo == se && l.IsConverted),
                OppsWon = won, OppsLost = lost,
                WinRate = won + lost == 0 ? 0 : Math.Round((decimal)won / (won + lost) * 100, 1),
                Revenue = revenue, Target = tgt, AttainmentPct = attain, Rag = rag,
            });
        }
        return result.OrderByDescending(r => r.Revenue).ToList();
    }

    public async Task<List<SalesTargetDto>> GetTargetsAsync()
        => mapper.Map<List<SalesTargetDto>>(await targets.Query().AsNoTracking().OrderByDescending(t => t.PeriodLabel).ToListAsync());

    public async Task<SalesTargetDto> SaveTargetAsync(SaveSalesTargetDto dto, string userId)
    {
        var period = Enum.TryParse<TargetPeriodType>(dto.PeriodType, true, out var pt) ? pt : TargetPeriodType.Annual;
        var existing = await targets.Query().FirstOrDefaultAsync(t => t.EmployeeId == dto.EmployeeId && t.PeriodType == period && t.PeriodLabel == dto.PeriodLabel);
        var isNew = existing is null;
        existing ??= new SalesTarget { EmployeeId = dto.EmployeeId, PeriodType = period, PeriodLabel = dto.PeriodLabel, CreatedBy = userId };
        existing.EmployeeName = dto.EmployeeName; existing.RevenueTarget = dto.RevenueTarget;
        existing.UpdatedBy = userId; existing.UpdatedAt = DateTime.UtcNow;
        if (isNew) await targets.CreateAsync(existing); else await targets.UpdateAsync(existing);
        return mapper.Map<SalesTargetDto>(existing);
    }
}
