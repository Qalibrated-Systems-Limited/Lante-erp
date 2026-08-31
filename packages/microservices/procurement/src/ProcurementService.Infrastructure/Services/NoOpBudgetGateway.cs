using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>Config-gated no-op budget seam. Returns "available" so a PR is never blocked while the Finance
/// budget check isn't wired (FinanceService:BaseUrl unset). Logs when enabled so the gap is visible.</summary>
public class NoOpBudgetGateway(IConfiguration config, ILogger<NoOpBudgetGateway> logger) : IBudgetGateway
{
    public Task<BudgetCheckResult> CheckAsync(string? budgetId, decimal amount, CancellationToken ct = default)
    {
        var enabled = config.GetValue("Finance:Enabled", false);
        if (enabled)
            logger.LogWarning("Finance:Enabled=true but no budget gateway is wired — passing budget {Budget} amount {Amt}.", budgetId, amount);
        return Task.FromResult(new BudgetCheckResult(true, null, "Budget check disabled — passed."));
    }
}
