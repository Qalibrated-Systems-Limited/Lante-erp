using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;
using ReportingService.Core.Interfaces;

namespace ReportingService.Infrastructure.Services;

// Report #5: project-profitability. If projectId is supplied, fetches just that project's budget
// summary. Otherwise pages through every project in OperationsService and fetches each one's
// budget summary in parallel (bounded concurrency) — a portfolio-wide rollup plus a totals row.
public class ProjectProfitabilityReportService(
    IOperationsServiceClient operations,
    ILogger<ProjectProfitabilityReportService> logger) : IProjectProfitabilityReportService
{
    private const int PageSize = 50;
    private const int MaxConcurrency = 8;

    public async Task<ProjectProfitabilityReportDto> GetAsync(string? projectId)
    {
        var report = new ProjectProfitabilityReportDto();

        if (!string.IsNullOrWhiteSpace(projectId))
        {
            try
            {
                var summary = await operations.GetProjectBudgetAsync(projectId);
                if (summary != null)
                {
                    report.Rows.Add(ToRow(projectId, null, summary));
                }
                else
                {
                    report.Warnings.Add($"No budget summary returned for project {projectId}.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch budget summary for project {ProjectId}", projectId);
                report.Warnings.Add($"Failed to fetch budget summary for project {projectId}.");
            }

            report.Totals = Sum(report.Rows);
            return report;
        }

        var allProjects = new List<ProjectReadDto>();
        var page = 1;
        while (true)
        {
            PagedResult<ProjectReadDto>? pageResult;
            try
            {
                pageResult = await operations.GetProjectsPageAsync(page, PageSize);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch projects page {Page}", page);
                report.Warnings.Add("Failed to fetch the full project list from OperationsService; results may be incomplete.");
                break;
            }

            if (pageResult == null || pageResult.Items.Count == 0) break;
            allProjects.AddRange(pageResult.Items);
            if (page >= pageResult.TotalPages || pageResult.Items.Count < PageSize) break;
            page++;
        }

        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var rowTasks = allProjects.Select(async p =>
        {
            await semaphore.WaitAsync();
            try
            {
                var summary = await operations.GetProjectBudgetAsync(p.Id);
                return summary != null ? ToRow(p.Id, p.Name, summary) : null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch budget summary for project {ProjectId}", p.Id);
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var rows = await Task.WhenAll(rowTasks);
        var successRows = rows.Where(r => r != null).Select(r => r!).ToList();
        if (successRows.Count < allProjects.Count)
            report.Warnings.Add($"Budget summary failed for {allProjects.Count - successRows.Count} of {allProjects.Count} project(s).");

        report.Rows = successRows;
        report.Totals = Sum(successRows);
        return report;
    }

    private static ProjectProfitabilityRowDto ToRow(string projectId, string? name, BudgetSummaryDto s) => new()
    {
        ProjectId = projectId,
        ProjectName = name,
        PlannedBudget = s.PlannedBudget,
        TotalEstimated = s.TotalEstimated,
        ActualCost = s.ActualCost,
        Remaining = s.Remaining,
        UtilizationPercent = s.UtilizationPercent,
    };

    private static ProjectProfitabilityRowDto Sum(List<ProjectProfitabilityRowDto> rows)
    {
        var plannedTotal = rows.Sum(r => r.PlannedBudget);
        var actualTotal = rows.Sum(r => r.ActualCost);
        return new ProjectProfitabilityRowDto
        {
            ProjectId = "TOTAL",
            ProjectName = "Portfolio Total",
            PlannedBudget = plannedTotal,
            TotalEstimated = rows.Sum(r => r.TotalEstimated),
            ActualCost = actualTotal,
            Remaining = rows.Sum(r => r.Remaining),
            UtilizationPercent = plannedTotal > 0 ? Math.Round(actualTotal / plannedTotal * 100, 2) : 0,
        };
    }
}
