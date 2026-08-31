using StoreService.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace StoreService.Core.Entities;

/// <summary>A physical place stock can sit — a warehouse, a job site, a vehicle, or a vendor's own
/// premises (consignment). Every stock-affecting action resolves to one of these.</summary>
public class Location : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public LocationType Type { get; set; } = LocationType.Warehouse;

    public bool IsActive { get; set; } = true;
}
