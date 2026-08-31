namespace CrmService.Core.DTOs.Legal;

// ── NDA ──
public class SaveNdaDto
{
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? Purpose { get; set; }
    public DateTime SignedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }   // defaults to SignedDate + 3 years when omitted
    public string? FileUrl { get; set; }
}
public class NdaDto
{
    public string Id { get; set; } = string.Empty;
    public string NdaNumber { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? Purpose { get; set; }
    public DateTime SignedDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public int DaysToExpiry { get; set; }
}

// ── Framework agreement ──
public class SaveFrameworkDto
{
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? PerformanceReviewDate { get; set; }
    public decimal? Value { get; set; }
    public string? FileUrl { get; set; }
}
public class FrameworkDto
{
    public string Id { get; set; } = string.Empty;
    public string AgreementNumber { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? PerformanceReviewDate { get; set; }
    public decimal? Value { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public int DaysToExpiry { get; set; }
}

// ── Subcontractor agreement ──
public class SaveSubcontractDto
{
    public string SubcontractorName { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public string? ProjectId { get; set; }
    public string? ScopeOfWork { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? Value { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public string? FileUrl { get; set; }
}
public class SubcontractDto
{
    public string Id { get; set; } = string.Empty;
    public string AgreementNumber { get; set; } = string.Empty;
    public string SubcontractorName { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public string? ProjectId { get; set; }
    public string? ScopeOfWork { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? Value { get; set; }
    public DateTime? InsuranceExpiryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public int DaysToExpiry { get; set; }
    public bool InsuranceExpired { get; set; }
}

// ── Carrier agreement ──
public class SaveCarrierDto
{
    public string CarrierName { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public string? NtsaLicenceNumber { get; set; }
    public DateTime? NtsaLicenceExpiry { get; set; }
    public DateTime? GoodsInTransitInsuranceExpiry { get; set; }
    public DateTime? VehicleInspectionExpiry { get; set; }
    public string? FileUrl { get; set; }
}
public class VetCarrierDto { public bool Approve { get; set; } public string? Notes { get; set; } }
public class CarrierDto
{
    public string Id { get; set; } = string.Empty;
    public string AgreementNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public string? NtsaLicenceNumber { get; set; }
    public DateTime? NtsaLicenceExpiry { get; set; }
    public DateTime? GoodsInTransitInsuranceExpiry { get; set; }
    public DateTime? VehicleInspectionExpiry { get; set; }
    public string VettingStatus { get; set; } = string.Empty;
    public string? VettingNotes { get; set; }
    public DateTime? VettedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public bool IsUsable { get; set; }          // Approved vetting + no expired compliance doc
    public bool ComplianceExpired { get; set; } // any of NTSA / insurance / inspection expired
}

// ── Summary ──
public class LegalSummaryDto
{
    public int ActiveNdas { get; set; }
    public int NdasExpiringSoon { get; set; }        // ≤60d
    public int ActiveFrameworks { get; set; }
    public int FrameworksExpiringSoon { get; set; }
    public int ReviewsDue { get; set; }               // performance review date reached, still active
    public int ActiveSubcontracts { get; set; }
    public int SubcontractInsuranceExpiring { get; set; }
    public int Carriers { get; set; }
    public int CarriersPendingVetting { get; set; }
    public int CarrierComplianceExpiring { get; set; } // any doc ≤30d or expired, approved carriers
}

public record LegalActionResult(string Status, string Message);
