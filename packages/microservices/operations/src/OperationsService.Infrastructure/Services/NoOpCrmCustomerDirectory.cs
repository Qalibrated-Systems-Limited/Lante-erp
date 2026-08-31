using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O1 — default CRM customer-verify seam: a config-gated no-op. Until crm-service verification is
/// wired, <see cref="CustomerExistsAsync"/> returns true so project creation is never blocked. When
/// <c>Crm:Enabled=true</c> a real implementation should replace this; the no-op logs so calls are
/// visible. Mirrors the D8 ICrmSync no-op pattern used in ticketing.
/// </summary>
public class NoOpCrmCustomerDirectory(
    IConfiguration config,
    ILogger<NoOpCrmCustomerDirectory> logger) : ICrmCustomerDirectory
{
    public Task<bool> CustomerExistsAsync(string crmLeadId, CancellationToken ct = default)
    {
        var enabled = config.GetValue("Crm:Enabled", false);
        if (enabled)
            // A real client isn't wired yet; log loudly rather than silently pass so the gap is visible.
            logger.LogWarning("Crm:Enabled=true but no CRM customer directory is wired — treating {LeadId} as valid.", crmLeadId);
        return Task.FromResult(true);
    }

    public Task ReportCalibrationDueAsync(CalibrationDueNotice notice, string? schema = null, CancellationToken ct = default)
    {
        logger.LogInformation("[CRM stub] Calibration recall: cert {Cert} for {Client} <{Email}> next due {Due:yyyy-MM-dd} (schema {Schema}).",
            notice.CertificateNumber, notice.ClientName ?? "n/a", notice.ClientEmail ?? "no address",
            notice.NextDueDate, schema ?? "request");
        return Task.CompletedTask;
    }

    // No directory to query — service requests stay unanchored and callers fall back to the
    // address captured on the request itself.
    public Task<string?> ResolveCustomerIdAsync(string? email, string? name, string? schema = null, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<string?> GetCustomerEmailByIdAsync(string crmCustomerId, string? schema = null, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<string?> GetCustomerEmailAsync(string clientName, string? schema = null, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
