using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Infrastructure.Services;

/// <summary>Config-gated no-op Projects seam (Operations:Enabled). Logs the intended project and
/// reports success without an id until the real operations-service HTTP client is wired (O10).</summary>
public class NoOpProjectGateway(IConfiguration config, ILogger<NoOpProjectGateway> logger) : IProjectGateway
{
    public Task<ProjectCreationResult> CreateProjectFromDealAsync(DealProjectRequest request, CancellationToken ct = default)
    {
        var enabled = config.GetValue<bool>("Operations:Enabled");
        logger.LogInformation("[Projects seam{State}] deal {Deal} → project '{Name}' value {Value} for {Customer}",
            enabled ? "" : " (disabled)", request.DealNumber, request.Name, request.ContractValue, request.CustomerName);
        return Task.FromResult(new ProjectCreationResult(true, null,
            enabled ? "Project creation requested." : "Operations integration disabled — project not created."));
    }
}
