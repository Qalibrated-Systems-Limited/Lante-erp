using System.ComponentModel.DataAnnotations;

namespace StoreService.Core.Entities;

/// <summary>Item category master (e.g. "Consumables", "PPE & Safety Equipment") — replaces the
/// free-text Category string previously on ItemMaster.</summary>
public class Category : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ItemMaster> Items { get; set; } = new List<ItemMaster>();
}
