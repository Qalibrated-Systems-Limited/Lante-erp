using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O7 — default subcontractor-compliance seam: a config-gated no-op. Until subcontracts-service /
/// procurement expose the check, this returns compliant so operations isn't blocked. When
/// <c>Subcontracts:Enabled=true</c> a real client (HTTP to subcontracts-service) should replace this.
/// </summary>
public class NoOpSubcontractorComplianceGateway(
    IConfiguration config,
    ILogger<NoOpSubcontractorComplianceGateway> logger) : ISubcontractorComplianceGateway
{
    public Task<SubcontractorComplianceResult> CheckSiteStartComplianceAsync(string subcontractorId, CancellationToken ct = default)
    {
        if (config.GetValue("Subcontracts:Enabled", false))
            logger.LogWarning("Subcontracts:Enabled=true but no subcontracts gateway is wired — treating {SubId} as compliant.", subcontractorId);
        else
            logger.LogInformation("[Subcontracts stub] Site-start compliance check for {SubId} — assumed compliant (seam disabled).", subcontractorId);

        return Task.FromResult(new SubcontractorComplianceResult(AsrRegistered: true, RamsValid: true,
            Message: "Compliance seam disabled — check assumed to pass."));
    }
}
