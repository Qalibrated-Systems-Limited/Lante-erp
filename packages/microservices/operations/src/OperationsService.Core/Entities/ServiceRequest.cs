using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

/// <summary>
/// O5 — a client service / calibration request. Migrated from ticketing so the whole field pipeline
/// (SR → quotation → assignment → FSR/calibration → certificate) lives in one service. Links back to
/// the helpdesk ticket raised for tracking (TicketId) and forward to the operations assignment.
/// </summary>
public class ServiceRequest : BaseEntity
{
    public string ReferenceNumber       { get; set; } = string.Empty;  // SR-2026-0001
    public ServiceRequestFormType FormType { get; set; }
    public ServiceRequestStatus Status  { get; set; } = ServiceRequestStatus.Submitted;

    // Linked records
    public string? TicketId               { get; set; }   // helpdesk tracking ticket (cross-service ref)
    public string? OperationsAssignmentId { get; set; }   // now an in-service ref to Assignment
    public string? QuotationId            { get; set; }

    // ── Client information ────────────────────────────────────────────────────
    public string  ClientName           { get; set; } = string.Empty;
    public string  ClientEmail          { get; set; } = string.Empty;
    public string? ClientPhone          { get; set; }
    public string? ClientOrganization   { get; set; }

    /// <summary>
    /// CRM customer id, resolved once when the request is ingested. This is the anchor that makes
    /// downstream client lookups exact — without it, matching a certificate back to a CRM customer
    /// falls back to matching on client name, which is ambiguous. Nullable: CRM may be disabled, or
    /// the client may not exist in CRM yet.
    /// </summary>
    public string? CrmCustomerId       { get; set; }
    public string? ClientAddress        { get; set; }

    // Signature from draw-pad (base64 PNG)
    public string? ClientSignatureData  { get; set; }
    public DateTime? ClientSignedAt     { get; set; }

    // OTP confirmation
    public DateTime? OtpVerifiedAt      { get; set; }

    public ServiceLocationType ServiceLocation { get; set; } = ServiceLocationType.OnSite;

    // ── Site / location ───────────────────────────────────────────────────────
    public string? SiteLocation         { get; set; }
    public double? Latitude             { get; set; }
    public double? Longitude            { get; set; }

    // ── Common form fields ────────────────────────────────────────────────────
    public string? Description          { get; set; }
    public string? SpecialInstructions  { get; set; }

    // ── Lab certificate ───────────────────────────────────────────────────────
    public string?   CertificateNumber  { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }

    // ── TM review ─────────────────────────────────────────────────────────────
    public string?   ReviewedByTmId     { get; set; }
    public DateTime? TmReviewedAt       { get; set; }
    public string?   TmComments         { get; set; }
    public string?   RejectionReason    { get; set; }
    public string?   ReviewChecklistJson { get; set; }  // JSON of 6-item review checklist
    public DateTime? PlannedServiceDate  { get; set; }
    public string?   AuthorizingName     { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public ICollection<ServiceRequestInstrument> Instruments { get; set; } = new List<ServiceRequestInstrument>();
    public Quotation? Quotation { get; set; }
}
