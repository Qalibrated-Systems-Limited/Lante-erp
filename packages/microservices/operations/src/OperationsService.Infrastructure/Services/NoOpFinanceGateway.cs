using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O3 — default finance seam: a config-gated no-op. Until finance-service is wired, invoice + revenue
/// recognition requests are logged (not sent) so the milestone sign-off flow is never blocked. When
/// <c>Finance:Enabled=true</c> a real client should replace this; the no-op logs so the gap is visible.
/// Mirrors the O1 <see cref="ICrmCustomerDirectory"/> no-op pattern.
/// </summary>
public class NoOpFinanceGateway(
    IConfiguration config,
    ILogger<NoOpFinanceGateway> logger) : IFinanceGateway
{
    public Task RaiseMilestoneInvoiceAsync(MilestoneInvoiceRequest request, CancellationToken ct = default)
    {
        if (config.GetValue("Finance:Enabled", false))
            logger.LogWarning("Finance:Enabled=true but no finance gateway is wired — invoice for milestone {MilestoneId} ({Amount:0.00}) not sent.",
                request.MilestoneId, request.Amount);
        else
            logger.LogInformation("[Finance stub] Milestone invoice: project {ProjectId} milestone '{Title}' amount {Amount:0.00} (client {Client}).",
                request.ProjectId, request.MilestoneTitle, request.Amount, request.ClientName ?? "n/a");
        return Task.CompletedTask;
    }

    public Task ReportRevenueRecognitionAsync(RevenueRecognitionRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("[Finance stub] IFRS-15 revenue recognition: project {ProjectId} milestone {MilestoneId} {Pct}% → recognize {Amount:0.00}.",
            request.ProjectId, request.MilestoneId, request.PercentComplete, request.RecognizedAmount);
        return Task.CompletedTask;
    }

    public Task PostTimesheetLabourAsync(TimesheetLabourPosting posting, CancellationToken ct = default)
    {
        logger.LogInformation("[Finance stub] Labour journal: project {ProjectId} employee {Emp} week-ending {Week:yyyy-MM-dd} — {Hours}h (+{OT}h OT) = {Amount:0.00}.",
            posting.ProjectId, posting.EmployeeId, posting.WeekEndDate, posting.Hours, posting.OvertimeHours, posting.Amount);
        return Task.CompletedTask;
    }

    public Task RaiseVariationInvoiceAsync(VariationInvoiceRequest request, CancellationToken ct = default)
    {
        logger.LogInformation("[Finance stub] Variation invoice: project {ProjectId} VO {Number} amount {Amount:0.00}.",
            request.ProjectId, request.VariationOrderNumber, request.Amount);
        return Task.CompletedTask;
    }
}
