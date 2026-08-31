namespace OperationsService.Core.Entities;

public class NonConformanceReport : BaseEntity
{
    public string AssignmentId      { get; set; } = string.Empty;
    public string AncrNumber        { get; set; } = string.Empty;   // ANCR-2026-0001
    public string Type              { get; set; } = "Deviation";    // Deviation | NonConformingWork
    public string Category          { get; set; } = string.Empty;   // Equipment | Process | Personnel | Environmental | Measurement
    public string Description       { get; set; } = string.Empty;
    public string DetectionMethod   { get; set; } = string.Empty;
    public string? ImmediateAction  { get; set; }
    public string? RootCause        { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
    public string Status            { get; set; } = "Open";         // Open | UnderInvestigation | Closed
    public string RaisedById        { get; set; } = string.Empty;
    public string RaisedByName      { get; set; } = string.Empty;
    public string? ClosedById       { get; set; }
    public string? ClosedByName     { get; set; }
    public DateTime? ClosedAt       { get; set; }
    public string? ClosureNotes     { get; set; }

    public Assignment Assignment    { get; set; } = null!;
}
