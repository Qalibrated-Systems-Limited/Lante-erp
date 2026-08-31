using CrmService.Core.DTOs.Transfers;

namespace CrmService.Core.Interfaces.Services;

public interface ITransferService
{
    Task<TransferListResult> GetAllAsync(TransferFilterParams filter);
    Task<TransferDetailDto?> GetByIdAsync(string id);
    Task<TransferDetailDto> RaiseAsync(RaiseTransferDto dto, string userId);
    Task<TransferActionResult> ApproveHeadBdAsync(string id, string userId);
    Task<TransferActionResult> ApproveCfoAsync(string id, string userId);
    Task<TransferActionResult> ApproveMdAsync(string id, string userId);
    Task<TransferActionResult> RejectAsync(string id, RejectTransferDto dto, string userId);
    Task<TransferDetailDto> UpdateHandoverAsync(string id, UpdateHandoverDto dto, string userId);
    Task<TransferDetailDto> SignHandoverAsync(string id, SignHandoverDto dto, string userId);
    Task<TransferActionResult> CompleteAsync(string id, string userId);
}
