namespace FinanceService.Core.DTOs;

/// Universal journal-posting payload. This is the backbone entry point other modules call
/// (sourceModule/sourceDocumentId identify the origin). Raised in-Finance = null source.
public class CreateJournalDto
{
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CurrencyCode { get; set; }                  // defaults to base currency (KES)
    public string? SourceModule { get; set; }
    public string? SourceDocumentId { get; set; }
    public bool IsAccrual { get; set; }
    public DateTime? AutoReverseDate { get; set; }
    /// When true (inbound module post), the entry is created already posted (skips manual review).
    public bool PostImmediately { get; set; }
    public List<CreateJournalLineDto> Lines { get; set; } = new();
}

public class CreateJournalLineDto
{
    public string AccountId { get; set; } = string.Empty;
    public string? AccountCode { get; set; }                   // alternative to AccountId
    public string? CostCenterId { get; set; }
    public string? BranchId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class JournalLineReadDto
{
    public int LineNo { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? CostCenterId { get; set; }
    public string? BranchId { get; set; }
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class JournalReadDto
{
    /// <summary>The currency this record was transacted in. Persisted as CurrencyId on the entity and
    /// projected here because the read side silently dropped it: currency was captured, validated
    /// against a configured rate, stored — and then discarded on the way out, so no consumer could tell
    /// a USD record from a KES one (#288).</summary>
    public string? CurrencyCode { get; set; }
    public string Id { get; set; } = string.Empty;
    public string EntryNo { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string PeriodId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SourceModule { get; set; }
    public string? SourceDocumentId { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string? PreparedBy { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public List<JournalLineReadDto> Lines { get; set; } = new();
}

/// A single trial-balance row.
public class TrialBalanceRowDto
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class TrialBalanceDto
{
    public DateTime AsOf { get; set; }
    public List<TrialBalanceRowDto> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
}
