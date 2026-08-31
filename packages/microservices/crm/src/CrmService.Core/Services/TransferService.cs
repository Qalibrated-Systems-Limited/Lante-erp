using AutoMapper;
using Microsoft.EntityFrameworkCore;
using CrmService.Core.DTOs.Transfers;
using CrmService.Core.Entities;
using CrmService.Core.Enums;
using CrmService.Core.Interfaces.Repositories;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Core.Services;

/// <summary>C8 (P8) — account-ownership transfer &amp; pricing governance. See <see cref="ITransferService"/>.</summary>
public class TransferService(
    IGenericRepository<ClientTransferRequest> requests,
    IGenericRepository<ClientTransferHandover> handovers,
    IGenericRepository<Customer> customers,
    IGenericRepository<Opportunity> opps,
    IGenericRepository<ActivityTask> tasks,
    IMapper mapper) : ITransferService
{
    public async Task<TransferListResult> GetAllAsync(TransferFilterParams filter)
    {
        var q = requests.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<TransferStatus>(filter.Status, true, out var st))
            q = q.Where(r => r.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.CustomerId)) q = q.Where(r => r.CustomerId == filter.CustomerId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new TransferListResult(mapper.Map<List<TransferSummaryDto>>(items), total);
    }

    public async Task<TransferDetailDto?> GetByIdAsync(string id)
    {
        var r = await requests.Query().Include(x => x.Handover).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return r is null ? null : ToDetail(r);
    }

    public async Task<TransferDetailDto> RaiseAsync(RaiseTransferDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.IncomingOwnerId)) throw new InvalidOperationException("Incoming owner is required.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new InvalidOperationException("A reason is required.");
        var customer = await customers.Query().FirstOrDefaultAsync(c => c.Id == dto.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");
        if (customer.Status != CustomerStatus.Active)
            throw new InvalidOperationException("Only an active client's ownership can be transferred.");
        if (dto.IncomingOwnerId == customer.AccountOwnerId)
            throw new InvalidOperationException("Incoming owner is already the account owner.");
        var inFlight = await requests.Query().AnyAsync(r => r.CustomerId == dto.CustomerId
            && r.Status != TransferStatus.Completed && r.Status != TransferStatus.Rejected);
        if (inFlight) throw new InvalidOperationException("A transfer is already in progress for this client.");

        var req = new ClientTransferRequest
        {
            CustomerId = customer.Id, CustomerName = customer.Name,
            OutgoingOwnerId = customer.AccountOwnerId, IncomingOwnerId = dto.IncomingOwnerId,
            IncomingOwnerName = dto.IncomingOwnerName, Reason = dto.Reason.Trim(), EffectiveDate = dto.EffectiveDate,
            Status = TransferStatus.PendingHeadBd, RaisedBy = userId, CreatedBy = userId, UpdatedBy = userId,
        };
        var created = await requests.CreateAsync(req);
        return (await GetByIdAsync(created.Id))!;
    }

    public async Task<TransferActionResult> ApproveHeadBdAsync(string id, string userId)
    {
        var r = await Find(id); Require(r, TransferStatus.PendingHeadBd, "Head of BD endorsement");
        r.Status = TransferStatus.PendingCfo; r.HeadBdApprovedBy = userId; r.HeadBdApprovedAt = DateTime.UtcNow;
        Touch(r, userId); await requests.UpdateAsync(r);
        return new TransferActionResult(r.Status.ToString(), "Endorsed by Head of BD. Awaiting CFO review.");
    }

    public async Task<TransferActionResult> ApproveCfoAsync(string id, string userId)
    {
        var r = await Find(id); Require(r, TransferStatus.PendingCfo, "CFO review");
        r.Status = TransferStatus.PendingMd; r.CfoApprovedBy = userId; r.CfoApprovedAt = DateTime.UtcNow;
        Touch(r, userId); await requests.UpdateAsync(r);
        return new TransferActionResult(r.Status.ToString(), "Reviewed by CFO. Awaiting MD approval.");
    }

    public async Task<TransferActionResult> ApproveMdAsync(string id, string userId)
    {
        var r = await Find(id); Require(r, TransferStatus.PendingMd, "MD approval");
        r.Status = TransferStatus.PendingHandover; r.MdApprovedBy = userId; r.MdApprovedAt = DateTime.UtcNow;
        Touch(r, userId); await requests.UpdateAsync(r);
        // Create the Status Handover Document shell awaiting the 4 signatures.
        await handovers.CreateAsync(new ClientTransferHandover { TransferRequestId = r.Id, CreatedBy = userId, UpdatedBy = userId });
        return new TransferActionResult(r.Status.ToString(), "Approved by MD. Complete & sign the Status Handover Document.");
    }

    public async Task<TransferActionResult> RejectAsync(string id, RejectTransferDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new InvalidOperationException("A rejection reason is required.");
        var r = await Find(id);
        if (r.Status is TransferStatus.Completed or TransferStatus.Rejected)
            throw new InvalidOperationException($"Cannot reject a {r.Status} transfer.");
        r.Status = TransferStatus.Rejected; r.RejectedBy = userId; r.RejectedAt = DateTime.UtcNow; r.RejectionReason = dto.Reason.Trim();
        Touch(r, userId); await requests.UpdateAsync(r);
        return new TransferActionResult(r.Status.ToString(), "Transfer rejected.");
    }

    public async Task<TransferDetailDto> UpdateHandoverAsync(string id, UpdateHandoverDto dto, string userId)
    {
        var (r, h) = await FindHandover(id);
        if (h.IsPermanent) throw new InvalidOperationException("The handover document is completed and cannot be edited.");
        if (dto.ClientHistory != null) h.ClientHistory = dto.ClientHistory.Trim();
        if (dto.ActiveWork != null) h.ActiveWork = dto.ActiveWork.Trim();
        if (dto.PricingNotes != null) h.PricingNotes = dto.PricingNotes.Trim();
        if (dto.CreditTerms != null) h.CreditTerms = dto.CreditTerms.Trim();
        if (dto.OpenIssues != null) h.OpenIssues = dto.OpenIssues.Trim();
        if (dto.HandoverDocUrl != null) h.HandoverDocUrl = dto.HandoverDocUrl;
        Touch(h, userId); await handovers.UpdateAsync(h);
        return (await GetByIdAsync(id))!;
    }

    public async Task<TransferDetailDto> SignHandoverAsync(string id, SignHandoverDto dto, string userId)
    {
        var (r, h) = await FindHandover(id);
        if (h.IsPermanent) throw new InvalidOperationException("The handover document is already completed.");
        if (string.IsNullOrWhiteSpace(dto.SignatoryName)) throw new InvalidOperationException("Signatory name is required.");
        var now = DateTime.UtcNow;
        switch (dto.Role?.ToLower())
        {
            case "outgoing": h.OutgoingSignedName = dto.SignatoryName.Trim(); h.OutgoingSignedAt = now; break;
            case "incoming": h.IncomingSignedName = dto.SignatoryName.Trim(); h.IncomingSignedAt = now; break;
            case "depthead": h.DeptHeadSignedName = dto.SignatoryName.Trim(); h.DeptHeadSignedAt = now; break;
            case "md": h.MdSignedName = dto.SignatoryName.Trim(); h.MdSignedAt = now; break;
            default: throw new InvalidOperationException("Role must be Outgoing, Incoming, DeptHead, or Md.");
        }
        Touch(h, userId); await handovers.UpdateAsync(h);
        return (await GetByIdAsync(id))!;
    }

    public async Task<TransferActionResult> CompleteAsync(string id, string userId)
    {
        var (r, h) = await FindHandover(id);
        if (r.Status != TransferStatus.PendingHandover) throw new InvalidOperationException($"Transfer is {r.Status}, not awaiting handover.");
        var missing = MissingSigs(h);
        if (missing.Count > 0) throw new InvalidOperationException("Handover requires all 4 signatures. Missing: " + string.Join(", ", missing));

        // Finalise: permanent handover, complete request, switch owner, reassign open opps + tasks.
        h.IsPermanent = true; h.CompletedAt = DateTime.UtcNow; Touch(h, userId); await handovers.UpdateAsync(h);
        r.Status = TransferStatus.Completed; r.CompletedAt = DateTime.UtcNow; Touch(r, userId); await requests.UpdateAsync(r);

        var customer = await customers.Query().FirstOrDefaultAsync(c => c.Id == r.CustomerId);
        if (customer != null)
        {
            customer.AccountOwnerId = r.IncomingOwnerId;
            customer.AccountOwnerName = r.IncomingOwnerName ?? customer.AccountOwnerName;
            Touch(customer, userId); await customers.UpdateAsync(customer);
        }

        var openOpps = await opps.Query().Where(o => o.CustomerId == r.CustomerId && o.Status == OpportunityStatus.Open).ToListAsync();
        foreach (var o in openOpps) { o.AssignedTo = r.IncomingOwnerId; o.AssignedToName = r.IncomingOwnerName; Touch(o, userId); await opps.UpdateAsync(o); }
        var openTasks = await tasks.Query().Where(t => t.CustomerId == r.CustomerId && t.Status == ActivityTaskStatus.Open).ToListAsync();
        foreach (var t in openTasks) { t.AssignedTo = r.IncomingOwnerId; Touch(t, userId); await tasks.UpdateAsync(t); }

        return new TransferActionResult(r.Status.ToString(),
            $"Ownership transferred. Reassigned {openOpps.Count} opportunity(ies) and {openTasks.Count} task(s).");
    }

    // ── Helpers ──
    private static List<string> MissingSigs(ClientTransferHandover h)
    {
        var m = new List<string>();
        if (string.IsNullOrWhiteSpace(h.OutgoingSignedName)) m.Add("Outgoing owner");
        if (string.IsNullOrWhiteSpace(h.IncomingSignedName)) m.Add("Incoming owner");
        if (string.IsNullOrWhiteSpace(h.DeptHeadSignedName)) m.Add("Department Head");
        if (string.IsNullOrWhiteSpace(h.MdSignedName)) m.Add("MD");
        return m;
    }

    private TransferDetailDto ToDetail(ClientTransferRequest r)
    {
        var dto = mapper.Map<TransferDetailDto>(r);
        if (r.Handover != null) { dto.Handover = mapper.Map<HandoverDto>(r.Handover); dto.Handover.MissingSignatures = MissingSigs(r.Handover); }
        return dto;
    }

    private static void Require(ClientTransferRequest r, TransferStatus expected, string action)
    {
        if (r.Status != expected) throw new InvalidOperationException($"Cannot perform {action}: transfer is '{r.Status}', expected '{expected}'.");
    }
    private async Task<ClientTransferRequest> Find(string id) =>
        await requests.Query().FirstOrDefaultAsync(r => r.Id == id) ?? throw new KeyNotFoundException($"Transfer {id} not found.");
    private async Task<(ClientTransferRequest, ClientTransferHandover)> FindHandover(string id)
    {
        var r = await requests.Query().Include(x => x.Handover).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Transfer {id} not found.");
        if (r.Handover is null) throw new InvalidOperationException("Handover document not yet created (awaiting MD approval).");
        return (r, r.Handover);
    }
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
