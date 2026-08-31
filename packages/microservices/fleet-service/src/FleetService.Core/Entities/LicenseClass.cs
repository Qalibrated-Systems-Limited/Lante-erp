namespace FleetService.Core.Entities;

public class LicenseClass : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual ICollection<DriverProfile> DriverProfiles { get; set; } = new List<DriverProfile>();
}
