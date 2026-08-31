using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR4b — a reusable project shape: the milestones, tasks and budget lines a standard job always has.
/// A recalibration contract is the same skeleton every time, and retyping it is both slow and the
/// reason two supposedly identical jobs end up structured differently.
/// </summary>
public class ProjectTemplate : BaseEntity
{
    public string Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectType Type   { get; set; }
    /// <summary>Optional — a template that only makes sense for one department.</summary>
    public string? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>How many projects have been raised from it. Cheap signal for which templates earn their keep.</summary>
    public int UseCount { get; set; }

    public ICollection<ProjectTemplateMilestone> Milestones { get; set; } = new List<ProjectTemplateMilestone>();
    public ICollection<ProjectTemplateBudgetLine> BudgetLines { get; set; } = new List<ProjectTemplateBudgetLine>();
}

/// <summary>
/// A milestone on a template. Dates are stored as offsets, not dates: a template outlives any one
/// project's calendar, so it says "starts on day 0, runs 11 days" and the start date is supplied when
/// the template is used.
/// </summary>
public class ProjectTemplateMilestone : BaseEntity
{
    public string TemplateId { get; set; } = string.Empty;

    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }

    /// <summary>Days after the project start date that this milestone begins.</summary>
    public int OffsetDays  { get; set; }
    /// <summary>How long it runs. Zero makes it a point in time rather than a bar.</summary>
    public int DurationDays { get; set; }

    /// <summary>
    /// Share of the contract value this milestone represents, 0–100. A proportion rather than an
    /// amount, because the same shape of job is run at very different contract values.
    /// </summary>
    public decimal ValuePct { get; set; }

    public bool IsBillable { get; set; } = true;

    public ProjectTemplate Template { get; set; } = null!;
    public ICollection<ProjectTemplateTask> Tasks { get; set; } = new List<ProjectTemplateTask>();
}

public class ProjectTemplateTask : BaseEntity
{
    public string TemplateMilestoneId { get; set; } = string.Empty;

    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }
    public decimal? EstimatedHours { get; set; }

    // Named to match the TemplateMilestoneId foreign key. EF maps a navigation to "<Name>Id" by
    // convention, so calling this "Milestone" made it invent a second, non-null MilestoneId column
    // that nothing ever populated.
    public ProjectTemplateMilestone TemplateMilestone { get; set; } = null!;
}

/// <summary>
/// A budget line on a template. Carries quantity and rates so the generated budget is priced, not just
/// itemised — an unpriced budget line is a heading, and PR1's approval chain has nothing to approve.
/// </summary>
public class ProjectTemplateBudgetLine : BaseEntity
{
    public string TemplateId { get; set; } = string.Empty;

    public BudgetCategory Category    { get; set; }
    public string         Description { get; set; } = string.Empty;
    public string?        RateCode    { get; set; }

    public decimal Quantity       { get; set; }
    public decimal UnitCostRate   { get; set; }
    public decimal UnitClientRate { get; set; }

    public ProjectTemplate Template { get; set; } = null!;
}
