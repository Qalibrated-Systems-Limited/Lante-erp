using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Handovers;

public class CreateHandoverDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? ClientRepName { get; set; }
    public string? Notes { get; set; }
    public string? StepsJson { get; set; }
}

public class UpdateHandoverDto
{
    public string? ClientName { get; set; }
    public string? ClientRepName { get; set; }
    public string? Notes { get; set; }
    public string? StepsJson { get; set; }
}

public class AddHandoverSignatureDto
{
    public HandoverSignatureRole Role { get; set; }
    public string SignatoryName { get; set; } = string.Empty;
    public string? SignatureData { get; set; }
}

public class HandoverSignatureReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string SignatoryName { get; set; } = string.Empty;
    public string? SignatureData { get; set; }
    public DateTime SignedAt { get; set; }
}

public class HandoverReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string HandoverNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StepsJson { get; set; }
    public string? ClientName { get; set; }
    public string? ClientRepName { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public bool IsPermanent { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<HandoverSignatureReadDto> Signatures { get; set; } = new();
    // The mandatory signatory roles still missing before the handover can be completed.
    public List<string> MissingMandatoryRoles { get; set; } = new();
}
