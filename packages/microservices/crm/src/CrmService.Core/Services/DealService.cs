using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Deals;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C5 (P6) — deal close, contract award &amp; project/finance conversion. See <see cref="IDealService"/>.</summary>
public class DealService(
    IGenericRepository<Deal> deals,
    IGenericRepository<DealProduct> dealProducts,
    IGenericRepository<Contract> contracts,
    IGenericRepository<Opportunity> opps,
    IGenericRepository<Quotation> quotations,
    IFinanceGateway finance,
    IProjectGateway projects,
    IMapper mapper) : IDealService
{
    public async Task<DealListResult> GetAllAsync(DealFilterParams filter)
    {
        var q = deals.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<DealStatus>(filter.Status, true, out var st))
            q = q.Where(d => d.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) q = q.Where(d => d.CustomerId == filter.CustomerId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(d => d.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new DealListResult(items.Select(ToSummary).ToList(), total);
    }

    public async Task<DealDetailDto?> GetByIdAsync(string id)
    {
        var d = await deals.Query().Include(x => x.Products).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return null;
        var dto = mapper.Map<DealDetailDto>(d);
        dto.HasProject = !string.IsNullOrEmpty(d.ProjectId);
        var contract = await contracts.Query().AsNoTracking().FirstOrDefaultAsync(c => c.DealId == id);
        if (contract != null) dto.Contract = mapper.Map<ContractDto>(contract);
        return dto;
    }

    public async Task<DealDetailDto> CreateFromOpportunityAsync(CreateDealDto dto, string userId)
    {
        var opp = await opps.Query().FirstOrDefaultAsync(o => o.Id == dto.OpportunityId)
            ?? throw new KeyNotFoundException("Opportunity not found.");
        if (opp.Status != OpportunityStatus.Won)
            throw new InvalidOperationException($"A deal can only be created from a Won opportunity (current: {opp.Status}).");
        if (await deals.Query().AnyAsync(d => d.OpportunityId == opp.Id))
            throw new InvalidOperationException("A deal already exists for this opportunity.");

        // Link the current accepted (else sent) quotation for this opportunity.
        var quote = await quotations.Query().Include(q => q.Lines)
            .Where(q => q.OpportunityId == opp.Id && q.IsCurrent
                        && (q.Status == QuotationStatus.Accepted || q.Status == QuotationStatus.Sent))
            .OrderByDescending(q => q.Status == QuotationStatus.Accepted).ThenByDescending(q => q.IssueDate)
            .FirstOrDefaultAsync();

        var value = dto.ContractValue ?? quote?.TotalAmount ?? opp.EstimatedValue;

        // DealNumber is unique-indexed; GenerateNumberAsync's count-then-format is racy under
        // concurrent creates, so retry with a freshly counted number on collision.
        Deal created = null!;
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var deal = new Deal
            {
                DealNumber = await GenerateNumberAsync("DEAL", deals.Query().Select(d => d.DealNumber)),
                OpportunityId = opp.Id,
                CustomerId = opp.CustomerId,
                CustomerName = opp.CustomerName,
                QuotationId = quote?.Id,
                ContractValue = value,
                DealDate = DateTime.UtcNow,
                ContractStart = dto.ContractStart,
                ContractEnd = dto.ContractEnd,
                PaymentSchedule = dto.PaymentSchedule,
                Status = DealStatus.Open,
                WonBy = opp.AssignedTo,
                CreatedBy = userId, UpdatedBy = userId,
            };
            try
            {
                created = await deals.CreateAsync(deal);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }

        // Copy quotation lines → deal products.
        if (quote != null)
            foreach (var l in quote.Lines.Where(x => !x.IsDeleted))
                await dealProducts.CreateAsync(new DealProduct
                {
                    DealId = created.Id, ProductId = l.ProductId, Description = l.Description,
                    Quantity = l.Quantity, UnitPrice = l.UnitPrice, TotalPrice = l.LineTotal,
                    CreatedBy = userId, UpdatedBy = userId,
                });

        // Link the deal back onto the opportunity.
        opp.DealId = created.Id; opp.UpdatedBy = userId; opp.UpdatedAt = DateTime.UtcNow;
        await opps.UpdateAsync(opp);

        return (await GetByIdAsync(created.Id))!;
    }

    public async Task<DealDetailDto> UpdateAsync(string id, UpdateDealDto dto, string userId)
    {
        var d = await Find(id);
        if (d.Status != DealStatus.Open) throw new InvalidOperationException($"Cannot edit a {d.Status} deal.");
        if (dto.ContractValue.HasValue) d.ContractValue = dto.ContractValue.Value;
        if (dto.ContractStart.HasValue) d.ContractStart = dto.ContractStart;
        if (dto.ContractEnd.HasValue) d.ContractEnd = dto.ContractEnd;
        if (dto.PaymentSchedule != null) d.PaymentSchedule = dto.PaymentSchedule;
        Touch(d, userId); await deals.UpdateAsync(d);
        return (await GetByIdAsync(id))!;
    }

    public async Task<ContractDto> RegisterContractAsync(string id, RegisterContractDto dto, string userId)
    {
        var d = await Find(id);
        var contract = await contracts.Query().FirstOrDefaultAsync(c => c.DealId == id);
        var isNew = contract is null;
        contract ??= new Contract { DealId = id, ContractNumber = await GenerateNumberAsync("CON", contracts.Query().Select(c => c.ContractNumber)), CreatedBy = userId };
        contract.CustomerId = d.CustomerId;
        contract.Title = string.IsNullOrWhiteSpace(dto.Title) ? $"Contract for {d.DealNumber}" : dto.Title.Trim();
        contract.ContractType = dto.ContractType?.Trim();
        contract.StartDate = dto.StartDate ?? d.ContractStart;
        contract.EndDate = dto.EndDate ?? d.ContractEnd;
        contract.Value = dto.Value ?? d.ContractValue;
        contract.PaymentTerms = dto.PaymentTerms?.Trim();
        contract.RetentionPct = dto.RetentionPct;
        contract.FileUrl = dto.FileUrl;
        contract.SignedAt = dto.SignedAt ?? contract.SignedAt ?? DateTime.UtcNow;
        contract.Status = ContractStatus.Active;
        contract.UpdatedBy = userId; contract.UpdatedAt = DateTime.UtcNow;

        // ContractNumber is unique-indexed; retry a fresh new contract with a freshly counted
        // number if a concurrent RegisterContractAsync call for this same (still contract-less)
        // deal already took the one just generated above.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (isNew) await contracts.CreateAsync(contract); else await contracts.UpdateAsync(contract);
                break;
            }
            catch (DbUpdateException) when (isNew && attempt < maxAttempts)
            {
                contract.ContractNumber = await GenerateNumberAsync("CON", contracts.Query().Select(c => c.ContractNumber));
            }
        }
        return mapper.Map<ContractDto>(contract);
    }

    public async Task<DealActionResult> CreateProjectAsync(string id, string userId)
    {
        var d = await Find(id);
        if (!string.IsNullOrEmpty(d.ProjectId))
            throw new InvalidOperationException($"A project already exists for this deal ({d.ProjectId}).");
        var res = await projects.CreateProjectFromDealAsync(new DealProjectRequest(
            d.Id, d.DealNumber, d.CustomerId, d.CustomerName, d.CustomerName ?? d.DealNumber, d.ContractValue, d.ContractStart, d.ContractEnd));
        if (!res.Success) throw new InvalidOperationException(res.Message);
        if (!string.IsNullOrEmpty(res.ProjectId))
        {
            d.ProjectId = res.ProjectId; Touch(d, userId); await deals.UpdateAsync(d);
        }
        return new DealActionResult(d.Status.ToString(), res.Message);
    }

    public async Task<DealActionResult> CloseAsync(string id, string userId)
    {
        var d = await Find(id);
        if (d.Status == DealStatus.Closed) throw new InvalidOperationException("Deal is already closed.");
        d.Status = DealStatus.Closed;
        d.ClosedAt = DateTime.UtcNow;

        // Fire the Finance invoice once (one-way latch prevents duplicates on re-close/re-save).
        var message = "Deal closed.";
        if (!d.InvoiceTriggered)
        {
            var res = await finance.RaiseDealInvoiceAsync(new DealInvoiceRequest(
                d.Id, d.DealNumber, d.CustomerId, d.CustomerName, d.ContractValue, d.Currency));
            // Only latch once an invoice was actually created — a transient Finance failure stays retryable.
            if (res.InvoiceId != null)
            {
                d.InvoiceTriggered = true;
                d.FinanceInvoiceId = res.InvoiceId;
            }
            message = $"Deal closed. {res.Message}";
        }
        Touch(d, userId); await deals.UpdateAsync(d);
        return new DealActionResult(d.Status.ToString(), message);
    }

    // ── Helpers ──
    private DealSummaryDto ToSummary(Deal d)
    {
        var dto = mapper.Map<DealSummaryDto>(d);
        dto.HasProject = !string.IsNullOrEmpty(d.ProjectId);
        return dto;
    }

    private async Task<Deal> Find(string id) =>
        await deals.Query().FirstOrDefaultAsync(d => d.Id == id)
        ?? throw new KeyNotFoundException($"Deal {id} not found.");

    private static async Task<string> GenerateNumberAsync(string prefix, IQueryable<string> numbers)
    {
        var p = $"{prefix}-{DateTime.UtcNow.Year}-";
        var count = await numbers.Where(n => n.StartsWith(p)).CountAsync();
        return $"{p}{(count + 1):D4}";
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
