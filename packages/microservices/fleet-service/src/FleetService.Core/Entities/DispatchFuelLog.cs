namespace FleetService.Core.Entities;

/// <summary>Fuel purchase logged against a single <see cref="VehicleDispatch"/> — distinct from
/// the vehicle-level <see cref="FuelLog"/> used for FLEET-005 benchmark tracking on trucks.</summary>
public class DispatchFuelLog : BaseEntity
{
    public string DispatchId { get; set; } = string.Empty;
    public decimal AmountLitres { get; set; }
    public decimal CostKes { get; set; }
    public string? Location { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public virtual VehicleDispatch? Dispatch { get; set; }
}
