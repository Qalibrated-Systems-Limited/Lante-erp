namespace FleetService.Core.Entities;

public class DriverProfileChange : BaseEntity
{
    public string OldProfileId { get; set; } = string.Empty;
    public string NewProfileId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public ProfileChangeType ChangeType { get; set; }

    public virtual DriverProfile? OldProfile { get; set; }
    public virtual DriverProfile? NewProfile { get; set; }
}

public enum ProfileChangeType { Added, Modified, Removed }
