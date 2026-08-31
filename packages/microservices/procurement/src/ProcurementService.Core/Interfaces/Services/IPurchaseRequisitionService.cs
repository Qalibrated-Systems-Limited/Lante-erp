using ProcurementService.Core.DTOs.Requisitions;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P2 (LPO steps 1–2) — Purchase Requisition workflow. Build a Draft, submit it (hard budget check
/// via the Finance seam), Dept-Head reviews within the 2-business-day SLA (breach escalates to MD). An
/// approved PR is what P3 quotation is raised against.</summary>
public interface IPurchaseRequisitionService
{
    Task<PrListResult> GetAllAsync(PrFilterParams filter);
    Task<PrReadDto?> GetByIdAsync(string id);
    Task<PrReadDto> CreateAsync(CreatePrDto dto, string userId, string? userName);
    Task<PrReadDto?> UpdateAsync(string id, CreatePrDto dto, string userId);
    Task<PrActionResult> SubmitAsync(string id, string userId, string? userName);
    Task<PrActionResult> ReviewAsync(string id, ReviewPrDto dto, string userId, string? userName);
    Task<PrActionResult> EscalateAsync(string id, string userId);
    Task<PrSummaryDto> GetSummaryAsync();
}
