using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Tenders;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C6 (P5) — tender &amp; bid management. See <see cref="ITenderService"/>.</summary>
public class TenderService(
    IGenericRepository<Tender> tenders,
    IGenericRepository<TenderBidBond> bidBonds,
    IOpportunityService opportunities,
    IMapper mapper) : ITenderService
{
    private static bool IsOpen(Tender t) => t.Status is TenderStatus.Registered or TenderStatus.Submitted;
    private static int DaysTo(DateTime d) => (int)Math.Ceiling((d - DateTime.UtcNow).TotalDays);

    public async Task<TenderListResult> GetAllAsync(TenderFilterParams filter)
    {
        var query = tenders.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(s) || t.ClientName.ToLower().Contains(s) || t.TenderNumber.ToLower().Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<TenderStatus>(filter.Status, true, out var st))
            query = query.Where(t => t.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo)) query = query.Where(t => t.AssignedTo == filter.AssignedTo);

        var all = await query.OrderBy(t => t.SubmissionDeadline).ToListAsync();
        var open = all.Where(IsOpen).ToList();
        var items = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        var bondTenderIds = (await bidBonds.Query().AsNoTracking().Select(b => b.TenderId).ToListAsync()).ToHashSet();

        var dtos = items.Select(t => { var d = ToSummary(t); d.HasBidBond = bondTenderIds.Contains(t.Id); return d; }).ToList();
        return new TenderListResult(dtos, all.Count, open.Count,
            open.Count(t => DaysTo(t.SubmissionDeadline) <= 7), open.Sum(t => t.EstimatedValue));
    }

    public async Task<TenderDetailDto?> GetByIdAsync(string id)
    {
        var t = await tenders.Query().Include(x => x.BidBond).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return null;
        var dto = mapper.Map<TenderDetailDto>(t);
        dto.DaysToDeadline = DaysTo(t.SubmissionDeadline);
        dto.HasBidBond = t.BidBond != null;
        if (t.BidBond != null) dto.BidBond = mapper.Map<BidBondDto>(t.BidBond);
        return dto;
    }

    public async Task<TenderDetailDto> CreateAsync(CreateTenderDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) throw new InvalidOperationException("Tender title is required.");
        if (string.IsNullOrWhiteSpace(dto.ClientName)) throw new InvalidOperationException("Client name is required.");
        // TenderNumber is unique-indexed; retry with a freshly counted number on collision (see
        // OpportunityService.CreateAsync for the full rationale).
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var t = new Tender
            {
                TenderNumber = await GenerateNumberAsync(),
                Title = dto.Title.Trim(), Source = dto.Source?.Trim(), CustomerId = dto.CustomerId,
                ClientName = dto.ClientName.Trim(), Description = dto.Description?.Trim(),
                EstimatedValue = dto.EstimatedValue, SubmissionDeadline = dto.SubmissionDeadline,
                AssignedTo = string.IsNullOrWhiteSpace(dto.AssignedTo) ? userId : dto.AssignedTo,
                AssignedToName = dto.AssignedToName ?? userName,
                Status = TenderStatus.Registered, CreatedBy = userId, UpdatedBy = userId,
            };
            try
            {
                var created = await tenders.CreateAsync(t);
                return (await GetByIdAsync(created.Id))!;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }
    }

    public async Task<TenderDetailDto> UpdateAsync(string id, UpdateTenderDto dto, string userId)
    {
        var t = await Find(id);
        if (t.Status is TenderStatus.Won or TenderStatus.Lost) throw new InvalidOperationException($"Cannot edit a {t.Status} tender.");
        if (dto.Title != null) t.Title = dto.Title.Trim();
        if (dto.Source != null) t.Source = dto.Source.Trim();
        if (dto.ClientName != null) t.ClientName = dto.ClientName.Trim();
        if (dto.Description != null) t.Description = dto.Description.Trim();
        if (dto.EstimatedValue.HasValue) t.EstimatedValue = dto.EstimatedValue.Value;
        if (dto.SubmissionDeadline.HasValue) { t.SubmissionDeadline = dto.SubmissionDeadline.Value; t.Alert14SentAt = t.Alert7SentAt = t.Alert3SentAt = t.Alert1SentAt = null; }
        if (dto.AssignedTo != null) t.AssignedTo = dto.AssignedTo;
        if (dto.AssignedToName != null) t.AssignedToName = dto.AssignedToName;
        Touch(t, userId); await tenders.UpdateAsync(t);
        return (await GetByIdAsync(id))!;
    }

    public async Task<BidBondDto> SaveBidBondAsync(string id, SaveBidBondDto dto, string userId)
    {
        _ = await Find(id);
        var bond = await bidBonds.Query().FirstOrDefaultAsync(b => b.TenderId == id);
        var isNew = bond is null;
        bond ??= new TenderBidBond { TenderId = id, CreatedBy = userId };
        bond.GuaranteeNumber = dto.GuaranteeNumber.Trim();
        bond.IssuingBank = dto.IssuingBank.Trim();
        bond.ValidityDate = dto.ValidityDate;
        bond.Amount = dto.Amount;
        bond.Status = BidBondStatus.Active;
        bond.ExpiryAlertSentAt = null;
        bond.UpdatedBy = userId; bond.UpdatedAt = DateTime.UtcNow;
        if (isNew) await bidBonds.CreateAsync(bond); else await bidBonds.UpdateAsync(bond);
        return mapper.Map<BidBondDto>(bond);
    }

    public async Task<TenderActionResult> SubmitAsync(string id, string userId)
    {
        var t = await Find(id);
        if (t.Status != TenderStatus.Registered) throw new InvalidOperationException($"Only a registered tender can be submitted (current: {t.Status}).");
        t.Status = TenderStatus.Submitted; Touch(t, userId); await tenders.UpdateAsync(t);
        return new TenderActionResult(t.Status.ToString(), "Tender marked submitted.");
    }

    public async Task<TenderActionResult> MarkWonAsync(string id, TenderOutcomeDto dto, string userId)
    {
        var t = await Find(id);
        if (t.Status is TenderStatus.Won or TenderStatus.Lost or TenderStatus.Cancelled)
            throw new InvalidOperationException($"Tender is already {t.Status}.");

        // Won → create an opportunity (enters the pipeline, P3 → deal close P6).
        var opp = await opportunities.CreateAsync(new DTOs.Opportunities.CreateOpportunityDto
        {
            Name = $"{t.ClientName} — {t.Title}", CustomerId = t.CustomerId, CustomerName = t.ClientName,
            AssignedTo = t.AssignedTo, AssignedToName = t.AssignedToName,
            EstimatedValue = t.EstimatedValue, Source = "Tender",
        }, userId, t.AssignedToName);

        t.Status = TenderStatus.Won; t.LinkedOpportunityId = opp.Id; t.OutcomeNotes = dto.Notes?.Trim();
        Touch(t, userId); await tenders.UpdateAsync(t);
        return new TenderActionResult(t.Status.ToString(), $"Tender won — opportunity {opp.OpportunityNumber} created.");
    }

    public async Task<TenderActionResult> MarkLostAsync(string id, TenderOutcomeDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new InvalidOperationException("A reason is required for a lost tender.");
        var t = await Find(id);
        if (t.Status is TenderStatus.Won or TenderStatus.Lost) throw new InvalidOperationException($"Tender is already {t.Status}.");
        t.Status = TenderStatus.Lost; t.LostReason = dto.Reason.Trim(); Touch(t, userId); await tenders.UpdateAsync(t);
        return new TenderActionResult(t.Status.ToString(), "Tender marked lost.");
    }

    public async Task<TenderActionResult> MarkNoBidAsync(string id, TenderOutcomeDto dto, string userId)
    {
        var t = await Find(id);
        if (t.Status is TenderStatus.Won or TenderStatus.Lost) throw new InvalidOperationException($"Tender is already {t.Status}.");
        t.Status = TenderStatus.NoBid; t.OutcomeNotes = dto.Notes?.Trim(); Touch(t, userId); await tenders.UpdateAsync(t);
        return new TenderActionResult(t.Status.ToString(), "Tender marked no-bid.");
    }

    private TenderSummaryDto ToSummary(Tender t)
    {
        var dto = mapper.Map<TenderSummaryDto>(t);
        dto.DaysToDeadline = DaysTo(t.SubmissionDeadline);
        return dto;
    }
    private async Task<Tender> Find(string id) =>
        await tenders.Query().FirstOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException($"Tender {id} not found.");
    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"TND-{DateTime.UtcNow.Year}-";
        var count = await tenders.Query().CountAsync(t => t.TenderNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
