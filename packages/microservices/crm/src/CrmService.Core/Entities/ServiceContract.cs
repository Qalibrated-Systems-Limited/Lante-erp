using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P12 (CRM-056) — SERVICE_CONTRACT. Calibration/maintenance/support agreements with a client;
/// renewal alerts fire 60 and 30 days before expiry. Distinct from the C5 deal CONTRACT (which records
/// the commercial terms of a won deal); this is the ongoing recurring-service agreement.</summary>
public class ServiceContract : BaseEntity
{
    public string ContractNumber { get; set; } = string.Empty;   // SVC-{yr}-{seq}
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public ServiceContractType ContractType { get; set; } = ServiceContractType.Maintenance;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Value { get; set; }
    public string? BillingFrequency { get; set; }                // e.g. Monthly / Quarterly / Annual
    public ServiceContractStatus Status { get; set; } = ServiceContractStatus.Active;
    public bool AutoRenew { get; set; }
    public string? RenewedFromContractId { get; set; }           // set on the new contract when renewed
    public DateTime? RenewalAlert60SentAt { get; set; }
    public DateTime? RenewalAlert30SentAt { get; set; }
}
