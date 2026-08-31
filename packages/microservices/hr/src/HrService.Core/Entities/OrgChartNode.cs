namespace HrService.Core.Entities;

/// <summary>
/// H1 (HR-005, P32) — ORG_CHART_NODE: one node per employee, MD at the apex.
/// <para><b>This is a maintained projection, not a second hierarchy.</b> The DFD says the chart is "built from
/// EMPLOYEE.reports_to_id", so <see cref="Employee.ReportsToId"/> stays the single source of truth for
/// reporting lines and <see cref="ParentEmployeeId"/> mirrors it. Keeping the table earns its place through
/// <see cref="DisplayOrder"/> and <see cref="Level"/>, which cannot be derived from the employee record —
/// sibling ordering is a presentation decision someone makes.</para>
/// <para>Nodes are upserted when an employee is hired or their reporting line changes, and deactivated on
/// separation (H10) so the chart keeps history while dropping leavers from the live view.</para>
/// </summary>
public class OrgChartNode : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>The manager's employee id — mirrors <see cref="Employee.ReportsToId"/>. Null at the apex.</summary>
    public string? ParentEmployeeId { get; set; }
    /// <summary>Depth from the apex (0 = MD), recomputed on rebuild.</summary>
    public int Level { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Ordering among siblings — the one genuinely independent field here.</summary>
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Employee? Employee { get; set; }
}
