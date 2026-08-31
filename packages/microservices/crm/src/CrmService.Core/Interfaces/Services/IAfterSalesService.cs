using CrmService.Core.DTOs.AfterSales;

namespace CrmService.Core.Interfaces.Services;

/// <summary>C11 (P12) — after-sales &amp; retention: satisfaction surveys (CSAT), service contracts
/// (with renewal alerts), client complaints and annual NPS, plus a retention summary.</summary>
public interface IAfterSalesService
{
    // Satisfaction surveys (CSAT)
    Task<List<SurveyDto>> GetSurveysAsync(string? customerId, string? status);
    Task<SurveyDto> SendSurveyAsync(SendSurveyDto dto, string userId);
    Task<SurveyDto?> RespondSurveyAsync(string id, SurveyResponseDto dto, string userId);

    // Service contracts
    Task<List<ServiceContractDto>> GetServiceContractsAsync(string? customerId, string? status);
    Task<ServiceContractDto?> GetServiceContractAsync(string id);
    Task<ServiceContractDto> CreateServiceContractAsync(SaveServiceContractDto dto, string userId);
    Task<ServiceContractDto?> UpdateServiceContractAsync(string id, SaveServiceContractDto dto, string userId);
    Task<AfterSalesActionResult> RenewServiceContractAsync(string id, RenewServiceContractDto dto, string userId);
    Task<AfterSalesActionResult> CancelServiceContractAsync(string id, string userId);

    // Complaints
    Task<List<ComplaintDto>> GetComplaintsAsync(string? customerId, string? status);
    Task<ComplaintDto> RaiseComplaintAsync(RaiseComplaintDto dto, string userId);
    Task<AfterSalesActionResult> AssignComplaintAsync(string id, AssignComplaintDto dto, string userId);
    Task<AfterSalesActionResult> StartComplaintAsync(string id, string userId);
    Task<AfterSalesActionResult> ResolveComplaintAsync(string id, ResolveComplaintDto dto, string userId);
    Task<AfterSalesActionResult> CloseComplaintAsync(string id, string userId);

    // NPS
    Task<List<NpsDto>> GetNpsAsync(string? customerId, int? year);
    Task<NpsDto> SendNpsAsync(SendNpsDto dto, string userId);
    Task<NpsDto?> RespondNpsAsync(string id, NpsResponseDto dto, string userId);

    // Retention summary
    Task<AfterSalesSummaryDto> GetSummaryAsync();

    // O6 — calibration recall (inbound from Operations): schedule a re-calibration follow-up task
    Task<AfterSalesActionResult> RecordCalibrationRecallAsync(CalibrationRecallDto dto, string userId);
}
