namespace FleetService.Core.Interfaces;

public record AssignmentSummaryDto(string Id, string Title, string? LocationName);

public interface IOperationsServiceClient
{
    // Service-to-service lookup (operations-service's internal/assignments/{id}) — fleet-service
    // only ever stores a raw AssignmentId, no local copy of the assignment itself. Returns null
    // (and logs) on any failure, so an operations-service outage never blocks the caller's own flow.
    Task<AssignmentSummaryDto?> GetAssignmentAsync(string tenantSchema, string assignmentId);
}
