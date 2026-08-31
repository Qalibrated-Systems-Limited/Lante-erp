namespace OperationsService.Core.Entities;

public class FuelLog : BaseEntity
{
    public string DispatchId { get; set; } = string.Empty;
    public VehicleDispatch Dispatch { get; set; } = null!;
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
