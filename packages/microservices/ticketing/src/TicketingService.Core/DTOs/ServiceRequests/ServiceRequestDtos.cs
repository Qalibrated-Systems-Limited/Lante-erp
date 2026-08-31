using TicketingService.Core.Enums;

namespace TicketingService.Core.DTOs.ServiceRequests;

// ── Initiate (step 1 — submit form, receive OTP) ──────────────────────────────

public class InitiateServiceRequestDto
{
    public string FormType            { get; set; } = string.Empty; // "SRF" | "CRF_NAWI" | "CRF_MASS"
    public string ClientName          { get; set; } = string.Empty;
    public string ClientEmail         { get; set; } = string.Empty;
    public string? ClientPhone        { get; set; }
    public string? ClientOrganization { get; set; }
    public string? ClientAddress      { get; set; }
    public string? SiteLocation       { get; set; }
    public double? Latitude           { get; set; }
    public double? Longitude          { get; set; }
    public string? Description        { get; set; }
    public string? SpecialInstructions{ get; set; }
    public string  ServiceLocation    { get; set; } = "OnSite";   // "OnSite" | "InLab"
    public List<InstrumentDto> Instruments { get; set; } = new();
}

public class InstrumentDto
{
    public int    RowNumber           { get; set; } = 0;
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

    // NAWI-specific
    public string? NawiInstrumentType { get; set; }
    public string? NawiCapacity       { get; set; }
    public string? NawiScaleInterval  { get; set; }
    public string? NawiAccuracyClass  { get; set; }

    // MASS-specific
    public string? MassNominalValue   { get; set; }
    public string? MassAccuracyClass  { get; set; }

    // SRF-specific
    public string? ServiceType        { get; set; }
}

// ── Verify OTP (step 2 — confirm email) ──────────────────────────────────────

public class VerifyOtpDto
{
    public string PendingId { get; set; } = string.Empty;
    public string OtpCode   { get; set; } = string.Empty;
}

// ── Resend OTP ────────────────────────────────────────────────────────────────

public class ResendOtpDto
{
    public string PendingId { get; set; } = string.Empty;
}

// ── Client signature (submitted after OTP verify, optional second call) ───────

public class SubmitSignatureDto
{
    public string ReferenceNumber  { get; set; } = string.Empty;
    public string SignatureData    { get; set; } = string.Empty; // base64 PNG
}

// ── Read DTOs (public portal) ─────────────────────────────────────────────────

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

// ── Read DTOs (TM internal) ───────────────────────────────────────────────────

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
