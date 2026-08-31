namespace TicketingService.Core.Interfaces.Services;

public interface IOperationsServiceClient
{
    /// <summary>
    /// Creates an Assignment in OperationsService from an approved Service Request.
    /// Returns the created assignment ID, or null on failure.
    /// </summary>
    Task<string?> CreateAssignmentFromServiceRequestAsync(
        CreateSrAssignmentPayload payload,
        string? bearerToken);

    /// <summary>
    /// O5.3 — push a verified portal service request to operations (the SR system of record).
    /// Best-effort during the transition: returns the operations reference on success, null on
    /// failure (ticketing keeps its own copy so the portal keeps working). Uses internal-key auth.
    /// </summary>
    Task<string?> PushServiceRequestAsync(IngestServiceRequestPayload payload);

    /// <summary>
    /// O5.4 — the SR now lives in operations, but the anonymous public portal surface stays in
    /// ticketing. These proxy the portal's tracking + signature calls to operations (internal-key +
    /// tenant headers) and relay the raw response so the portal's response shapes are preserved.
    /// </summary>
    Task<ProxyResponse> GetServiceRequestTrackingAsync(string reference);
    Task<ProxyResponse> SubmitServiceRequestSignatureAsync(string reference, string signatureData);
}

public record ProxyResponse(int StatusCode, string Body, bool Reachable);

public record IngestServiceRequestPayload(
    string? ReferenceNumber,
    string FormType,
    string ServiceLocation,
    string? TicketId,
    string ClientName,
    string ClientEmail,
    string? ClientPhone,
    string? ClientOrganization,
    string? ClientAddress,
    string? SiteLocation,
    double? Latitude,
    double? Longitude,
    string? Description,
    string? SpecialInstructions,
    DateTime? OtpVerifiedAt,
    List<IngestInstrumentPayload> Instruments);

public record IngestInstrumentPayload(
    int RowNumber,
    string? Description,
    string? Manufacturer,
    string? Model,
    string? SerialNumber,
    string? TagNumber,
    string? Range,
    string? RangeUnit,
    string? Condition,
    string? Remarks,
    DateTime? LastCalibrationDate,
    string? CertificateNumber,
    string? NawiInstrumentType,
    string? NawiCapacity,
    string? NawiScaleInterval,
    string? NawiAccuracyClass,
    string? MassNominalValue,
    string? MassAccuracyClass,
    string? ServiceType);

public record CreateSrAssignmentPayload(
    string Title,
    string? Description,
    string LinkedTicketId,
    string ServiceRequestId,
    string ServiceRequestDataJson,
    string DepartmentId,
    string? LocationName,
    string? LocationAddress,
    double? LocationLatitude,
    double? LocationLongitude,
    string Priority,
    string NatureOfVisit,
    string ServiceType,
    DateTime? Deadline,
    List<string> TechnicianIds,
    List<string> TechnicianNames);
