using ComplianceService.Core.DTOs.Common;

namespace ComplianceService.Core.DTOs.Icm;

public class IntercompanyTxnFilterParameters : PaginationParameters
{
    public bool? UnreconciledOnly { get; set; }
}

public class IntercompanyTxnReadDto
{
    public string Id { get; set; } = string.Empty;
    public string IcsaId { get; set; } = string.Empty;
    public string RelatedPartyId { get; set; } = string.Empty;
    public string? RelatedPartyName { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public string QslLedgerRef { get; set; } = string.Empty;
    public string SisterLedgerRef { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public int AgeDays { get; set; }
}

public class CreateIntercompanyTxnDto
{
    public string IcsaId { get; set; } = string.Empty;
    public string RelatedPartyId { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public string QslLedgerRef { get; set; } = string.Empty;
    public string SisterLedgerRef { get; set; } = string.Empty;
    public DateTime? PostedAt { get; set; }
}

// Core-field edit — ReconciledAt stays workflow-controlled (see Reconcile).
public class UpdateIntercompanyTxnDto
{
    public string IcsaId { get; set; } = string.Empty;
    public string RelatedPartyId { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Amount { get; set; }
    public string QslLedgerRef { get; set; } = string.Empty;
    public string SisterLedgerRef { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}
