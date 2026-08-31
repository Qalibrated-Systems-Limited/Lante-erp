namespace OperationsService.Core.Entities;

/// <summary>
/// O5-FSR — FSR_EQUIPMENT: one piece of equipment serviced on a Field Service Report. Together these
/// form the per-serial service history (queryable by SerialNumber across reports). Seeded from the
/// report's DetailsJson and, for calibration work, from the intake ServiceRequestInstrument.
/// </summary>
public class FsrEquipment : BaseEntity
{
    public string  ServiceReportId            { get; set; } = string.Empty;
    public string? ServiceRequestInstrumentId { get; set; }  // link back to the intake instrument, if any

    public string? SerialNumber    { get; set; }
    public string? Manufacturer    { get; set; }
    public string? Model           { get; set; }
    public string? TagNumber       { get; set; }
    public string? Description     { get; set; }
    public string? ConditionBefore { get; set; }
    public string? ConditionAfter  { get; set; }
    public string? WorkDone        { get; set; }
    public string? Notes           { get; set; }

    public ServiceReport ServiceReport { get; set; } = null!;
}
