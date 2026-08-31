namespace FleetService.Core.Entities;

public class TripType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public TripCategory Category { get; set; }
    public EmptyTripOption EmptyTripOption { get; set; }
    public MaterialRequirement MaterialRequirement { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
}

public enum TripCategory { Loaded, Empty, Maintenance, Other }
public enum EmptyTripOption { NotAllowed, Allowed, Required }
public enum MaterialRequirement { None, Optional, Mandatory }
