namespace OperationsService.Core.Entities;

public class LabDataSheet : BaseEntity
{
    public string  LabWorkOrderId        { get; set; } = string.Empty;
    public string  SheetType             { get; set; } = string.Empty; // Mass | NawiBalance | NawiWeighbridge
    public string? RawDataJson           { get; set; }
    public string? CalculatedResultsJson { get; set; }
    public string  Status                { get; set; } = "Draft";      // Draft | Submitted
    public DateTime? SubmittedAt         { get; set; }
    public string?   SubmittedById       { get; set; }
    public string?   SubmittedByName     { get; set; }

    public LabWorkOrder LabWorkOrder { get; set; } = null!;
}
