namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O8 (DEC-1) — read-only seam to hse-service. HSE incidents, TRIR, RED-008 escalation and 7-year
/// retention all live in hse-service; operations does NOT own them. Operations only CONNECTS: it reads
/// a project's HSE status (for display and the close-out safety gate — a project handover cannot be
/// completed while a serious/LTI incident is open). Config-gated, D8-style: the no-op reports "clear"
/// when <c>Hse:Enabled</c> is false, so operations is never blocked before the seam is wired.
/// </summary>
public interface IHseGateway
{
    Task<HseProjectSummary> GetProjectHseSummaryAsync(string projectId, CancellationToken ct = default);
}

public record HseProjectSummary(
    int OpenIncidents,
    int OpenSeriousIncidents,   // LTI / serious (RED-008)
    double? Trir,               // total recordable incident rate
    string? Message)
{
    /// <summary>A project is clear for close-out when no serious/LTI incident is still open.</summary>
    public bool ClearForCloseOut => OpenSeriousIncidents == 0;
}
