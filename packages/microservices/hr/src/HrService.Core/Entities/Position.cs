namespace HrService.Core.Entities;

/// <summary>
/// H1 (HR-DEC-3, HR-014) — a job position: the 19 QSL roles. HR owns these because they are employment
/// constructs, deliberately kept separate from user-service's <c>Role</c>, which is an authorisation role
/// (a bundle of permissions). Conflating the two would wire performance management to the permission system.
/// <para>H9 hangs KPI scorecards off a position, so the id is stable and referenced, not a free-text title.</para>
/// </summary>
public class Position : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    /// <summary>Salary grade this position sits on — H5 attaches salary structures by grade.</summary>
    public string? JobGrade { get; set; }
    /// <summary>Owning department (a user-service department id — HR-DEC-3).</summary>
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? Description { get; set; }
    /// <summary>Approved headcount for the position, for org-chart vacancy reporting.</summary>
    public int? ApprovedHeadcount { get; set; }

    /// <summary>
    /// H7 (HR-026) — annual training hours this position must reach: 40 for most staff, 60 for technical.
    /// <para>Held per position rather than as a rule in code, because which roles count as "technical" is a
    /// tenant's decision and changes as the business does. Null falls back to the tenant default.</para>
    /// </summary>
    public int? AnnualTrainingHoursTarget { get; set; }
    public bool IsActive { get; set; } = true;
}
