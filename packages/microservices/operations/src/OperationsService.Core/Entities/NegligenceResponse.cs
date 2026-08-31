namespace OperationsService.Core.Entities;

/// <summary>
/// O9 — NEGLIGENCE_RESPONSE: a response to a negligence incident (by HR / Department Head / MD),
/// optionally carrying a payroll deduction (posted to HR).
/// </summary>
public class NegligenceResponse : BaseEntity
{
    public string  IncidentId    { get; set; } = string.Empty;
    public string  ResponderId   { get; set; } = string.Empty;
    public string  ResponderName { get; set; } = string.Empty;
    public string  ResponderRole { get; set; } = string.Empty;   // HR | DepartmentHead | MD
    public string  ResponseText  { get; set; } = string.Empty;
    public string? ActionTaken   { get; set; }
    public decimal? PayrollDeductionAmount { get; set; }
    public DateTime RespondedAt  { get; set; }

    public NegligenceIncident Incident { get; set; } = null!;
}
