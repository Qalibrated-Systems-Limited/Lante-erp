namespace HSEService.Core.Entities;

// HSE-007 (Should): 1:1 optional extension of HseIncident, isolating NEMA-specific fields so
// the core incident table stays lean — per the ERD design notes (companion doc, section 1).
public class EnvIncident : BaseEntity
{
    public string IncidentId { get; set; } = string.Empty;
    public string? NemaRef { get; set; }
    public bool NemaNotificationRequired { get; set; } = true;
    public DateTime? NotifiedAt { get; set; }

    public HseIncident? Incident { get; set; }
}
