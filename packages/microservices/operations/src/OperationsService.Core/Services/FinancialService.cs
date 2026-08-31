using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Claims;
using OperationsService.Core.DTOs.Financial;
using OperationsService.Core.DTOs.Requisitions;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class FinancialService : IFinancialService
{
    private readonly IGenericRepository<Requisition> _requisitions;
    private readonly IGenericRepository<Claim> _claims;
    private readonly IGenericRepository<PettyCashAdvanceForm> _pettyCash;
    private readonly IGenericRepository<PerDiemReturnForm> _perDiem;
    private readonly IGenericRepository<AdvanceReturnForm> _advanceReturns;
    private readonly IGenericRepository<Refund> _refunds;
    private readonly IMapper _mapper;

    public FinancialService(
        IGenericRepository<Requisition> requisitions,
        IGenericRepository<Claim> claims,
        IGenericRepository<PettyCashAdvanceForm> pettyCash,
        IGenericRepository<PerDiemReturnForm> perDiem,
        IGenericRepository<AdvanceReturnForm> advanceReturns,
        IGenericRepository<Refund> refunds,
        IMapper mapper)
    {
        _requisitions = requisitions;
        _claims = claims;
        _pettyCash = pettyCash;
        _perDiem = perDiem;
        _advanceReturns = advanceReturns;
        _refunds = refunds;
        _mapper = mapper;
    }

    // ── Requisitions ──────────────────────────────────────────────────────────

    public async Task<RequisitionReadDto?> GetRequisitionByIdAsync(string id)
    {
        var r = await _requisitions.GetByIdAsync(id);
        return r is null ? null : _mapper.Map<RequisitionReadDto>(r);
    }

    public async Task<IEnumerable<RequisitionReadDto>> GetRequisitionsAsync(RequisitionFilterParameters filters)
    {
        var query = _requisitions.Query().Where(r => !r.IsDeleted);
        if (!string.IsNullOrEmpty(filters.AssignmentId))
            query = query.Where(r => r.AssignmentId == filters.AssignmentId);
        if (!string.IsNullOrEmpty(filters.RequestedByUserId))
            query = query.Where(r => r.TechnicianId == filters.RequestedByUserId);
        if (filters.Status.HasValue)
            query = query.Where(r => (int)r.Status == filters.Status.Value);
        if (filters.Type.HasValue)
            query = query.Where(r => (int)r.Type == filters.Type.Value);
        var items = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return _mapper.Map<List<RequisitionReadDto>>(items);
    }

    public async Task<RequisitionReadDto> CreateRequisitionAsync(CreateRequisitionDto dto, string userId, string userName)
    {
        var lineItemsJson = dto.LineItems.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(dto.LineItems)
            : null;
        var requisition = new Requisition
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = userId,
            RequestedByName = userName,
            Type = dto.Type,
            Description = dto.Description,
            Amount = dto.Amount,
            LineItemsJson = lineItemsJson,
            Justification = dto.Justification,
            Status = RequisitionStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        return _mapper.Map<RequisitionReadDto>(await _requisitions.CreateAsync(requisition));
    }

    public async Task<RequisitionReadDto> UpdateRequisitionAsync(string id, UpdateRequisitionDto dto, string userId)
    {
        var req = await _requisitions.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Requisition {id} not found.");
        if (dto.Description != null) req.Description = dto.Description;
        if (dto.Amount.HasValue) req.Amount = dto.Amount.Value;
        if (dto.Justification != null) req.Justification = dto.Justification;
        req.UpdatedBy = userId;
        req.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RequisitionReadDto>(await _requisitions.UpdateAsync(req));
    }

    public async Task<RequisitionReadDto> ReviewRequisitionByManagerAsync(string id, ReviewRequisitionDto dto, string managerId)
    {
        var req = await _requisitions.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Requisition {id} not found.");
        req.Status = dto.Approved ? RequisitionStatus.TmApproved : RequisitionStatus.TmRejected;
        req.TmReviewedBy = managerId;
        req.TmReviewedAt = DateTime.UtcNow;
        req.TmComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) req.ApprovedAmount = dto.ApprovedAmount.Value;
        req.UpdatedBy = managerId;
        req.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RequisitionReadDto>(await _requisitions.UpdateAsync(req));
    }

    public async Task<RequisitionReadDto> ReviewRequisitionByCfoAsync(string id, ReviewRequisitionDto dto, string cfoId)
    {
        var req = await _requisitions.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Requisition {id} not found.");
        req.Status = dto.Approved ? RequisitionStatus.CfoApproved : RequisitionStatus.CfoRejected;
        req.CfoReviewedBy = cfoId;
        req.CfoReviewedAt = DateTime.UtcNow;
        req.CfoComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) req.ApprovedAmount = dto.ApprovedAmount.Value;
        req.UpdatedBy = cfoId;
        req.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RequisitionReadDto>(await _requisitions.UpdateAsync(req));
    }

    public async Task<RequisitionReadDto> MarkRequisitionPaidAsync(string id, string userId)
    {
        var req = await _requisitions.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Requisition {id} not found.");
        req.Status = RequisitionStatus.Paid;
        req.PaidAt = DateTime.UtcNow;
        req.UpdatedBy = userId;
        req.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RequisitionReadDto>(await _requisitions.UpdateAsync(req));
    }

    // ── Claims ────────────────────────────────────────────────────────────────

    public async Task<ClaimReadDto?> GetClaimByIdAsync(string id)
    {
        var c = await _claims.GetByIdAsync(id);
        return c is null ? null : _mapper.Map<ClaimReadDto>(c);
    }

    public async Task<IEnumerable<ClaimReadDto>> GetClaimsAsync(ClaimFilterParameters filters)
    {
        var query = _claims.Query().Where(c => !c.IsDeleted);
        if (!string.IsNullOrEmpty(filters.AssignmentId))
            query = query.Where(c => c.AssignmentId == filters.AssignmentId);
        if (!string.IsNullOrEmpty(filters.ClaimantUserId))
            query = query.Where(c => c.TechnicianId == filters.ClaimantUserId);
        if (filters.Status.HasValue)
            query = query.Where(c => (int)c.Status == filters.Status.Value);
        var items = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return _mapper.Map<List<ClaimReadDto>>(items);
    }

    public async Task<ClaimReadDto> CreateClaimAsync(CreateClaimDto dto, string userId, string userName)
    {
        var claim = new Claim
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = userId,
            TechnicianName = userName,
            Description = dto.Description,
            Amount = dto.Amount,
            Justification = dto.Justification,
            AttachmentPath = dto.ReceiptUrl,
            Status = ClaimStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        return _mapper.Map<ClaimReadDto>(await _claims.CreateAsync(claim));
    }

    public async Task<ClaimReadDto> UpdateClaimAsync(string id, UpdateClaimDto dto, string userId)
    {
        var claim = await _claims.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Claim {id} not found.");
        if (dto.Description != null) claim.Description = dto.Description;
        if (dto.Amount.HasValue) claim.Amount = dto.Amount.Value;
        if (dto.Justification != null) claim.Justification = dto.Justification;
        if (dto.ReceiptUrl != null) claim.AttachmentPath = dto.ReceiptUrl;
        claim.UpdatedBy = userId;
        claim.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<ClaimReadDto>(await _claims.UpdateAsync(claim));
    }

    public async Task<ClaimReadDto> ReviewClaimByManagerAsync(string id, ReviewClaimDto dto, string managerId)
    {
        var claim = await _claims.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Claim {id} not found.");
        claim.Status = dto.Approved ? ClaimStatus.ManagerApproved : ClaimStatus.ManagerRejected;
        claim.ManagerReviewedBy = managerId;
        claim.ManagerReviewedAt = DateTime.UtcNow;
        claim.ManagerComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) claim.ApprovedAmount = dto.ApprovedAmount.Value;
        claim.UpdatedBy = managerId;
        claim.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<ClaimReadDto>(await _claims.UpdateAsync(claim));
    }

    public async Task<ClaimReadDto> ReviewClaimByCfoAsync(string id, ReviewClaimDto dto, string cfoId)
    {
        var claim = await _claims.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Claim {id} not found.");
        claim.Status = dto.Approved ? ClaimStatus.CfoApproved : ClaimStatus.CfoRejected;
        claim.CfoReviewedBy = cfoId;
        claim.CfoReviewedAt = DateTime.UtcNow;
        claim.CfoComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) claim.ApprovedAmount = dto.ApprovedAmount.Value;
        claim.UpdatedBy = cfoId;
        claim.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<ClaimReadDto>(await _claims.UpdateAsync(claim));
    }

    public async Task<ClaimReadDto> MarkClaimDisbursedAsync(string id, string userId)
    {
        var claim = await _claims.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Claim {id} not found.");
        claim.Status = ClaimStatus.Disbursed;
        claim.DisbursedAt = DateTime.UtcNow;
        claim.DisbursedBy = userId;
        claim.UpdatedBy = userId;
        claim.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<ClaimReadDto>(await _claims.UpdateAsync(claim));
    }

    // ── Petty Cash ────────────────────────────────────────────────────────────

    public async Task<PettyCashReadDto?> GetPettyCashByIdAsync(string id)
    {
        var p = await _pettyCash.GetByIdAsync(id);
        return p is null ? null : _mapper.Map<PettyCashReadDto>(p);
    }

    public async Task<IEnumerable<PettyCashReadDto>> GetPettyCashByAssignmentAsync(string assignmentId)
    {
        var items = await _pettyCash.Query()
            .Where(p => p.AssignmentId == assignmentId && !p.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<PettyCashReadDto>>(items);
    }

    public async Task<PettyCashReadDto> CreatePettyCashAsync(CreatePettyCashAdvanceDto dto, string userId, string userName)
    {
        var form = new PettyCashAdvanceForm
        {
            AssignmentId = dto.AssignmentId,
            PreparedBy = userId,
            RequestedByName = userName,
            Sum = dto.Amount,
            Description = dto.Purpose,
            Status = PettyCashStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        return _mapper.Map<PettyCashReadDto>(await _pettyCash.CreateAsync(form));
    }

    public async Task<PettyCashReadDto> ReviewPettyCashByManagerAsync(string id, ReviewPettyCashDto dto, string managerId)
    {
        var form = await _pettyCash.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Petty cash form {id} not found.");
        form.Status = dto.Approved ? PettyCashStatus.Approved : PettyCashStatus.Rejected;
        form.ApprovedBy = dto.Approved ? managerId : null;
        form.ApprovedAt = dto.Approved ? DateTime.UtcNow : null;
        form.ApprovalComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) form.ApprovedAmount = dto.ApprovedAmount.Value;
        form.UpdatedBy = managerId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<PettyCashReadDto>(await _pettyCash.UpdateAsync(form));
    }

    public async Task<PettyCashReadDto> ReviewPettyCashByCfoAsync(string id, ReviewPettyCashDto dto, string cfoId)
    {
        var form = await _pettyCash.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Petty cash form {id} not found.");
        form.Status = dto.Approved ? PettyCashStatus.Approved : PettyCashStatus.Rejected;
        form.CfoReviewedBy = cfoId;
        form.CfoReviewedAt = DateTime.UtcNow;
        form.CfoComments = dto.Comments;
        if (dto.ApprovedAmount.HasValue) form.ApprovedAmount = dto.ApprovedAmount.Value;
        form.UpdatedBy = cfoId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<PettyCashReadDto>(await _pettyCash.UpdateAsync(form));
    }

    public async Task<PettyCashReadDto> MarkPettyCashDisbursedAsync(string id, string userId)
    {
        var form = await _pettyCash.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Petty cash form {id} not found.");
        form.Status = PettyCashStatus.Disbursed;
        form.DisbursedAt = DateTime.UtcNow;
        form.DisbursedBy = userId;
        form.UpdatedBy = userId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<PettyCashReadDto>(await _pettyCash.UpdateAsync(form));
    }

    // ── Per Diem ──────────────────────────────────────────────────────────────

    public async Task<PerDiemReturnReadDto?> GetPerDiemReturnByIdAsync(string id)
    {
        var p = await _perDiem.GetByIdAsync(id);
        return p is null ? null : _mapper.Map<PerDiemReturnReadDto>(p);
    }

    public async Task<IEnumerable<PerDiemReturnReadDto>> GetPerDiemReturnsByAssignmentAsync(string assignmentId)
    {
        var items = await _perDiem.Query()
            .Where(p => p.AssignmentId == assignmentId && !p.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<PerDiemReturnReadDto>>(items);
    }

    public async Task<PerDiemReturnReadDto> CreatePerDiemReturnAsync(CreatePerDiemReturnDto dto, string userId, string userName)
    {
        var lineItemsJson = dto.LineItems.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(dto.LineItems)
            : null;
        var form = new PerDiemReturnForm
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = userId,
            SubmittedByName = userName,
            TotalAmount = dto.TotalAdvanced,
            TotalSpent = dto.TotalSpent,
            Notes = dto.Notes,
            DetailsJson = dto.Details,
            LineItemsJson = lineItemsJson,
            Status = ReturnFormStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        return _mapper.Map<PerDiemReturnReadDto>(await _perDiem.CreateAsync(form));
    }

    public async Task<PerDiemReturnReadDto> ReviewPerDiemReturnByManagerAsync(string id, ReviewPerDiemReturnDto dto, string managerId)
    {
        var form = await _perDiem.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Per diem return {id} not found.");
        form.Status = dto.Approved ? ReturnFormStatus.Approved : ReturnFormStatus.Rejected;
        form.ApprovedBy = dto.Approved ? managerId : null;
        form.ApprovedAt = dto.Approved ? DateTime.UtcNow : null;
        form.ManagerComments = dto.Comments;
        form.RejectionReason = !dto.Approved ? dto.Comments : null;
        form.UpdatedBy = managerId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<PerDiemReturnReadDto>(await _perDiem.UpdateAsync(form));
    }

    public async Task<PerDiemReturnReadDto> ReviewPerDiemReturnByCfoAsync(string id, ReviewPerDiemReturnDto dto, string cfoId)
    {
        var form = await _perDiem.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Per diem return {id} not found.");
        form.Status = dto.Approved ? ReturnFormStatus.Approved : ReturnFormStatus.Rejected;
        form.CfoReviewedBy = cfoId;
        form.CfoReviewedAt = DateTime.UtcNow;
        form.CfoComments = dto.Comments;
        form.RejectionReason = !dto.Approved ? dto.Comments : null;
        form.UpdatedBy = cfoId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<PerDiemReturnReadDto>(await _perDiem.UpdateAsync(form));
    }

    // ── Advance Returns ───────────────────────────────────────────────────────

    public async Task<AdvanceReturnReadDto?> GetAdvanceReturnByIdAsync(string id)
    {
        var a = await _advanceReturns.Query()
            .Include(x => x.LineItems)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        return a is null ? null : _mapper.Map<AdvanceReturnReadDto>(a);
    }

    public async Task<IEnumerable<AdvanceReturnReadDto>> GetAdvanceReturnsByAssignmentAsync(string assignmentId)
    {
        var items = await _advanceReturns.Query()
            .Include(a => a.LineItems)
            .Where(a => a.AssignmentId == assignmentId && !a.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<AdvanceReturnReadDto>>(items);
    }

    public async Task<AdvanceReturnReadDto> CreateAdvanceReturnAsync(CreateAdvanceReturnDto dto, string userId, string userName)
    {
        var totalAccountedFor = dto.LineItems.Sum(li => li.Amount);
        var form = new AdvanceReturnForm
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = userId,
            SubmittedByName = userName,
            TotalAmount = dto.TotalAdvanced,
            TotalAccountedFor = totalAccountedFor,
            AmountReturned = dto.TotalAdvanced - totalAccountedFor,
            Notes = dto.Notes,
            DetailsJson = dto.Details,
            Status = ReturnFormStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId,
            LineItems = dto.LineItems.Select(li => new AdvanceReturnLineItem
            {
                Description = li.Description,
                Amount = li.Amount,
                ReceiptNumber = li.ReceiptUrl
            }).ToList()
        };
        return _mapper.Map<AdvanceReturnReadDto>(await _advanceReturns.CreateAsync(form));
    }

    public async Task<AdvanceReturnReadDto> ReviewAdvanceReturnByManagerAsync(string id, ReviewAdvanceReturnDto dto, string managerId)
    {
        var form = await _advanceReturns.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Advance return {id} not found.");
        form.Status = dto.Approved ? ReturnFormStatus.Approved : ReturnFormStatus.Rejected;
        form.ApprovedBy = dto.Approved ? managerId : null;
        form.ApprovedAt = dto.Approved ? DateTime.UtcNow : null;
        form.ManagerComments = dto.Comments;
        form.RejectionReason = !dto.Approved ? dto.Comments : null;
        form.UpdatedBy = managerId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<AdvanceReturnReadDto>(await _advanceReturns.UpdateAsync(form));
    }

    public async Task<AdvanceReturnReadDto> ReviewAdvanceReturnByCfoAsync(string id, ReviewAdvanceReturnDto dto, string cfoId)
    {
        var form = await _advanceReturns.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Advance return {id} not found.");
        form.Status = dto.Approved ? ReturnFormStatus.Approved : ReturnFormStatus.Rejected;
        form.CfoReviewedBy = cfoId;
        form.CfoReviewedAt = DateTime.UtcNow;
        form.CfoComments = dto.Comments;
        form.RejectionReason = !dto.Approved ? dto.Comments : null;
        form.UpdatedBy = cfoId;
        form.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<AdvanceReturnReadDto>(await _advanceReturns.UpdateAsync(form));
    }

    // ── Refunds ───────────────────────────────────────────────────────────────

    public async Task<RefundReadDto?> GetRefundByIdAsync(string id)
    {
        var r = await _refunds.GetByIdAsync(id);
        return r is null ? null : _mapper.Map<RefundReadDto>(r);
    }

    public async Task<IEnumerable<RefundReadDto>> GetRefundsByAssignmentAsync(string assignmentId)
    {
        var items = await _refunds.Query()
            .Where(r => r.AssignmentId == assignmentId && !r.IsDeleted)
            .ToListAsync();
        return _mapper.Map<List<RefundReadDto>>(items);
    }

    public async Task<RefundReadDto> CreateRefundAsync(CreateRefundDto dto, string userId, string userName)
    {
        var refund = new Refund
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = userId,
            TechnicianName = userName,
            Amount = dto.Amount,
            Description = dto.Reason,
            AttachmentPath = dto.ReceiptUrl,
            Status = RefundStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        return _mapper.Map<RefundReadDto>(await _refunds.CreateAsync(refund));
    }

    public async Task<RefundReadDto> ReviewRefundAsync(string id, ReviewRefundDto dto, string managerId)
    {
        var refund = await _refunds.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Refund {id} not found.");
        refund.Status = dto.Approved ? RefundStatus.ManagerApproved : RefundStatus.ManagerRejected;
        refund.ManagerReviewedBy = managerId;
        refund.ManagerReviewedAt = DateTime.UtcNow;
        refund.ManagerComments = dto.Comments;
        refund.UpdatedBy = managerId;
        refund.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RefundReadDto>(await _refunds.UpdateAsync(refund));
    }

    public async Task<RefundReadDto> ProcessRefundAsync(string id, string userId)
    {
        var refund = await _refunds.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Refund {id} not found.");
        refund.Status = RefundStatus.CfoReceived;
        refund.ReceivedAt = DateTime.UtcNow;
        refund.ReceivedBy = userId;
        refund.UpdatedBy = userId;
        refund.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<RefundReadDto>(await _refunds.UpdateAsync(refund));
    }
}
