using System.ComponentModel.DataAnnotations;
using StoreService.Core.DTOs.Common;

namespace StoreService.Core.DTOs.Locations;

public class LocationFilterParameters : PaginationParameters
{
    public string? Type { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateLocationDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Type { get; set; } = string.Empty;
}

public class UpdateLocationDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public bool? IsActive { get; set; }
}

public class LocationReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
