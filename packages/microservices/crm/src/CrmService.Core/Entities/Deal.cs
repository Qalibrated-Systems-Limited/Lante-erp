using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P6 — Deal close, contract award &amp; project conversion (CRM-015/041). Created when an opportunity
/// is Won; links the approved quotation. Closing fires a decoupled Finance invoice (InvoiceTriggered
/// one-way latch prevents duplicates) and can one-click create the Module 5 project (Project.ClientId
/// ← this deal's customer).
/// </summary>
public class Deal : BaseEntity
{
    public string DealNumber { get; set; } = string.Empty;   // DEAL-{year}-{seq}
    public string OpportunityId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? QuotationId { get; set; }

    public decimal ContractValue { get; set; }
    public string Currency { get; set; } = "KES";
    public DateTime DealDate { get; set; } = DateTime.UtcNow;
    public DateTime? ContractStart { get; set; }
    public DateTime? ContractEnd { get; set; }
    public string? PaymentSchedule { get; set; }

    public DealStatus Status { get; set; } = DealStatus.Open;
    public string? WonBy { get; set; }
    public string? ApprovedBy { get; set; }

    // Cross-module links (set via config-gated seams).
    public bool InvoiceTriggered { get; set; }         // one-way latch (CRM-041)
    public string? FinanceInvoiceId { get; set; }
    public string? ProjectId { get; set; }             // Module 5 project (one-click, CRM-015)
    public DateTime? ClosedAt { get; set; }

    public ICollection<DealProduct> Products { get; set; } = new List<DealProduct>();
}
