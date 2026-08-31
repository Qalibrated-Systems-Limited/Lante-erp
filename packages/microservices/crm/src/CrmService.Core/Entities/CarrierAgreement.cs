using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P13 (CRM-062) — CARRIER_AGREEMENT. Transport/logistics carriers (SUPPLIER seam). Vetting is a
/// GATE: a carrier may not be used until <see cref="VettingStatus"/> is Approved (valid NTSA licence +
/// goods-in-transit insurance + vehicle inspection). Each compliance document has its own expiry alert.</summary>
public class CarrierAgreement : BaseEntity
{
    public string AgreementNumber { get; set; } = string.Empty;   // CAR-{yr}-{seq}
    public string CarrierName { get; set; } = string.Empty;       // SUPPLIER seam (string ref)
    public string? SupplierId { get; set; }
    public string? NtsaLicenceNumber { get; set; }
    public DateTime? NtsaLicenceExpiry { get; set; }
    public DateTime? GoodsInTransitInsuranceExpiry { get; set; }
    public DateTime? VehicleInspectionExpiry { get; set; }
    public CarrierVettingStatus VettingStatus { get; set; } = CarrierVettingStatus.Pending;
    public string? VettingNotes { get; set; }
    public DateTime? VettedAt { get; set; }
    public string? VettedBy { get; set; }
    public CarrierStatus Status { get; set; } = CarrierStatus.Active;
    public string? FileUrl { get; set; }
    public DateTime? NtsaAlertSentAt { get; set; }
    public DateTime? InsuranceAlertSentAt { get; set; }
    public DateTime? InspectionAlertSentAt { get; set; }
}
