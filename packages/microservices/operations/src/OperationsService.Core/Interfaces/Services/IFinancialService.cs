using OperationsService.Core.DTOs.Claims;
using OperationsService.Core.DTOs.Financial;
using OperationsService.Core.DTOs.Requisitions;

namespace OperationsService.Core.Interfaces.Services;

public interface IFinancialService
{
    // Requisitions
    Task<RequisitionReadDto?> GetRequisitionByIdAsync(string id);
    Task<IEnumerable<RequisitionReadDto>> GetRequisitionsAsync(RequisitionFilterParameters filters);
    Task<RequisitionReadDto> CreateRequisitionAsync(CreateRequisitionDto dto, string userId, string userName);
    Task<RequisitionReadDto> UpdateRequisitionAsync(string id, UpdateRequisitionDto dto, string userId);
    Task<RequisitionReadDto> ReviewRequisitionByManagerAsync(string id, ReviewRequisitionDto dto, string managerId);
    Task<RequisitionReadDto> ReviewRequisitionByCfoAsync(string id, ReviewRequisitionDto dto, string cfoId);
    Task<RequisitionReadDto> MarkRequisitionPaidAsync(string id, string userId);

    // Claims
    Task<ClaimReadDto?> GetClaimByIdAsync(string id);
    Task<IEnumerable<ClaimReadDto>> GetClaimsAsync(ClaimFilterParameters filters);
    Task<ClaimReadDto> CreateClaimAsync(CreateClaimDto dto, string userId, string userName);
    Task<ClaimReadDto> UpdateClaimAsync(string id, UpdateClaimDto dto, string userId);
    Task<ClaimReadDto> ReviewClaimByManagerAsync(string id, ReviewClaimDto dto, string managerId);
    Task<ClaimReadDto> ReviewClaimByCfoAsync(string id, ReviewClaimDto dto, string cfoId);
    Task<ClaimReadDto> MarkClaimDisbursedAsync(string id, string userId);

    // Petty cash
    Task<PettyCashReadDto?> GetPettyCashByIdAsync(string id);
    Task<IEnumerable<PettyCashReadDto>> GetPettyCashByAssignmentAsync(string assignmentId);
    Task<PettyCashReadDto> CreatePettyCashAsync(CreatePettyCashAdvanceDto dto, string userId, string userName);
    Task<PettyCashReadDto> ReviewPettyCashByManagerAsync(string id, ReviewPettyCashDto dto, string managerId);
    Task<PettyCashReadDto> ReviewPettyCashByCfoAsync(string id, ReviewPettyCashDto dto, string cfoId);
    Task<PettyCashReadDto> MarkPettyCashDisbursedAsync(string id, string userId);

    // Per diem returns
    Task<PerDiemReturnReadDto?> GetPerDiemReturnByIdAsync(string id);
    Task<IEnumerable<PerDiemReturnReadDto>> GetPerDiemReturnsByAssignmentAsync(string assignmentId);
    Task<PerDiemReturnReadDto> CreatePerDiemReturnAsync(CreatePerDiemReturnDto dto, string userId, string userName);
    Task<PerDiemReturnReadDto> ReviewPerDiemReturnByManagerAsync(string id, ReviewPerDiemReturnDto dto, string managerId);
    Task<PerDiemReturnReadDto> ReviewPerDiemReturnByCfoAsync(string id, ReviewPerDiemReturnDto dto, string cfoId);

    // Advance returns
    Task<AdvanceReturnReadDto?> GetAdvanceReturnByIdAsync(string id);
    Task<IEnumerable<AdvanceReturnReadDto>> GetAdvanceReturnsByAssignmentAsync(string assignmentId);
    Task<AdvanceReturnReadDto> CreateAdvanceReturnAsync(CreateAdvanceReturnDto dto, string userId, string userName);
    Task<AdvanceReturnReadDto> ReviewAdvanceReturnByManagerAsync(string id, ReviewAdvanceReturnDto dto, string managerId);
    Task<AdvanceReturnReadDto> ReviewAdvanceReturnByCfoAsync(string id, ReviewAdvanceReturnDto dto, string cfoId);

    // Refunds
    Task<RefundReadDto?> GetRefundByIdAsync(string id);
    Task<IEnumerable<RefundReadDto>> GetRefundsByAssignmentAsync(string assignmentId);
    Task<RefundReadDto> CreateRefundAsync(CreateRefundDto dto, string userId, string userName);
    Task<RefundReadDto> ReviewRefundAsync(string id, ReviewRefundDto dto, string managerId);
    Task<RefundReadDto> ProcessRefundAsync(string id, string userId);
}
