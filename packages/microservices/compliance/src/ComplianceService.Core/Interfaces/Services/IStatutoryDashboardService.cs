using ComplianceService.Core.DTOs.Statutory;

namespace ComplianceService.Core.Interfaces.Services;

public interface IStatutoryDashboardService
{
    /// <summary>"Green" (&gt;60d), "Amber" (30-60d), or "Red" (&lt;30d or overdue) — STAT-010.</summary>
    string ComputeRag(DateTime dueDate);
    Task<StatutoryDashboardDto> ComputeAsync();
}
