namespace FinanceService.Core.DTOs;

public class CreateImprestDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
}

public class RetireImprestDto
{
    public List<RetireLineDto> Lines { get; set; } = new();
}

public class RetireLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReceiptUrl { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public string? ExpenseAccountCode { get; set; }        // defaults 5500
}

public class ImprestReadDto
{
    /// <summary>The currency this record was transacted in. Persisted as CurrencyId on the entity and
    /// projected here because the read side silently dropped it: currency was captured, validated
    /// against a configured rate, stored — and then discarded on the way out, so no consumer could tell
    /// a USD record from a KES one (#288).</summary>
    public string? CurrencyCode { get; set; }
    public string Id { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DisbursedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int? DaysToDue { get; set; }                    // negative = overdue
    public decimal RetiredAmount { get; set; }
    public decimal UnretiredBalance { get; set; }
}

public class PersonalAdvanceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ImprestRef { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ConvertedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
