namespace OperationsService.Core.Entities;

public class AssignedTechnician : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public Assignment Assignment { get; set; } = null!;
}
