using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.Categories;

public class CategoryReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public TicketPriority DefaultPriority { get; set; }
    public string? DefaultAssigneeId { get; set; }
    public bool RequiresEvidence { get; set; }
    public bool AutoCreateANCR { get; set; }
    public bool IsComplaint { get; set; }
    public bool BusinessHoursOnly { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public TicketPriority DefaultPriority { get; set; } = TicketPriority.Medium;
    public string? DefaultAssigneeId { get; set; }
    public bool RequiresEvidence { get; set; } = false;
    public bool AutoCreateANCR { get; set; } = false;
}

public class UpdateCategoryDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DepartmentId { get; set; }
    public TicketPriority? DefaultPriority { get; set; }
    public string? DefaultAssigneeId { get; set; }
    public bool? RequiresEvidence { get; set; }
    public bool? AutoCreateANCR { get; set; }
    public bool? IsActive { get; set; }
}
