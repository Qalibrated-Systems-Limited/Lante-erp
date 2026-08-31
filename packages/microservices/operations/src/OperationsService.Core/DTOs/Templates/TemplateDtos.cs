namespace OperationsService.Core.DTOs.Templates;

// PR4b — project templates and recurring work.

public class ProjectTemplateDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string  Type        { get; set; } = string.Empty;
    public string? DepartmentId { get; set; }
    public bool    IsActive    { get; set; }
    public int     UseCount    { get; set; }
    public int     MilestoneCount  { get; set; }
    public int     BudgetLineCount { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class ProjectTemplateDetailDto : ProjectTemplateDto
{
    /// <summary>Sum of the milestone value shares. Under 100 means part of the contract is unallocated.</summary>
    public decimal TotalValuePct { get; set; }
    public List<TemplateMilestoneDto>  Milestones  { get; set; } = new();
    public List<TemplateBudgetLineDto> BudgetLines { get; set; } = new();
}

public class TemplateMilestoneDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }
    public int     OffsetDays  { get; set; }
    public int     DurationDays { get; set; }
    public decimal ValuePct    { get; set; }
    public bool    IsBillable  { get; set; }
    public List<TemplateTaskDto> Tasks { get; set; } = new();
}

public class TemplateTaskDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public class TemplateBudgetLineDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  Category    { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string? RateCode    { get; set; }
    public decimal Quantity       { get; set; }
    public decimal UnitCostRate   { get; set; }
    public decimal UnitClientRate { get; set; }
}

// ── Writes ──────────────────────────────────────────────────────────────────────

public class SaveProjectTemplateDto
{
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type        { get; set; }
    public string? DepartmentId { get; set; }
    public bool?   IsActive    { get; set; }
    public List<SaveTemplateMilestoneDto>  Milestones  { get; set; } = new();
    public List<SaveTemplateBudgetLineDto> BudgetLines { get; set; } = new();
}

public class SaveTemplateMilestoneDto
{
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }
    public int     OffsetDays  { get; set; }
    public int     DurationDays { get; set; }
    public decimal ValuePct    { get; set; }
    public bool?   IsBillable  { get; set; }
    public List<SaveTemplateTaskDto> Tasks { get; set; } = new();
}

public class SaveTemplateTaskDto
{
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int     Order       { get; set; }
    public decimal? EstimatedHours { get; set; }
}

public class SaveTemplateBudgetLineDto
{
    public string? Category    { get; set; }
    public string? Description { get; set; }
    public string? RateCode    { get; set; }
    public decimal Quantity       { get; set; }
    public decimal UnitCostRate   { get; set; }
    public decimal UnitClientRate { get; set; }
}

public class InstantiateTemplateDto
{
    public string  Name         { get; set; } = string.Empty;
    public string? ClientName   { get; set; }
    public string? ClientId     { get; set; }
    public string? DepartmentId { get; set; }
    public string? ProjectManagerId { get; set; }
    public string? ScopeSummary { get; set; }
    public decimal ContractValue { get; set; }
    /// <summary>Milestone offsets are measured from here. Defaults to today.</summary>
    public DateTime? StartDate  { get; set; }
}

// ── Recurrence ──────────────────────────────────────────────────────────────────

public class RecurringScheduleDto
{
    public string  Id           { get; set; } = string.Empty;
    public string  TemplateId   { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public string? ClientName   { get; set; }
    public string? ClientId     { get; set; }
    public string  DepartmentId { get; set; } = string.Empty;
    public string  ProjectManagerId { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public string  Frequency    { get; set; } = string.Empty;
    public int     Interval     { get; set; }
    public int     LeadTimeDays { get; set; }
    public DateTime  NextDueDate { get; set; }
    public DateTime? EndDate     { get; set; }
    public bool      IsActive    { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public string?   LastGeneratedProjectId { get; set; }
    public int       GeneratedCount { get; set; }
    /// <summary>True once the lead-time window has opened and the next project is due to be raised.</summary>
    public bool      DueNow { get; set; }
}

public class SaveRecurringScheduleDto
{
    public string  TemplateId   { get; set; } = string.Empty;
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public string? ClientName   { get; set; }
    public string? ClientId     { get; set; }
    public string? DepartmentId { get; set; }
    public string? ProjectManagerId { get; set; }
    public decimal ContractValue { get; set; }
    public string? Frequency    { get; set; }
    public int?    Interval     { get; set; }
    public int?    LeadTimeDays { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? EndDate     { get; set; }
    public bool?   IsActive     { get; set; }
}
