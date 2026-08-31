using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.UnitsOfMeasure;

public class UnitOfMeasureFilterParameters : PaginationParameters
{
    public bool? IsActive { get; set; }
}

public class CreateUnitOfMeasureDto
{
    [Required]
    [MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Description { get; set; }
}

public class UpdateUnitOfMeasureDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class UnitOfMeasureReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
