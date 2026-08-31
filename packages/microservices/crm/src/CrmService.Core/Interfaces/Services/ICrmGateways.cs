namespace CrmService.Core.Interfaces.Services;

// Cross-module seams (O10-style, config-gated). CRM never hard-depends on Finance / Operations;
// these interfaces are the only touch-points. NoOp implementations run until the real HTTP clients
// are wired (Finance:Enabled / Operations:Enabled config flags).

public record DealInvoiceRequest(string DealId, string DealNumber, string? CustomerId, string? CustomerName, decimal Amount, string Currency);
public record FinanceInvoiceResult(bool Success, string? InvoiceId, string Message);

/// <summary>P6 seam — on deal close, ask Finance to create a draft invoice for the contract value.</summary>
public interface IFinanceGateway
{
    Task<FinanceInvoiceResult> RaiseDealInvoiceAsync(DealInvoiceRequest request, CancellationToken ct = default);
}

public record DealProjectRequest(string DealId, string DealNumber, string? CustomerId, string? CustomerName, string Name, decimal ContractValue, DateTime? Start, DateTime? End);
public record ProjectCreationResult(bool Success, string? ProjectId, string Message);

/// <summary>P6 seam — one-click create the Module 5 project from a won deal (Project.ClientId ← customer).</summary>
public interface IProjectGateway
{
    Task<ProjectCreationResult> CreateProjectFromDealAsync(DealProjectRequest request, CancellationToken ct = default);
}
