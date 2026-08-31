namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O7 (DEC-2) — read-only seam to subcontracts-service / procurement. Subcontractor records, RAMS and
/// retention live in subcontracts-service; operations does NOT own them. Before a subcontractor starts
/// on site, operations enforces the gate by CHECKING here that the subcontractor is ASR-registered
/// (procurement) and has valid RAMS. Config-gated, D8-style: the no-op returns compliant when
/// <c>Subcontracts:Enabled</c> is false, so operations is never blocked before the seam is wired.
/// </summary>
public interface ISubcontractorComplianceGateway
{
    Task<SubcontractorComplianceResult> CheckSiteStartComplianceAsync(string subcontractorId, CancellationToken ct = default);
}

public record SubcontractorComplianceResult(
    bool AsrRegistered,
    bool RamsValid,
    string? Message)
{
    public bool IsCompliant => AsrRegistered && RamsValid;
}
