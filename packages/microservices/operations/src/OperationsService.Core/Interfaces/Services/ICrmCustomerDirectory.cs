namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O1 — cross-module seam to CRM for verifying that a project's linked customer/lead actually exists
/// (DFD "CUSTOMER verify"). Config-gated, D8-style: the default no-op implementation returns true so
/// operations is not coupled to CRM being present. A real implementation (HTTP to crm-service) can be
/// swapped in without touching ProjectService. Enable with <c>Crm:Enabled=true</c>.
/// </summary>
public interface ICrmCustomerDirectory
{
    /// <summary>True if the customer/lead is known to CRM (or verification is disabled).</summary>
    Task<bool> CustomerExistsAsync(string crmLeadId, CancellationToken ct = default);

    /// <summary>
    /// O6 — report a calibration's next-due date to CRM so it can schedule a recall reminder for the
    /// client. Best-effort; no-op when CRM is disabled.
    /// </summary>
    /// <param name="schema">
    /// Tenant schema to scope the call to. Required from the background sweep: it has no HTTP
    /// request, so an implementation that reads the schema from HttpContext would silently
    /// resolve none and skip the send. Null means "use the current request's schema".
    /// </param>
    Task ReportCalibrationDueAsync(CalibrationDueNotice notice, string? schema = null, CancellationToken ct = default);

    /// <summary>
    /// O6.2 — resolve the CRM customer id for a client, once, when a service request is ingested.
    /// Email is the primary key because it is unique and stable; the name is only a secondary hint.
    /// Returns null when CRM is disabled or the client is not (yet) a CRM customer — callers must
    /// treat that as normal, not an error.
    /// </summary>
    Task<string?> ResolveCustomerIdAsync(string? email, string? name, string? schema = null, CancellationToken ct = default);

    /// <summary>
    /// O6.2 — a client's current contact email, by CRM customer id. This is the exact path: given an
    /// anchored id, a recall reaches the address CRM holds today with no name guessing.
    /// </summary>
    Task<string?> GetCustomerEmailByIdAsync(string crmCustomerId, string? schema = null, CancellationToken ct = default);

    /// <summary>
    /// O6.1 — last-resort lookup by client name, for records issued before CRM ids were anchored.
    /// Name matching is inexact; implementations must return null unless the match is unambiguous.
    /// Prefer <see cref="GetCustomerEmailByIdAsync"/> whenever an id is available.
    /// </summary>
    Task<string?> GetCustomerEmailAsync(string clientName, string? schema = null, CancellationToken ct = default);
}

public record CalibrationDueNotice(
    string  CertificateNumber,
    string? ClientName,
    string? ClientEmail,
    DateTime NextDueDate);
