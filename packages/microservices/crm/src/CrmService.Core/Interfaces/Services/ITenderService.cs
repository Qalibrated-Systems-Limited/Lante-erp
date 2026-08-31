using CrmService.Core.DTOs.Tenders;

namespace CrmService.Core.Interfaces.Services;

public interface ITenderService
{
    Task<TenderListResult> GetAllAsync(TenderFilterParams filter);
    Task<TenderDetailDto?> GetByIdAsync(string id);
    Task<TenderDetailDto> CreateAsync(CreateTenderDto dto, string userId, string? userName);
    Task<TenderDetailDto> UpdateAsync(string id, UpdateTenderDto dto, string userId);
    Task<BidBondDto> SaveBidBondAsync(string id, SaveBidBondDto dto, string userId);
    Task<TenderActionResult> SubmitAsync(string id, string userId);
    Task<TenderActionResult> MarkWonAsync(string id, TenderOutcomeDto dto, string userId);
    Task<TenderActionResult> MarkLostAsync(string id, TenderOutcomeDto dto, string userId);
    Task<TenderActionResult> MarkNoBidAsync(string id, TenderOutcomeDto dto, string userId);
}
