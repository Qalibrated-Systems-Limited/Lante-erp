using TicketingService.Core.Enums;

namespace TicketingService.Core.Entities;

public class ServiceRequest : BaseEntity
{
    public string ReferenceNumber       { get; set; } = string.Empty;  // SR-2026-0001
    public ServiceRequestFormType FormType { get; set; }
    public ServiceRequestStatus Status  { get; set; } = ServiceRequestStatus.Submitted;

    // Linked records (set after workflow actions)
    public string? TicketId                   { get; set; }
    public string? OperationsAssignmentId     { get; set; }
    public string? QuotationId                { get; set; }

    // ── Client information ────────────────────────────────────────────────────
    public string  ClientName           { get; set; } = string.Empty;
    public string  ClientEmail          { get; set; } = string.Empty;
    public string? ClientPhone          { get; set; }
    public string? ClientOrganization   { get; set; }
    public string? ClientAddress        { get; set; }

    // Signature from draw-pad (base64 PNG)
    public string? ClientSignatureData  { get; set; }
    public DateTime? ClientSignedAt     { get; set; }

    // OTP confirmation
    public DateTime? OtpVerifiedAt      { get; set; }

    // ── Service location type ─────────────────────────────────────────────────
    public ServiceLocationType ServiceLocation { get; set; } = ServiceLocationType.OnSite;

    // ── Site / location ───────────────────────────────────────────────────────
    public string? SiteLocation         { get; set; }
    public double? Latitude             { get; set; }
    public double? Longitude            { get; set; }

    // ── Common form fields ────────────────────────────────────────────────────
    public string? Description          { get; set; }  // free text / nature of request
    public string? SpecialInstructions  { get; set; }

    // ── Lab certificate (written back from operations) ────────────────────────
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
    public Quotation? Quotation { get; set; }
}
