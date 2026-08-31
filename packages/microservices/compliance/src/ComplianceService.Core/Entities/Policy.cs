namespace ComplianceService.Core.Entities;

// COMP-006: every staff member digitally signs all company policies.
public class Policy : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string? FileUrl { get; set; }

    public ICollection<PolicyAck> Acknowledgements { get; set; } = new List<PolicyAck>();
}
