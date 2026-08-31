namespace FleetService.Core.Entities;

public class VehicleClass : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
