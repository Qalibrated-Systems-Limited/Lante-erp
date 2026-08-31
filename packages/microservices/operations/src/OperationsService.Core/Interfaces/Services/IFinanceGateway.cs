namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O3 — cross-module seam to finance-service. Config-gated: the default no-op keeps operations
/// decoupled from finance being present, and <c>Finance:Enabled=true</c> + <c>FinanceService:BaseUrl</c>
/// swaps in the real HTTP client (PR2). Two hooks fire on milestone sign-off: (1) raise an invoice for a
/// billable, signed-off milestone; (2) report % completion for IFRS-15 revenue recognition.
/// </summary>
public interface IFinanceGateway
{
    Task RaiseMilestoneInvoiceAsync(MilestoneInvoiceRequest request, CancellationToken ct = default);

    /// <summary>
    /// PR2 — no real target exists: finance-service has no revenue-recognition feature (no schedule,
    /// no endpoint, no entity). Every implementation therefore logs rather than posts. Left on the
    /// interface because the call site — sign-off is the recognition trigger — is correct and should
    /// not have to be rediscovered if finance ever grows IFRS-15 support.
    /// </summary>
    Task ReportRevenueRecognitionAsync(RevenueRecognitionRequest request, CancellationToken ct = default);

    /// <summary>O4 — post an approved timesheet's labour cost to a project's actuals journal.</summary>
    Task PostTimesheetLabourAsync(TimesheetLabourPosting posting, CancellationToken ct = default);

    /// <summary>O7 — raise an invoice for an approved, billable variation order.</summary>
    Task RaiseVariationInvoiceAsync(VariationInvoiceRequest request, CancellationToken ct = default);
}

/// <param name="ClientId">
/// The project's CRM customer id. Carried so the finance customer can be matched on the shared
/// <c>CRM-{id}</c> code that CRM's own invoice gateway writes — without it operations would match by
/// name only and create a duplicate finance customer for a client CRM has already registered.
/// </param>
public record MilestoneInvoiceRequest(
    string ProjectId,
    string MilestoneId,
    string MilestoneTitle,
    decimal Amount,
    string? ClientName,
    string RaisedByUserId,
    string? ClientId = null);

public record RevenueRecognitionRequest(
    string ProjectId,
    string MilestoneId,
    int PercentComplete,
    decimal RecognizedAmount);

/// <param name="Amount">
/// The labour cost of the hours, priced by the caller from the project's rate card (PR1
/// <c>ContractRate.CostRate</c>). The gateway never invents a rate: a journal needs money, and a
/// guessed rate is a wrong number in the general ledger. Zero means "could not be priced" and the
/// posting is skipped.
/// </param>
public record TimesheetLabourPosting(
    string ProjectId,
    string EmployeeId,
    DateTime WeekEndDate,
    decimal Hours,
    decimal OvertimeHours,
    decimal Amount = 0m,
    string? RateCode = null);

public record VariationInvoiceRequest(
    string ProjectId,
    string VariationOrderId,
    string VariationOrderNumber,
    decimal Amount,
    string RaisedByUserId,
    string? ClientName = null,
    string? ClientId = null);
