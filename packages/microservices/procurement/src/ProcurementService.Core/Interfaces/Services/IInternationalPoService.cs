using ProcurementService.Core.DTOs.International;

namespace ProcurementService.Core.Interfaces.Services;

/// <summary>P7 (PROC-003) — international / China sourcing. Attaches foreign-currency detail to an existing
/// LPO, governs the T/T advance (mandatory MD approval before funds move), tracks the shipment and the
/// customs entry, and rolls every cost up into a true landed cost per unit.</summary>
public interface IInternationalPoService
{
    Task<IntlListResult> GetAllAsync(IntlFilterParams filter);
    Task<IntlPoReadDto?> GetByIdAsync(string id);
    Task<IntlPoReadDto?> GetByPoAsync(string poId);
    Task<IntlSummaryDto> GetSummaryAsync();
    /// <summary>Currencies (with rates) Finance offers, so the UI can show the rate before committing.</summary>
    Task<List<CurrencyRateDto>> GetCurrenciesAsync();

    Task<IntlActionResult> CreateAsync(CreateIntlPoDto dto, string userId);
    Task<IntlActionResult> UpdateShipmentAsync(string id, ShipmentDto dto, string userId);

    // T/T advance — request → MD approval → funds sent (Finance raises the ad-hoc voucher).
    Task<IntlActionResult> RequestTtAsync(string id, RequestTtDto dto, string userId);
    Task<IntlActionResult> ApproveTtAsync(string id, string userId);
    Task<IntlActionResult> SendTtAsync(string id, string userId);

    // Landed cost build-up.
    Task<IntlActionResult> AddComponentAsync(string id, AddComponentDto dto, string userId);
    Task<IntlActionResult> RemoveComponentAsync(string componentId, string userId);
    Task<IntlActionResult> DeclareCustomsAsync(string id, DeclareCustomsDto dto, string userId);
    /// <summary>Recomputes the roll-up and marks the order Costed.</summary>
    Task<IntlActionResult> FinaliseCostAsync(string id, string userId);
}
