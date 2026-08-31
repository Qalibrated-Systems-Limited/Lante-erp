using OperationsService.Core.DTOs.ServiceRequests;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O5 — TM-side Service &amp; Calibration Request pipeline, migrated from ticketing. Owns review,
/// quotation build/send/LPO/revise, certificate recording and dispatch. Dispatch now creates the
/// operations Assignment in-process (no cross-service HTTP hop) via <see cref="IAssignmentService"/>.
/// Throws <see cref="KeyNotFoundException"/> (→404) / <see cref="InvalidOperationException"/> (→400);
/// the global exception middleware maps these to responses.
/// </summary>
public interface IServiceRequestService
{
    Task<IngestResult> IngestAsync(IngestServiceRequestDto dto);
    Task<IngestResult> CreateAsync(IngestServiceRequestDto dto, string userId);
    Task<ServiceRequestSummaryDto?> TrackAsync(string reference);
    Task<SrSignatureResult> SubmitSignatureAsync(string reference, string signatureData);
    Task<ServiceRequestListResult> GetAllAsync(ServiceRequestFilterParams filter);
    Task<ServiceRequestDetailDto?> GetByIdAsync(string id);
    Task<SrReviewResult> ReviewAsync(string id, TmReviewDto dto, string userId);
    Task<QuotationDetailDto> CreateQuotationAsync(string id, CreateQuotationDto dto, string userId);
    Task<QuotationDetailDto> UpdateQuotationAsync(string id, UpdateQuotationDto dto, string userId);
    Task<SrSendResult> SendQuotationAsync(string id);
    Task<SrLpoResult> RecordLpoAsync(string id, RecordLpoDto dto);
    Task<SrReviseResult> ReviseQuotationAsync(string id);
    Task<SrQuotationRejectResult> RejectQuotationAsync(string id, RejectQuotationDto dto, string userId);
    Task<SrCertificateResult> RecordCertificateAsync(string id, RecordCertificateDto dto);
    Task<SrAssignmentResult> CreateAssignmentAsync(string id, CreateSrAssignmentDto dto, string userId);
    Task<SrCancelResult> CancelAsync(string id, CancelServiceRequestDto dto, string userId);
}
