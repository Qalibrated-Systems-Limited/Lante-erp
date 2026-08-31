using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// PR4b — standing instruction to raise a project from a template on a cycle: the periodic
/// recalibration visits a client is contracted for.
///
/// <para>It raises the project as a <b>Draft</b>, never Active. The point is that the work does not get
/// forgotten, not that it starts unsupervised — a recalibration visit still needs someone to confirm
/// scope and dates with the client before it is committed to.</para>
/// </summary>
public class RecurringProjectSchedule : BaseEntity
{
    public string TemplateId { get; set; } = string.Empty;

    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Who the recurring work is for. Mirrors the project's own client fields so the generated project
    // is anchored to the same CRM customer rather than re-resolved by name each cycle.
    public string? ClientName { get; set; }
    public string? ClientId   { get; set; }

    public string  DepartmentId     { get; set; } = string.Empty;
    public string  ProjectManagerId { get; set; } = string.Empty;

    /// <summary>Contract value stamped on each generated project; milestone values derive from it.</summary>
    public decimal ContractValue { get; set; }

    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Annually;
    /// <summary>Multiplier on the frequency — every 2 months, every 3 years. Defaults to 1.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>
    /// How far ahead of the due date the project is raised, so there is time to plan it. A visit due
    /// in March that only appears in March is already late.
    /// </summary>
    public int LeadTimeDays { get; set; } = 14;

    /// <summary>The date the next occurrence is due to start.</summary>
    public DateTime NextDueDate { get; set; }
    /// <summary>Stops the schedule after this date. Null runs indefinitely.</summary>
    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastGeneratedAt { get; set; }
    public string?   LastGeneratedProjectId { get; set; }
    public int       GeneratedCount { get; set; }

    public ProjectTemplate Template { get; set; } = null!;
}
