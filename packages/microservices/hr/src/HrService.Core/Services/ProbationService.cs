using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Probation;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H2 (P3 + P33) — probation milestones and fixed-term contract renewal.
/// <para><b>The sweep only ever raises records; it never decides anything.</b> At Day 90 and Day 180 from the
/// hire date it opens a <see cref="ProbationReview"/> for HR and the line manager to complete, and for
/// fixed-term staff it opens a <see cref="ContractRenewalAlert"/> and stamps the 30-day and 7-day warnings.
/// Confirmation, extension, termination, renewal and conversion are all deliberate human actions.</para>
/// <para><b>Idempotence comes from the data, not from remembering.</b> A milestone review is unique on
/// (employee, type, scheduled date) and the alert stamps are one-way, so running the sweep twice — or twice a
/// day for a month — raises nothing new and re-notifies nobody.</para>
/// <para>Terminated probation and lapsed contracts hand off to separation (H10, Process 21), which does not
/// exist yet: the outcome is recorded and surfaced, and the employee is left for that phase to process.</para>
/// </summary>
public class ProbationService(
    IGenericRepository<Employee> employees,
    IGenericRepository<ProbationReview> reviews,
    IGenericRepository<ContractRenewalAlert> alerts,
    IGenericRepository<HrAuditLog> audit,
    IHrAlertGateway notifier) : IProbationService
{
    /// <summary>QSL policy: probation is 6 months, with a review at 3.</summary>
    private const int ThreeMonthDays = 90;
    private const int SixMonthDays = 180;
    private const int Alert30Days = 30;
    private const int Alert7Days = 7;

    public async Task<ProbationSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var allReviews = await reviews.Query().AsNoTracking().ToListAsync();
        var allAlerts = await alerts.Query().AsNoTracking().ToListAsync();
        var staff = await employees.Query().AsNoTracking()
            .Select(e => new { e.Status, e.EmploymentType, e.ContractEndDate }).ToListAsync();

        return new ProbationSummaryDto
        {
            OnProbation = staff.Count(e => e.Status == EmploymentStatus.OnProbation),
            ReviewsPending = allReviews.Count(r => r.Outcome == ProbationOutcome.Pending),
            ReviewsOverdue = allReviews.Count(r => r.Outcome == ProbationOutcome.Pending && r.ScheduledDate < now),
            // Counted per EMPLOYEE, not per review row: confirming someone also auto-closes their other
            // outstanding milestone as Confirmed, so counting rows would report one person as two.
            ConfirmedThisYear = allReviews
                .Where(r => r.Outcome == ProbationOutcome.Confirmed && r.CompletedDate >= yearStart)
                .Select(r => r.EmployeeId).Distinct().Count(),
            ExtendedThisYear = allReviews
                .Where(r => r.Outcome == ProbationOutcome.Extended && r.CompletedDate >= yearStart)
                .Select(r => r.EmployeeId).Distinct().Count(),
            TerminationRecommended = allReviews
                .Where(r => r.Outcome == ProbationOutcome.Terminated)
                .Select(r => r.EmployeeId).Distinct().Count(),

            FixedTermStaff = staff.Count(e => IsFixedTerm(e.EmploymentType) && !IsGone(e.Status)),
            ContractsExpiringIn30Days = staff.Count(e => IsFixedTerm(e.EmploymentType) && !IsGone(e.Status)
                && e.ContractEndDate != null && e.ContractEndDate > now && e.ContractEndDate <= now.AddDays(Alert30Days)),
            ContractsExpiringIn7Days = staff.Count(e => IsFixedTerm(e.EmploymentType) && !IsGone(e.Status)
                && e.ContractEndDate != null && e.ContractEndDate > now && e.ContractEndDate <= now.AddDays(Alert7Days)),
            ContractsExpiredUnactioned = allAlerts.Count(a => a.Outcome == ContractRenewalOutcome.Pending && a.ContractEndDate <= now),
            AwaitingRenewalDecision = allAlerts.Count(a => a.Outcome == ContractRenewalOutcome.Pending),
        };
    }

    // ── Probation ──
    public async Task<List<ProbationReviewDto>> ListReviewsAsync(string? outcome, string? employeeId)
    {
        var q = reviews.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(outcome) && Enum.TryParse<ProbationOutcome>(outcome, true, out var o))
            q = q.Where(r => r.Outcome == o);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(r => r.EmployeeId == employeeId);
        var list = await q.OrderBy(r => r.ScheduledDate).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ProbationActionResult> RecordOutcomeAsync(string reviewId, RecordProbationOutcomeDto dto, string userId, string? userName)
    {
        var review = await reviews.GetByIdAsync(reviewId);
        if (review is null) return Err("Probation review not found.");
        if (review.Outcome != ProbationOutcome.Pending)
            return Err($"This review is already recorded as {review.Outcome}.");
        if (!Enum.TryParse<ProbationOutcome>(dto.Outcome, true, out var outcome) || outcome == ProbationOutcome.Pending)
            return Err("The outcome must be Confirmed, Extended or Terminated.");

        var employee = await employees.GetByIdAsync(review.EmployeeId);
        if (employee is null) return Err("The employee on this review no longer exists.");

        var now = DateTime.UtcNow;
        review.Outcome = outcome;
        review.CompletedDate = now;
        review.ReviewerId = userId;
        review.ReviewerName = userName;
        review.Notes = dto.Notes;

        var message = string.Empty;
        switch (outcome)
        {
            case ProbationOutcome.Confirmed:
                employee.Status = EmploymentStatus.Active;
                employee.ConfirmationDate = now;
                Touch(employee, userId);
                await employees.UpdateAsync(employee);
                // Any other pending milestone for this person is moot once they are confirmed.
                foreach (var other in await reviews.Query()
                             .Where(r => r.EmployeeId == employee.Id && r.Id != review.Id && r.Outcome == ProbationOutcome.Pending)
                             .ToListAsync())
                {
                    other.Outcome = ProbationOutcome.Confirmed;
                    other.CompletedDate = now;
                    other.Notes = $"Closed automatically — employee confirmed on {now:yyyy-MM-dd}.";
                    Touch(other, userId);
                    await reviews.UpdateAsync(other);
                }
                await LogAsync(employee.Id, HrAuditAction.ProbationConfirmed,
                    $"{employee.EmployeeNumber} confirmed in post after the {Label(review.ReviewType)} review.", userId, userName);
                message = $"{employee.FullName} confirmed — status is now Active.";
                break;

            case ProbationOutcome.Extended:
                if (dto.ExtendedToDate is null)
                    return Err("Extending probation needs the date the follow-up review falls due.");
                if (dto.ExtendedToDate <= now)
                    return Err("The follow-up review date must be in the future.");
                review.ExtendedToDate = dto.ExtendedToDate;
                // Design note: an extension is a NEW review record with new dates, not a reopened one.
                var followUp = await reviews.CreateAsync(new ProbationReview
                {
                    EmployeeId = employee.Id,
                    EmployeeNumber = employee.EmployeeNumber,
                    EmployeeName = employee.FullName,
                    ReviewType = ProbationReviewType.Extended,
                    ScheduledDate = dto.ExtendedToDate.Value,
                    Outcome = ProbationOutcome.Pending,
                    Notes = $"Follow-up to the {Label(review.ReviewType)} review extended on {now:yyyy-MM-dd}.",
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });
                review.FollowUpReviewId = followUp.Id;
                await LogAsync(employee.Id, HrAuditAction.ProbationExtended,
                    $"{employee.EmployeeNumber} probation extended — follow-up review due {dto.ExtendedToDate:yyyy-MM-dd}.", userId, userName);
                message = $"Probation extended — a follow-up review is scheduled for {dto.ExtendedToDate:yyyy-MM-dd}.";
                break;

            case ProbationOutcome.Terminated:
                if (string.IsNullOrWhiteSpace(dto.Notes))
                    return Err("Terminating probation requires a reason on the record.");
                // Separation itself is H10 (Process 21) — record the decision and leave the employee for it.
                await LogAsync(employee.Id, HrAuditAction.ProbationTerminationRecommended,
                    $"{employee.EmployeeNumber} probation terminated at the {Label(review.ReviewType)} review: {dto.Notes} Awaiting separation processing.", userId, userName);
                message = $"Termination recorded for {employee.FullName}. Separation and final dues are handled by the separation process, which is not yet built — the employee's status is unchanged for now.";
                break;
        }

        Touch(review, userId);
        await reviews.UpdateAsync(review);
        return new ProbationActionResult(outcome.ToString(), message, review.Id);
    }

    // ── Contract renewal ──
    public async Task<List<ContractRenewalAlertDto>> ListContractAlertsAsync(string? outcome)
    {
        var q = alerts.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(outcome) && Enum.TryParse<ContractRenewalOutcome>(outcome, true, out var o))
            q = q.Where(a => a.Outcome == o);
        var list = await q.OrderBy(a => a.ContractEndDate).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ProbationActionResult> RenewContractAsync(string alertId, RenewContractDto dto, string userId)
    {
        var (alert, employee, error) = await LoadAlertAsync(alertId);
        if (error is not null) return error;

        if (dto.NewContractEndDate <= alert!.ContractEndDate)
            return Err($"The new end date must be later than the current one ({alert.ContractEndDate:yyyy-MM-dd}).");

        employee!.ContractEndDate = dto.NewContractEndDate;
        employee.ContractStartDate = alert.ContractEndDate;   // the new term starts where the old one ended
        Touch(employee, userId);
        await employees.UpdateAsync(employee);

        Close(alert, ContractRenewalOutcome.Renewed, userId, dto.Notes);
        await alerts.UpdateAsync(alert);

        await LogAsync(employee.Id, HrAuditAction.ContractRenewed,
            $"{employee.EmployeeNumber} contract renewed to {dto.NewContractEndDate:yyyy-MM-dd}.", userId, null);
        return new ProbationActionResult("Renewed",
            $"Contract renewed to {dto.NewContractEndDate:yyyy-MM-dd}. Upload the signed contract to the document vault.", alert.Id);
    }

    public async Task<ProbationActionResult> ConvertToPermanentAsync(string alertId, ConvertToPermanentDto dto, string userId)
    {
        var (alert, employee, error) = await LoadAlertAsync(alertId);
        if (error is not null) return error;

        employee!.EmploymentType = EmploymentType.Permanent;
        employee.ContractEndDate = null;                      // permanent staff have no expiry to chase
        Touch(employee, userId);
        await employees.UpdateAsync(employee);

        Close(alert!, ContractRenewalOutcome.ConvertedToPermanent, userId, dto.Notes);
        await alerts.UpdateAsync(alert!);

        // Design note: converting closes every future renewal alert for that employee.
        foreach (var other in await alerts.Query()
                     .Where(a => a.EmployeeId == employee.Id && a.Id != alert!.Id && a.Outcome == ContractRenewalOutcome.Pending)
                     .ToListAsync())
        {
            Close(other, ContractRenewalOutcome.ConvertedToPermanent, userId, "Closed automatically — employee converted to permanent.");
            await alerts.UpdateAsync(other);
        }

        await LogAsync(employee.Id, HrAuditAction.ContractConvertedToPermanent,
            $"{employee.EmployeeNumber} converted to permanent employment; renewal alerts closed.", userId, null);
        return new ProbationActionResult("ConvertedToPermanent",
            $"{employee.FullName} is now permanent — renewal alerts closed.", alert!.Id);
    }

    public async Task<ProbationActionResult> LetContractExpireAsync(string alertId, LetContractExpireDto dto, string userId)
    {
        var (alert, employee, error) = await LoadAlertAsync(alertId);
        if (error is not null) return error;

        Close(alert!, ContractRenewalOutcome.Expired, userId, dto.Notes);
        await alerts.UpdateAsync(alert!);

        await LogAsync(employee!.Id, HrAuditAction.ContractExpired,
            $"{employee.EmployeeNumber} contract allowed to expire on {alert!.ContractEndDate:yyyy-MM-dd}. Awaiting separation processing.", userId, null);
        return new ProbationActionResult("Expired",
            $"Recorded — {employee.FullName}'s contract will lapse on {alert.ContractEndDate:yyyy-MM-dd}. Final dues are handled by the separation process, which is not yet built.", alert.Id);
    }

    // ── The daily sweep ──
    public async Task<MilestoneSweepResultDto> RunMilestoneSweepAsync(string? tenantSchema, string userId)
    {
        var now = DateTime.UtcNow;
        var result = new MilestoneSweepResultDto();

        // ── Probation milestones (P3 steps 3.1–3.2) ──
        var probationers = await employees.Query()
            .Where(e => e.Status == EmploymentStatus.OnProbation).ToListAsync();

        foreach (var e in probationers)
        {
            var daysSinceHire = (now - e.HireDate).TotalDays;
            foreach (var (type, dayMark) in new[]
                     {
                         (ProbationReviewType.ThreeMonth, ThreeMonthDays),
                         (ProbationReviewType.SixMonth, SixMonthDays),
                     })
            {
                if (daysSinceHire < dayMark) continue;
                var scheduled = e.HireDate.AddDays(dayMark).Date;
                if (await reviews.Query().AnyAsync(r => r.EmployeeId == e.Id && r.ReviewType == type && r.ScheduledDate == scheduled))
                    continue;

                var review = await reviews.CreateAsync(new ProbationReview
                {
                    EmployeeId = e.Id,
                    EmployeeNumber = e.EmployeeNumber,
                    EmployeeName = e.FullName,
                    ReviewType = type,
                    ScheduledDate = scheduled,
                    Outcome = ProbationOutcome.Pending,
                    AlertSentAt = now,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });
                result.ProbationReviewsRaised++;

                // The 3-month review is HR + line manager; the 6-month confirmation escalates to the MD.
                var isConfirmation = type == ProbationReviewType.SixMonth;
                await NotifyAsync(tenantSchema, "ProbationReview", isConfirmation ? "Critical" : "Warning",
                    isConfirmation
                        ? $"6-month confirmation due — {e.FullName}"
                        : $"3-month probation review due — {e.FullName}",
                    $"{e.EmployeeNumber} ({e.JobTitle ?? "—"}) reached day {dayMark} of probation on {scheduled:d}. "
                    + (isConfirmation ? "HR and the MD must record the confirmation decision." : "HR and the line manager should complete the review."),
                    isConfirmation ? "hr.approve" : "hr.manager", e.UserId);

                await LogAsync(e.Id, HrAuditAction.ProbationReviewRaised,
                    $"{Label(type)} probation review raised for {e.EmployeeNumber} (due {scheduled:yyyy-MM-dd}).", userId, null);
                _ = review;
            }
        }

        // ── Follow-up reviews created by an extension ──
        // An extension books its own date, which is past day 180, so the milestone loop above will never
        // see it. Without this pass the follow-up would sit pending and nobody would ever be told it fell due.
        var dueFollowUps = await reviews.Query()
            .Where(r => r.Outcome == ProbationOutcome.Pending
                     && r.ReviewType == ProbationReviewType.Extended
                     && r.AlertSentAt == null
                     && r.ScheduledDate <= now)
            .ToListAsync();

        foreach (var r in dueFollowUps)
        {
            r.AlertSentAt = now;
            Touch(r, userId);
            await reviews.UpdateAsync(r);
            result.FollowUpReviewsAlerted++;

            var subject = await employees.Query().AsNoTracking()
                .Where(e => e.Id == r.EmployeeId).Select(e => e.UserId).FirstOrDefaultAsync();
            await NotifyAsync(tenantSchema, "ProbationReview", "Critical",
                $"Extended probation review due — {r.EmployeeName} (due {r.ScheduledDate:yyyy-MM-dd})",
                $"{r.EmployeeNumber}'s extended probation reached its follow-up date of {r.ScheduledDate:d}. "
                + "A confirmation or termination decision is now outstanding.",
                "hr.approve", subject);
        }

        // ── Fixed-term contract expiry (P3 step 3.4 / P33 steps 33.1–33.4) ──
        var fixedTerm = await employees.Query()
            .Where(e => e.ContractEndDate != null
                     && (e.EmploymentType == EmploymentType.FixedTerm || e.EmploymentType == EmploymentType.Contract)
                     && e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
            .ToListAsync();

        foreach (var e in fixedTerm)
        {
            var end = e.ContractEndDate!.Value.Date;
            var daysLeft = (end - now.Date).TotalDays;
            // Only start tracking once the 30-day window opens — no point opening alerts a year out.
            if (daysLeft > Alert30Days) continue;

            var alert = await alerts.Query().FirstOrDefaultAsync(a => a.EmployeeId == e.Id && a.ContractEndDate == end);
            if (alert is null)
            {
                alert = await alerts.CreateAsync(new ContractRenewalAlert
                {
                    EmployeeId = e.Id,
                    EmployeeNumber = e.EmployeeNumber,
                    EmployeeName = e.FullName,
                    ContractEndDate = end,
                    Outcome = ContractRenewalOutcome.Pending,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                });
                result.ContractAlertsOpened++;
            }
            // A decision already recorded means nothing more to chase for this term.
            if (alert.Outcome != ContractRenewalOutcome.Pending) continue;

            var changed = false;
            if (daysLeft <= Alert30Days && alert.Alert30SentAt is null)
            {
                alert.Alert30SentAt = now;
                changed = true;
                result.Alert30Fired++;
                await NotifyAsync(tenantSchema, "ContractRenewal", "Warning",
                    $"Contract renewal decision needed — {e.FullName} (ends {end:yyyy-MM-dd})",
                    $"{e.EmployeeNumber}'s {e.EmploymentType} contract ends on {end:d}, in {Math.Max(0, (int)daysLeft)} day(s). HR and the line manager should decide: renew, convert to permanent, or let it expire.",
                    "hr.manager", e.UserId);
            }
            if (daysLeft <= Alert7Days && alert.Alert7SentAt is null)
            {
                alert.Alert7SentAt = now;
                changed = true;
                result.Alert7Fired++;
                await NotifyAsync(tenantSchema, "ContractRenewal", "Critical",
                    $"FINAL NOTICE: contract expires in {Math.Max(0, (int)daysLeft)} day(s) — {e.FullName} (ends {end:yyyy-MM-dd})",
                    $"{e.EmployeeNumber}'s contract ends on {end:d} and no decision has been recorded. This now needs the MD.",
                    "hr.approve", e.UserId);
            }
            if (daysLeft < 0 && alert.ExpiredAlertSentAt is null)
            {
                alert.ExpiredAlertSentAt = now;
                changed = true;
                result.ExpiredFlagged++;
                await NotifyAsync(tenantSchema, "ContractRenewal", "Critical",
                    $"Contract LAPSED with no decision — {e.FullName} (ended {end:yyyy-MM-dd})",
                    $"{e.EmployeeNumber}'s contract ended on {end:d} and no renewal decision was recorded. This must go to separation and final dues.",
                    "hr.approve", e.UserId);
            }

            if (changed)
            {
                Touch(alert, userId);
                await alerts.UpdateAsync(alert);
                await LogAsync(e.Id, HrAuditAction.ContractAlertRaised,
                    $"Contract expiry alert for {e.EmployeeNumber} — ends {end:yyyy-MM-dd}, {(int)daysLeft} day(s) remaining.", userId, null);
            }
        }

        result.Message =
            $"{result.ProbationReviewsRaised} probation review(s) raised, {result.FollowUpReviewsAlerted} follow-up review(s) now due, "
            + $"{result.ContractAlertsOpened} contract alert(s) opened, "
            + $"{result.Alert30Fired} 30-day and {result.Alert7Fired} 7-day warning(s) fired, {result.ExpiredFlagged} lapsed contract(s) flagged.";
        return result;
    }

    // ── Helpers ──
    private async Task<(ContractRenewalAlert? Alert, Employee? Employee, ProbationActionResult? Error)> LoadAlertAsync(string alertId)
    {
        var alert = await alerts.GetByIdAsync(alertId);
        if (alert is null) return (null, null, Err("Contract renewal alert not found."));
        if (alert.Outcome != ContractRenewalOutcome.Pending)
            return (null, null, Err($"This contract has already been recorded as {alert.Outcome}."));
        var employee = await employees.GetByIdAsync(alert.EmployeeId);
        if (employee is null) return (null, null, Err("The employee on this alert no longer exists."));
        return (alert, employee, null);
    }

    private static void Close(ContractRenewalAlert alert, ContractRenewalOutcome outcome, string userId, string? notes)
    {
        alert.Outcome = outcome;
        alert.ActionedBy = userId;
        alert.ActionedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(notes))
            alert.Notes = string.IsNullOrWhiteSpace(alert.Notes) ? notes : $"{alert.Notes} {notes}";
        Touch(alert, userId);
    }

    /// <summary>
    /// Raises a tenant alert through ticketing.
    /// <para><b>Every tier must have a distinct title.</b> Ticketing drops an incoming alert when an OPEN one
    /// already exists with the same (tenant, source, title) — that dedupe is what stops its own background
    /// scanners re-alerting the same open condition. Our escalations are separate alerts, not repeats, so a
    /// shared title makes the later tier vanish silently: the 30-day and 7-day contract warnings originally
    /// both read "Contract expires in N days — Name", and because nobody acknowledges the 30-day one, the
    /// 7-day escalation to the MD never arrived while HR's own Alert7SentAt stamp said it had. Titles also
    /// carry the contract end / review date so a re-issued term cannot collide with the previous one's
    /// still-open alert.</para>
    /// </summary>
    private async Task NotifyAsync(string? schema, string source, string severity, string title, string message,
        string? permission, string? assignedToUserId)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;   // no tenant context (e.g. a direct unit call)
        await notifier.CreateAlertAsync(schema, source, severity, title, message, permission, assignedToUserId);
    }

    private static ProbationReviewDto ToDto(ProbationReview r)
    {
        var now = DateTime.UtcNow;
        return new ProbationReviewDto
        {
            Id = r.Id, EmployeeId = r.EmployeeId, EmployeeNumber = r.EmployeeNumber, EmployeeName = r.EmployeeName,
            ReviewType = r.ReviewType.ToString(), ScheduledDate = r.ScheduledDate, CompletedDate = r.CompletedDate,
            Outcome = r.Outcome.ToString(), ReviewerId = r.ReviewerId, ReviewerName = r.ReviewerName, Notes = r.Notes,
            ExtendedToDate = r.ExtendedToDate, FollowUpReviewId = r.FollowUpReviewId, AlertSentAt = r.AlertSentAt,
            IsOverdue = r.Outcome == ProbationOutcome.Pending && r.ScheduledDate < now,
            DaysSinceScheduled = (int)(now.Date - r.ScheduledDate.Date).TotalDays,
        };
    }

    private static ContractRenewalAlertDto ToDto(ContractRenewalAlert a)
    {
        var now = DateTime.UtcNow;
        return new ContractRenewalAlertDto
        {
            Id = a.Id, EmployeeId = a.EmployeeId, EmployeeNumber = a.EmployeeNumber, EmployeeName = a.EmployeeName,
            ContractEndDate = a.ContractEndDate, Alert30SentAt = a.Alert30SentAt, Alert7SentAt = a.Alert7SentAt,
            ExpiredAlertSentAt = a.ExpiredAlertSentAt, Outcome = a.Outcome.ToString(),
            ActionedBy = a.ActionedBy, ActionedAt = a.ActionedAt, Notes = a.Notes,
            DaysToExpiry = (int)(a.ContractEndDate.Date - now.Date).TotalDays,
            IsExpired = a.ContractEndDate.Date < now.Date,
        };
    }

    private static string Label(ProbationReviewType type) => type switch
    {
        ProbationReviewType.ThreeMonth => "3-month",
        ProbationReviewType.SixMonth => "6-month",
        _ => "extended",
    };

    private static bool IsFixedTerm(EmploymentType t) => t is EmploymentType.FixedTerm or EmploymentType.Contract;
    private static bool IsGone(EmploymentStatus s) => s is EmploymentStatus.Resigned or EmploymentStatus.Terminated;

    private static ProbationActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string employeeId, HrAuditAction action, string detail, string userId, string? userName)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = "Employee", EntityId = employeeId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
