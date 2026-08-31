using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H2 (HR-006, P3 step 3.4 / P33) — CONTRACT_RENEWAL_ALERT. One row per fixed-term contract period, tracking
/// the 30-day and 7-day expiry warnings and how the contract was ultimately resolved.
/// <para>The two <c>Alert*SentAt</c> stamps are what stop a daily scheduler re-firing the same warning
/// (design note), and the row is keyed on employee + contract end date so re-issuing a contract with a new end
/// date opens a fresh alert rather than mutating the history of the previous term.</para>
/// <para>The escalation differs by window on purpose: at 30 days HR and the line manager are told; at 7 days it
/// goes to HR and the MD.</para>
/// </summary>
public class ContractRenewalAlert : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public DateTime ContractEndDate { get; set; }

    public DateTime? Alert30SentAt { get; set; }
    public DateTime? Alert7SentAt { get; set; }
    /// <summary>Stamped once the contract has lapsed with no decision recorded (P33 step 33.4 — hands off to
    /// H10 separation).</summary>
    public DateTime? ExpiredAlertSentAt { get; set; }

    public ContractRenewalOutcome Outcome { get; set; } = ContractRenewalOutcome.Pending;
    public string? ActionedBy { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
}
