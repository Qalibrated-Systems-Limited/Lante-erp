using ProcurementService.Core.DTOs.Emergency;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P8 (PROC-004) — emergency procurement. A controlled bypass of sourcing only: the LPO is raised
/// without quotations, but the MD must authorise it before the purchase is made, the justification and waiver
/// are mandatory, a post-hoc requisition is due within 24 hours, and every case is reported to the board.
/// Receipt, 3-way match and payment authority are untouched — an emergency LPO is an ordinary LPO.</summary>
public interface IEmergencyProcurementService
{
    Task<EmergencyListResult> GetAllAsync(EmergencyFilterParams filter);
    Task<EmergencyReadDto?> GetByIdAsync(string id);
    Task<EmergencyReadDto?> GetByPoAsync(string poId);
    Task<EmergencySummaryDto> GetSummaryAsync();
    /// <summary>Everything declared in a "YYYY-MM" period, for the monthly board pack.</summary>
    Task<List<EmergencyRowDto>> GetBoardPackAsync(string period);

    /// <summary>Declares the emergency and raises the LPO, routed to the MD and not yet issued.</summary>
    Task<EmergencyActionResult> DeclareAsync(DeclareEmergencyDto dto, string userId);
    /// <summary>The PROC-004 key control — MD authorisation, which is what issues the LPO.</summary>
    Task<EmergencyActionResult> MdApproveAsync(string id, MdApproveEmergencyDto dto, string userId, string? userName);
    Task<EmergencyActionResult> RaisePostHocPrAsync(string id, PostHocPrDto dto, string userId);
    Task<EmergencyActionResult> MarkBoardPackAsync(string id, BoardPackDto dto, string userId);
}
