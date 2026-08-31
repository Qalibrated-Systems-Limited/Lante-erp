namespace HrService.Core.DTOs.Org;

// ── Positions (HR-DEC-3: HR owns these; user-service owns Department/Branch) ──
public class CreatePositionDto
{
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? Description { get; set; }
    public int? ApprovedHeadcount { get; set; }
}

public class UpdatePositionDto
{
    public string? Title { get; set; }
    public string? Code { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? Description { get; set; }
    public int? ApprovedHeadcount { get; set; }
    public bool? IsActive { get; set; }
}

public class PositionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Description { get; set; }
    public int? ApprovedHeadcount { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Employees currently holding this position.</summary>
    public int FilledCount { get; set; }
    /// <summary>Approved headcount less filled, when a headcount is set.</summary>
    public int? Vacancies { get; set; }
}

// ── Org chart (P32) ──
public class OrgChartNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? ParentEmployeeId { get; set; }
    public int Level { get; set; }
    public int DisplayOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    /// <summary>Direct reports, nested — the tree the UI renders with the MD at the apex.</summary>
    public List<OrgChartNodeDto> Reports { get; set; } = new();
}

public class OrgChartDto
{
    /// <summary>Top-level nodes (normally just the MD). Employees with no reporting line appear here too, so
    /// an incomplete hierarchy is visible rather than silently dropped.</summary>
    public List<OrgChartNodeDto> Roots { get; set; } = new();
    public int NodeCount { get; set; }
    public int MaxDepth { get; set; }
    /// <summary>Active employees with no org-chart node yet — a prompt to rebuild.</summary>
    public int UnplacedEmployees { get; set; }
    /// <summary>Employees with no manager set, excluding the apex — a data-quality signal.</summary>
    public List<string> MissingReportingLine { get; set; } = new();
}

public class SetReportingLineDto
{
    /// <summary>The manager's employee id. Null detaches the employee to the apex.</summary>
    public string? ReportsToId { get; set; }
    public int? DisplayOrder { get; set; }
}

public record OrgActionResult(string Status, string Message);

/// <summary>A department or branch as held by user-service (HR-DEC-3 — HR references, does not own).</summary>
public record OrgUnitDto(string Id, string Name, string? Code, bool IsActive);
