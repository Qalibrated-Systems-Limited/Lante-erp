using CrmService.Core.DTOs.Quotations;

namespace CrmService.Core.Interfaces.Services;

public interface IQuotationService
{
    Task<QuotationListResult> GetAllAsync(QuotationFilterParams filter);
    Task<QuotationDetailDto?> GetByIdAsync(string id);
    Task<List<QuotationSummaryDto>> GetVersionsAsync(string quoteNumber);
    Task<LastSaleInfo> GetLastSaleAsync(string? customerId, string productRef);

    Task<QuotationDetailDto> CreateAsync(CreateQuotationDto dto, string userId);
    Task<QuotationDetailDto> SaveLinesAsync(string id, SaveQuotationLinesDto dto, string userId);
    Task<QuotationActionResult> SubmitAsync(string id, string userId);
    Task<QuotationActionResult> DeptHeadReviewAsync(string id, bool approve, string? reason, string userId);
    Task<QuotationActionResult> MdApproveAsync(string id, string userId);
    Task<QuotationActionResult> SendAsync(string id, string userId);
    Task<QuotationActionResult> RecordOutcomeAsync(string id, QuotationOutcomeDto dto, string userId);
    Task<QuotationDetailDto> ReviseAsync(string id, string userId);

    Task<List<PriceListDto>> GetPriceListsAsync();
    Task<PriceListDto> SavePriceListAsync(SavePriceListDto dto, string userId);
}
