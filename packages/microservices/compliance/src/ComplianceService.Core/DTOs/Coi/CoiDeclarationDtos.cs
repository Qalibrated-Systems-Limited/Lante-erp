using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Enums;

namespace ComplianceService.Core.DTOs.Coi;

public class CoiDeclarationFilterParameters : PaginationParameters
{
    public int? Year { get; set; }
}

public class CoiDeclarationReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public DateTime? DeclaredOn { get; set; }
    public bool HasConflict { get; set; }
    public string? Details { get; set; }
    public CoiStatus Status { get; set; }
}

public class CreateCoiDeclarationDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public bool HasConflict { get; set; }
    public string? Details { get; set; }
}

public class ReviewCoiDeclarationDto
{
    public CoiStatus Status { get; set; }
}

// Core-field edit — Status stays review-controlled (see ReviewCoiDeclarationDto).
public class UpdateCoiDeclarationDto
{
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int Year { get; set; }
    public bool HasConflict { get; set; }
    public string? Details { get; set; }
}
