namespace OperationsService.Core.DTOs.Deviations;

public class NonConformanceReportDto
{
    public string  Id               { get; set; } = string.Empty;
    public string  AssignmentId     { get; set; } = string.Empty;
    public string  AncrNumber       { get; set; } = string.Empty;
    public string  Type             { get; set; } = string.Empty;
    public string  Category         { get; set; } = string.Empty;
    public string  Description      { get; set; } = string.Empty;
    public string  DetectionMethod  { get; set; } = string.Empty;
    public string? ImmediateAction  { get; set; }
    public string? RootCause        { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public string  Status           { get; set; } = string.Empty;
    public string  RaisedByName     { get; set; } = string.Empty;
    public DateTime CreatedAt       { get; set; }
    public string? ClosedByName     { get; set; }
    public DateTime? ClosedAt       { get; set; }
    public string? ClosureNotes     { get; set; }
}

public class CreateNcrDto
{
    public string  Type             { get; set; } = "Deviation";
    public string  Category         { get; set; } = string.Empty;
    public string  Description      { get; set; } = string.Empty;
    public string  DetectionMethod  { get; set; } = string.Empty;
    public string? ImmediateAction  { get; set; }
    public string? RootCause        { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
}

public class CloseNcrDto
{
    public string? ClosureNotes     { get; set; }
}

public class UpdateNcrDto
{
    public string? RootCause        { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public string? Status           { get; set; }  // Open | UnderInvestigation
}
