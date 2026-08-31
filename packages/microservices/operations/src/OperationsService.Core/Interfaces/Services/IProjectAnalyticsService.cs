using OperationsService.Core.DTOs.Analytics;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// PR4a — earned value, the S-curve and the portfolio rollup. Everything is derived from the baseline
/// (PR1), the actuals (PR2) and controlled re-baselining (PR3); nothing new is stored.
/// </summary>
public interface IProjectAnalyticsService
{
    /// <summary>EVM metrics plus the weekly S-curve for one project.</summary>
    Task<ProjectEvmDto> GetProjectEvmAsync(string projectId, DateTime? asOf = null);

    /// <summary>The same metrics summed across live projects, worst forecast overrun first.</summary>
    Task<PortfolioEvmDto> GetPortfolioEvmAsync(DateTime? asOf = null);
}
