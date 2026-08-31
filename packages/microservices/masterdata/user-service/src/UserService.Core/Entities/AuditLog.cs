namespace UserService.Core.Entities;

// Tenant-scoped record of every state-changing request the gateway proxied, captured centrally
// at the gateway (one write path, not one per microservice) rather than duplicated per service.
// Method/path/status only — the gateway never sees request/response bodies, so it can't record
// "what changed", only "which endpoint was hit, by whom, with what result".
public class AuditLog : BaseEntity
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorId { get; set; }
}
