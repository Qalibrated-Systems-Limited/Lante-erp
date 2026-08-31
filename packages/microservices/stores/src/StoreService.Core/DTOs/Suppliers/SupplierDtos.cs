using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Suppliers;

public class SupplierFilterParameters : PaginationParameters
{
    public bool? IsActive { get; set; }
}

public class CreateSupplierDto
{
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? KraPin { get; set; }

    [Range(0, 5)]
    public decimal? Rating { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? Address { get; set; }
}

public class UpdateSupplierDto
{
    public string? Name { get; set; }
    public string? KraPin { get; set; }

    [Range(0, 5)]
    public decimal? Rating { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool? IsActive { get; set; }
}

public class SupplierReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public decimal? Rating { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
