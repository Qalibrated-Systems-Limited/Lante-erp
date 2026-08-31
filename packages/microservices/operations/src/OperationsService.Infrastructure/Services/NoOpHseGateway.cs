using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O8 — default HSE seam: a config-gated no-op. Until hse-service exposes the read API, this reports a
/// clear summary (no open incidents) so the close-out gate never blocks. When <c>Hse:Enabled=true</c> a
/// real client (HTTP to hse-service) should replace this.
/// </summary>
public class NoOpHseGateway(
    IConfiguration config,
    ILogger<NoOpHseGateway> logger) : IHseGateway
{
    public Task<HseProjectSummary> GetProjectHseSummaryAsync(string projectId, CancellationToken ct = default)
    {
        if (config.GetValue("Hse:Enabled", false))
            logger.LogWarning("Hse:Enabled=true but no HSE gateway is wired — reporting project {ProjectId} as HSE-clear.", projectId);

        return Task.FromResult(new HseProjectSummary(
            OpenIncidents: 0, OpenSeriousIncidents: 0, Trir: null,
            Message: "HSE seam disabled — no incident data available."));
    }
}
