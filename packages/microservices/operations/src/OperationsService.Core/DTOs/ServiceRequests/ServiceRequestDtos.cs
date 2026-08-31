namespace OperationsService.Core.DTOs.ServiceRequests;

// O5 — SR pipeline DTOs, migrated from ticketing. The anonymous portal + OTP intake stays in
// ticketing (DEC-4); these cover the TM-side management surface now owned by operations:
// list / detail / review / quotation build+send / LPO / certificate / in-service dispatch.

// ── Read DTOs (queue summary) ─────────────────────────────────────────────────

public class ServiceRequestSummaryDto
{
    public string ReferenceNumber     { get; set; } = string.Empty;
    public string FormType            { get; set; } = string.Empty;
    public string Status              { get; set; } = string.Empty;
    public string ServiceLocation     { get; set; } = "OnSite";
    public string ClientName          { get; set; } = string.Empty;
    public string ClientEmail         { get; set; } = string.Empty;
    public string? ClientOrganization { get; set; }
    public string? SiteLocation       { get; set; }
    public int    InstrumentCount     { get; set; }
    public DateTime CreatedAt         { get; set; }
    public string? TicketId           { get; set; }
    public bool   HasQuotation        { get; set; }
}

// ── Read DTOs (TM detail) ─────────────────────────────────────────────────────

public class ServiceRequestDetailDto
{
    public string  Id                  { get; set; } = string.Empty;
    public string  ReferenceNumber     { get; set; } = string.Empty;
    public string  FormType            { get; set; } = string.Empty;
    public string  Status              { get; set; } = string.Empty;
    public string  ServiceLocation     { get; set; } = "OnSite";
    public string  ClientName          { get; set; } = string.Empty;
    public string  ClientEmail         { get; set; } = string.Empty;
    public string? ClientPhone         { get; set; }
    public string? ClientOrganization  { get; set; }
    public string? ClientAddress       { get; set; }
    public string? SiteLocation        { get; set; }
    public double? Latitude            { get; set; }
    public double? Longitude           { get; set; }
    public string? Description         { get; set; }
    public string? SpecialInstructions { get; set; }
    public bool    HasSignature        { get; set; }
    public DateTime? ClientSignedAt    { get; set; }
    public DateTime? OtpVerifiedAt     { get; set; }
    public string? TicketId            { get; set; }
    public string? OperationsAssignmentId { get; set; }
    public string? CertificateNumber   { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
    public string? ReviewedByTmId       { get; set; }
    public DateTime? TmReviewedAt       { get; set; }
    public string? TmComments           { get; set; }
    public string? RejectionReason      { get; set; }
    public string? ReviewChecklistJson  { get; set; }
    public DateTime? PlannedServiceDate { get; set; }
    public string? AuthorizingName      { get; set; }
    public DateTime CreatedAt          { get; set; }
    public DateTime UpdatedAt          { get; set; }
    public List<InstrumentDetailDto> Instruments { get; set; } = new();
    public QuotationDetailDto? Quotation { get; set; }
}

public class InstrumentDetailDto
{
    public string  Id                    { get; set; } = string.Empty;
    public int     RowNumber             { get; set; }
    public string? Description           { get; set; }
    public string? Manufacturer          { get; set; }
    public string? Model                 { get; set; }
    public string? SerialNumber          { get; set; }
    public string? TagNumber             { get; set; }
    public string? Range                 { get; set; }
    public string? RangeUnit             { get; set; }
    public string? Condition             { get; set; }
    public string? Remarks               { get; set; }
    public DateTime? LastCalibrationDate { get; set; }
    public string?   CertificateNumber  { get; set; }
    public string? NawiInstrumentType   { get; set; }
    public string? NawiCapacity         { get; set; }
    public string? NawiScaleInterval    { get; set; }
    public string? NawiAccuracyClass    { get; set; }
    public string? MassNominalValue     { get; set; }
    public string? MassAccuracyClass    { get; set; }
    public string? ServiceType          { get; set; }
}

public class QuotationDetailDto
{
    public string  Id               { get; set; } = string.Empty;
    public string  QuotationNumber  { get; set; } = string.Empty;
    public string  Status           { get; set; } = string.Empty;
    public DateTime? ValidUntil     { get; set; }
    public List<QuotationLineItemDto> LineItems { get; set; } = new();
    public decimal Subtotal         { get; set; }
    public decimal VatRate          { get; set; }
    public decimal VatAmount        { get; set; }
    public decimal TotalAmount      { get; set; }
    public string? Notes            { get; set; }
    public DateTime? SentAt         { get; set; }
    public string? LpoNumber        { get; set; }
    public DateTime? LpoReceivedAt  { get; set; }
    public DateTime? AcceptedAt     { get; set; }
    public DateTime? RejectedAt     { get; set; }
    public DateTime CreatedAt       { get; set; }
}

public class QuotationLineItemDto
{
    public string  Description { get; set; } = string.Empty;
    public decimal Quantity    { get; set; } = 1;
    public decimal UnitPrice   { get; set; }
    public decimal Amount      { get; set; }  // read-only; computed = Qty × UnitPrice
}

// ── TM action DTOs ────────────────────────────────────────────────────────────

public class TmReviewDto
{
    /// <summary>true = approve (move to UnderReview), false = reject</summary>
    public bool   Approve              { get; set; }
    public string? TmComments          { get; set; }
    public string? RejectionReason     { get; set; }
    public string? ReviewChecklistJson { get; set; }
    public DateTime? PlannedServiceDate { get; set; }
    public string? AuthorizingName     { get; set; }
}

public class CreateQuotationDto
{
    public DateTime? ValidUntil        { get; set; }
    public decimal   VatRate           { get; set; } = 0.16m;
    public List<QuotationLineItemDto> LineItems { get; set; } = new();
    public string? Notes               { get; set; }
}

public class UpdateQuotationDto
{
    public DateTime? ValidUntil        { get; set; }
    public decimal?  VatRate           { get; set; }
    public List<QuotationLineItemDto>? LineItems { get; set; }
    public string? Notes               { get; set; }
}

public class RecordLpoDto
{
    public string  LpoNumber  { get; set; } = string.Empty;
    public string? Notes      { get; set; }
}

public class RecordCertificateDto
{
    public string    CertificateNumber   { get; set; } = string.Empty;
    public DateTime? CertificateIssuedAt { get; set; }
}

// Client declines a sent quotation. Reason is optional (free-text, stored on the SR).
public class RejectQuotationDto
{
    public string? Reason { get; set; }
}

// Cancel a service request before field work begins. Reason is optional.
public class CancelServiceRequestDto
{
    public string? Reason { get; set; }
}

public class CreateSrAssignmentDto
{
    public List<string> TechnicianIds   { get; set; } = [];
    public List<string> TechnicianNames { get; set; } = [];
    public DateTime?    Deadline        { get; set; }
    public string?      Notes           { get; set; }
    public string       NatureOfVisit   { get; set; } = "CorrectiveMaintenance";
    public string       DepartmentId    { get; set; } = string.Empty;
}

public class ServiceRequestFilterParams
{
    public string? Status   { get; set; }
    public string? FormType { get; set; }
    public DateTime? From   { get; set; }
    public DateTime? To     { get; set; }
    public int Page         { get; set; } = 1;
    public int PageSize     { get; set; } = 20;
}

// ── Ingest (O5.3 — service-to-service intake from the ticketing portal) ─────────
// The anonymous portal + OTP live in ticketing; on OTP-verify it pushes the verified request here
// so operations becomes the SR system of record. Ticketing supplies the reference number and the
// linked tracking ticket id so the two stay aligned during the transition.

public class IngestServiceRequestDto
{
    public string? ReferenceNumber   { get; set; }  // supplied by ticketing; generated here if absent
    public string  FormType          { get; set; } = "SRF"; // "SRF" | "CRF_NAWI" | "CRF_MASS"
    public string  ServiceLocation   { get; set; } = "OnSite"; // "OnSite" | "InLab"
    public string? TicketId          { get; set; }  // helpdesk tracking ticket
    public string  ClientName        { get; set; } = string.Empty;
    public string  ClientEmail       { get; set; } = string.Empty;
    public string? ClientPhone       { get; set; }
    public string? ClientOrganization { get; set; }
    public string? ClientAddress      { get; set; }
    public string? SiteLocation       { get; set; }
    public double? Latitude           { get; set; }
    public double? Longitude          { get; set; }
    public string? Description         { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime? OtpVerifiedAt     { get; set; }
    public List<IngestInstrumentDto> Instruments { get; set; } = new();
}

public class IngestInstrumentDto
{
    public int    RowNumber           { get; set; }
    public string? Description        { get; set; }
    public string? Manufacturer       { get; set; }
    public string? Model              { get; set; }
    public string? SerialNumber       { get; set; }
    public string? TagNumber          { get; set; }
    public string? Range              { get; set; }
    public string? RangeUnit          { get; set; }
    public string? Condition          { get; set; }
    public string? Remarks            { get; set; }
    public DateTime? LastCalibrationDate { get; set; }
    public string?   CertificateNumber   { get; set; }
    public string? NawiInstrumentType { get; set; }
    public string? NawiCapacity       { get; set; }
    public string? NawiScaleInterval  { get; set; }
    public string? NawiAccuracyClass  { get; set; }
    public string? MassNominalValue   { get; set; }
    public string? MassAccuracyClass  { get; set; }
    public string? ServiceType        { get; set; }
}

public record IngestResult(string Id, string ReferenceNumber, string Status);
public record SrSignatureResult(string Message);

// ── Service result records (shaped into the FE-compatible { data } envelope by the controller) ──

public record ServiceRequestListResult(List<ServiceRequestSummaryDto> Items, int Total);
public record SrReviewResult(string Status, string Message);
public record SrSendResult(string Message, DateTime? SentAt);
public record SrLpoResult(string Message, string QuotationStatus);
public record SrReviseResult(string Message, string QuotationStatus);
public record SrCertificateResult(string CertificateNumber, string Status, string Message);
public record SrAssignmentResult(string AssignmentId, string Message);
public record SrQuotationRejectResult(string Status, string Message);
public record SrCancelResult(string Status, string Message);
