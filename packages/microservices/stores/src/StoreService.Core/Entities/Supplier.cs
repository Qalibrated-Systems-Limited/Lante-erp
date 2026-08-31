using System.ComponentModel.DataAnnotations;

namespace StoreService.Core.Entities;

public class Supplier : BaseEntity
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? KraPin { get; set; }

    /// <summary>0-5 supplier rating.</summary>
    public decimal? Rating { get; set; }

    [MaxLength(255)]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ItemMaster> Items { get; set; } = new List<ItemMaster>();
}
