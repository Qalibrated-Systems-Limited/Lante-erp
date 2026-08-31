using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>O9 — PROJECT_HANDOVER_SIGNATURE: one mandatory e-signature on a project handover.</summary>
public class ProjectHandoverSignature : BaseEntity
{
    public string HandoverId { get; set; } = string.Empty;
    public HandoverSignatureRole Role { get; set; }
    public string  SignatoryName { get; set; } = string.Empty;
    public string? SignatureData { get; set; }   // base64 draw-pad image
    public DateTime SignedAt     { get; set; }

    public ProjectHandover Handover { get; set; } = null!;
}
