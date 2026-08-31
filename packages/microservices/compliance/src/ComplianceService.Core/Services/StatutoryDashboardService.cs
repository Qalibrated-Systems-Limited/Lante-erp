using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Core.Services;

public class StatutoryDashboardService(
    IComplianceCrudService<StatutoryDeadline> deadlines,
    IComplianceCrudService<StatutoryObligation> obligations,
    IComplianceCrudService<RegulatoryLicence> licences,
    IComplianceCrudService<AnnualReturn> annualReturns,
    IComplianceCrudService<TaxComplianceCert> tccs) : IStatutoryDashboardService
{
    public string ComputeRag(DateTime dueDate) => Rag(dueDate, DateTime.UtcNow);

    /// <summary>
    /// STAT-010's banding: GREEN over 60 days out, AMBER 30-60, RED under 30 or overdue.
    ///
    /// <para><b>Whole calendar days, not fractional.</b> This used <c>TotalDays</c>, a double, so the
    /// band depended on the time of day the dashboard was opened: a deadline exactly 30 calendar days
    /// out read Amber at 00:00 and Red from 00:01 onwards — Amber for one instant of the day the spec
    /// says it should be Amber throughout. Two replicas whose clocks differ near a boundary could also
    /// disagree about the same deadline, which is the problem fleet-service's ExpiryBand was written
    /// to avoid and the reason StatutoryCalendarRules counts whole months.</para>
    ///
    /// <para><c>now</c> is a parameter so the boundaries can be tested rather than reasoned about.</para>
    /// </summary>
    internal static string Rag(DateTime dueDate, DateTime now)
    {
        var daysLeft = (dueDate.Date - now.Date).Days;
        if (daysLeft < 30) return "Red";
        if (daysLeft <= 60) return "Amber";
        return "Green";
    }

    /// <summary>
    /// A deadline is overdue only once its due DATE has passed.
    ///
    /// <para>Previously <c>DueDate &lt; now</c>, comparing instants: a return due today at midnight
    /// showed as overdue from 00:00:01, though the whole day remains available to file it. Same
    /// reading as the expiry states in the fleet UI — an expiry is a day, not a moment.</para>
    /// </summary>
    internal static bool IsPastDue(DateTime dueDate, DateTime now) => dueDate.Date < now.Date;

    public async Task<StatutoryDashboardDto> ComputeAsync()
    {
        var now = DateTime.UtcNow;
        var items = new List<StatutoryCalendarItemDto>();

        var obligationNames = (await obligations.GetAllAsync()).ToDictionary(o => o.Id, o => o.Name);
        var pendingDeadlines = await deadlines.FindAsync(d => d.Status != Enums.StatutoryDeadlineStatus.Filed);
        items.AddRange(pendingDeadlines.Select(d => new StatutoryCalendarItemDto
        {
            SourceType = "Obligation",
            SourceId = d.Id,
            Title = obligationNames.GetValueOrDefault(d.ObligationId, "Statutory Obligation"),
            DueDate = d.DueDate,
            Rag = ComputeRag(d.DueDate),
            IsOverdue = IsPastDue(d.DueDate, now),
            OwnerName = d.OwnerName,
        }));

        var allLicences = await licences.GetAllAsync();
        items.AddRange(allLicences.Select(l => new StatutoryCalendarItemDto
        {
            SourceType = "Licence",
            SourceId = l.Id,
            Title = $"{l.Authority} licence renewal",
            DueDate = l.ExpiryDate,
            Rag = ComputeRag(l.ExpiryDate),
            IsOverdue = IsPastDue(l.ExpiryDate, now),
        }));

        var pendingReturns = await annualReturns.FindAsync(r => r.Status != Enums.AnnualReturnStatus.Filed);
        items.AddRange(pendingReturns.Select(r => new StatutoryCalendarItemDto
        {
            SourceType = "AnnualReturn",
            SourceId = r.Id,
            Title = $"Annual return — {r.Year}",
            DueDate = r.DueDate,
            Rag = ComputeRag(r.DueDate),
            IsOverdue = IsPastDue(r.DueDate, now),
        }));

        var currentTccs = await tccs.FindAsync(t => t.Status != Enums.TccStatus.Expired || t.ExpiryDate >= now.AddDays(-365));
        items.AddRange(currentTccs.Select(t => new StatutoryCalendarItemDto
        {
            SourceType = "Tcc",
            SourceId = t.Id,
            Title = "Tax Compliance Certificate renewal",
            DueDate = t.ExpiryDate,
            Rag = ComputeRag(t.ExpiryDate),
            IsOverdue = IsPastDue(t.ExpiryDate, now),
        }));

        var sorted = items.OrderBy(i => i.DueDate).ToList();

        return new StatutoryDashboardDto
        {
            GreenCount = sorted.Count(i => i.Rag == "Green"),
            AmberCount = sorted.Count(i => i.Rag == "Amber"),
            RedCount = sorted.Count(i => i.Rag == "Red"),
            UpcomingDeadlines = sorted,
        };
    }
}
