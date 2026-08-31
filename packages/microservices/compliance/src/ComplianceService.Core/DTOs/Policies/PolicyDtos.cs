namespace ComplianceService.Core.DTOs.Policies;

public class PolicyReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? FileUrl { get; set; }
    public int AcknowledgedCount { get; set; }
}

public class CreatePolicyDto
{
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
}

public class UpdatePolicyDto
{
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
}

public class PolicyAckReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public DateTime SignedAt { get; set; }
}
