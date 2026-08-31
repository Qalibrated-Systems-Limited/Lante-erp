using System.ComponentModel.DataAnnotations;

namespace StoreService.Core.Entities;

/// <summary>Unit of Measure master (e.g. "kg", "pcs", "ltr") — replaces the free-text Uom string
/// previously on ItemMaster, so two items can't end up with "kg"/"Kg"/"KG" as distinct values.</summary>
public class UnitOfMeasure : BaseEntity
{
    [Required]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ItemMaster> Items { get; set; } = new List<ItemMaster>();
}
