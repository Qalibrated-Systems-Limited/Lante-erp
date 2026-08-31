using HrService.Core.DTOs.Org;

namespace HrService.Core.Interfaces.Services;

// Cross-module seams owned by HR. Each ships a config-gated no-op default so call sites are wired and inert
// until the matching *Service:BaseUrl is set. Real adapters mint a per-schema service token and call the
// sibling service over HTTP (the pattern established across ticketing/crm/procurement).

/// <summary>Outcome of creating the user-service login account for a new hire (P1 design note).</summary>
public record UserAccountResult(bool Created, string? UserId, string Message);

/// <summary>
/// H1 — user-service seam. Onboarding creates the employee's login account and sends the invitation email;
/// HR also reads Departments and Branches from there, since user-service owns them (HR-DEC-3).
/// <para><b>Account creation is fail-open by design:</b> the employee record is the master (HR-DEC-2) and must
/// not be lost because the identity service was briefly unavailable. A failure is recorded on the employee and
/// can be retried, rather than aborting the onboarding.</para>
/// </summary>
public interface IUserDirectoryGateway
{
    Task<UserAccountResult> CreateAccountAsync(
        string firstName, string lastName, string email, string? mobile,
        string? departmentId, string? branchId, List<string>? roleIds, CancellationToken ct = default);

    /// <summary>Departments user-service holds for this tenant; empty when it cannot be reached.</summary>
    Task<List<OrgUnitDto>> ListDepartmentsAsync(CancellationToken ct = default);

    /// <summary>Branches for this tenant; empty when unavailable.</summary>
    Task<List<OrgUnitDto>> ListBranchesAsync(CancellationToken ct = default);
}

/// <summary>
/// H2 — alert delivery. HR raises milestone alerts (probation reviews, contract expiry) through ticketing's
/// internal alert endpoint, the same channel the compliance statutory calendar uses, so HR notifications land
/// in the one place staff already watch instead of a channel of their own.
/// <para><paramref name="requiredPermission"/> gates who sees it — probation and contract alerts at 30 days go
/// to HR and line managers (<c>hr.manager</c>), while the 7-day contract warning is an MD escalation
/// (<c>hr.approve</c>). <paramref name="assignedToUserId"/> additionally raises a personal notification for
/// the employee the alert concerns.</para>
/// <para>Best-effort: a notification failure never blocks the milestone record, which is the durable artefact.</para>
/// </summary>
public interface IHrAlertGateway
{
    Task CreateAlertAsync(
        string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null, string? assignedToUserId = null, CancellationToken ct = default);
}

/// <summary>One posting account from finance's chart of accounts.</summary>
public record GlAccountDto(string Id, string Code, string Name, string? Classification, bool IsDirectPosting, bool IsActive);

/// <summary>One line of a journal HR asks finance to post. Debit and credit are kept separate rather than
/// signed, because that is the shape finance's own posting endpoint takes.</summary>
public record JournalLineDto(string AccountId, string Description, decimal Debit, decimal Credit);

/// <summary>Outcome of a posting attempt. A failure is reported, never thrown away: the payroll run is the
/// durable record and the posting is retried, not re-computed.</summary>
public record JournalPostResult(bool Posted, string? JournalEntryId, string? JournalEntryNo, string Message);

/// <summary>
/// H5 — finance seam. Reads finance's chart of accounts so pay components map to real posting accounts, and
/// (H6) posts the payroll journal.
/// <para>Finance stays the ledger (HR-DEC-4): HR computes pay and hands finance a balanced journal; it never
/// writes an account or a balance itself.</para>
/// <para>Degrades to an empty list / an unposted result when finance is unreachable or unconfigured, so a
/// mapping screen shows nothing to pick rather than failing, and an approved payroll run stands with its
/// posting outstanding rather than being lost.</para>
/// </summary>
public interface IFinanceGateway
{
    Task<List<GlAccountDto>> ListAccountsAsync(CancellationToken ct = default);

    /// <summary>
    /// H6 — posts one balanced journal for a payroll run, tagged <c>SourceModule=HR-Payroll</c> so finance can
    /// trace it back. Posted immediately rather than raised as a draft: the MD has already approved the run,
    /// and asking finance to approve it a second time would duplicate a control that has been exercised.
    /// <para><paramref name="tenantSchema"/> is explicit rather than read from the caller's token so this
    /// works from a background sweep as well as a request.</para>
    /// </summary>
    Task<JournalPostResult> PostJournalAsync(
        string tenantSchema, DateTime entryDate, string description, string sourceDocumentId,
        List<JournalLineDto> lines, CancellationToken ct = default);

    /// <summary>
    /// H10 — outstanding staff advances and un-retired imprest for one employee, so a leaver's final dues can
    /// recover them (P21).
    /// <para>Keyed on the user-service user id, which HR reaches through <c>Employee.UserId</c>.</para>
    /// <para><b>Returns null when finance cannot be read</b>, exactly as the training-evidence seam does.
    /// Treating an unreachable service as "nothing outstanding" would quietly overpay someone on their way out
    /// the door, which is the one moment it becomes very hard to get back.</para>
    /// </summary>
    Task<List<OutstandingAdvanceDto>?> ListOutstandingAdvancesAsync(string employeeUserId, CancellationToken ct = default);
}

/// <summary>One un-recovered advance or imprest against an employee.</summary>
public record OutstandingAdvanceDto(string Kind, string Reference, decimal Amount, string Status);

/// <summary>One completion of a mandatory training, as held by whichever service owns it.</summary>
public record TrainingEvidenceDto(string EmployeeUserId, string Course, DateTime CompletedOn, DateTime? ExpiresOn, string? CertificateUrl);

/// <summary>
/// H7 — mandatory-training evidence, read from the services that already own it (HR-DEC-5).
/// <para>HSE completion lives in hse-service, anti-bribery in compliance-service. HR holds the RULE — which
/// trainings are required, for how long, and whether lapsing blocks an increment — and reads the record rather
/// than keeping a copy that would drift the moment either service was updated directly.</para>
/// <para><b>Both records key on the user-service user id</b>, which HR reaches through
/// <c>Employee.UserId</c> (HR-DEC-2). An employee with no login account therefore has no evidence to read,
/// which is reported as <i>unknown</i>, never as <i>not done</i>.</para>
/// <para><b>Unreachable is not the same as incomplete.</b> A failed read returns null so the caller can say
/// "could not check" — reporting an unreadable service as a lapse would block someone's salary increment on
/// the strength of a network error.</para>
/// </summary>
public interface ITrainingEvidenceGateway
{
    /// <summary>HSE course completions for the given user ids. Null means the service could not be read.</summary>
    Task<List<TrainingEvidenceDto>?> ListHseTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default);

    /// <summary>Anti-bribery completions. Null means the service could not be read.</summary>
    Task<List<TrainingEvidenceDto>?> ListAntiBriberyTrainingAsync(IEnumerable<string> employeeUserIds, CancellationToken ct = default);
}

/// <summary>A Sales Engineer's revenue target and what they have collected against it, as CRM holds it.</summary>
public record SalesAttainmentDto(string EmployeeUserId, string? EmployeeName, decimal AnnualTarget, decimal RevenueAchieved, string Currency);

/// <summary>
/// H11 — the CRM seam (H11-DEC-1). <b>CRM owns the revenue target and the attainment; HR reads both.</b>
/// <para>The target lives in <c>crm.SalesTarget</c> and collected revenue in <c>crm.RevenueSnapshot</c>. HR
/// owns only the commission overlay — the bands, the MD's approval of the basis, and the quarterly split.
/// Storing a second copy of the target in HR is precisely how two numbers for "the annual target" come to
/// disagree, and the one that pays somebody would be whichever was edited last.</para>
/// <para><b>Returns null when CRM cannot be read</b>, as every other HR read-seam does. A commission
/// statement computed against a silently-zero target would pay nothing and look deliberate.</para>
/// </summary>
public interface ICrmRevenueGateway
{
    /// <summary>Targets and attainment for the year. Null means CRM could not be read.</summary>
    Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(int year, CancellationToken ct = default);

    /// <summary>
    /// As above, but with <paramref name="tenantSchema"/> passed explicitly rather than read from the caller's
    /// token — the quarterly statement sweep runs on a timer and has no request to take a schema claim from.
    /// Same contract: null means CRM could not be read, never a zero target.
    /// </summary>
    Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(
        string tenantSchema, int year, CancellationToken ct = default);
}
