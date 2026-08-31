using CrmService.Core.DTOs.Legal;

namespace CrmService.Core.Interfaces.Services;

/// <summary>C12 (P13) — legal &amp; contract register: NDAs, framework agreements, subcontractor
/// agreements and carrier agreements (with a vetting gate), plus an expiry-focused summary.</summary>
public interface ILegalService
{
    // NDAs
    Task<List<NdaDto>> GetNdasAsync(string? status);
    Task<NdaDto> CreateNdaAsync(SaveNdaDto dto, string userId);
    Task<NdaDto?> UpdateNdaAsync(string id, SaveNdaDto dto, string userId);
    Task<LegalActionResult> TerminateNdaAsync(string id, string userId);

    // Framework agreements
    Task<List<FrameworkDto>> GetFrameworksAsync(string? status);
    Task<FrameworkDto> CreateFrameworkAsync(SaveFrameworkDto dto, string userId);
    Task<FrameworkDto?> UpdateFrameworkAsync(string id, SaveFrameworkDto dto, string userId);
    Task<LegalActionResult> TerminateFrameworkAsync(string id, string userId);

    // Subcontractor agreements
    Task<List<SubcontractDto>> GetSubcontractsAsync(string? status);
    Task<SubcontractDto> CreateSubcontractAsync(SaveSubcontractDto dto, string userId);
    Task<SubcontractDto?> UpdateSubcontractAsync(string id, SaveSubcontractDto dto, string userId);
    Task<LegalActionResult> TerminateSubcontractAsync(string id, string userId);

    // Carrier agreements (+ vetting gate)
    Task<List<CarrierDto>> GetCarriersAsync(string? vettingStatus);
    Task<CarrierDto?> GetCarrierAsync(string id);
    Task<CarrierDto> CreateCarrierAsync(SaveCarrierDto dto, string userId);
    Task<CarrierDto?> UpdateCarrierAsync(string id, SaveCarrierDto dto, string userId);
    Task<LegalActionResult> VetCarrierAsync(string id, VetCarrierDto dto, string userId);
    Task<LegalActionResult> SuspendCarrierAsync(string id, string userId);

    // Summary
    Task<LegalSummaryDto> GetSummaryAsync();
}
