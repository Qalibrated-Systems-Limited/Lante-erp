namespace FinanceService.Core.DTOs;

public class SetRateDto
{
    public decimal Rate { get; set; }
}

/// One statutory obligation's status for a given period.
public class ObligationDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal OutstandingBalance { get; set; }           // GL liability balance (Cr − Dr) as of period end
    public decimal RemittedAmount { get; set; }
    public DateTime DueDate { get; set; }
    public int DaysToDue { get; set; }                        // negative = overdue
    public string Status { get; set; } = string.Empty;
    public string? RemittanceId { get; set; }
}

public class RemitStatutoryDto
{
    public string ObligationCode { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;        // yyyy-MM
    public decimal? Amount { get; set; }                      // defaults to the outstanding balance
    public string? BankAccountCode { get; set; }              // defaults 1100
    public string? PaymentReference { get; set; }
}

public class RemittanceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string RefNo { get; set; } = string.Empty;
    public string ObligationCode { get; set; } = string.Empty;
    public string ObligationName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime RemittedAt { get; set; }
    public string? PaymentReference { get; set; }
    public string? JournalEntryId { get; set; }
}
