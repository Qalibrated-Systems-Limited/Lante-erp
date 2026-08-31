using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class LabWorkOrder : BaseEntity
{
    public string AssignmentId      { get; set; } = string.Empty;
    public string ServiceRequestId  { get; set; } = string.Empty;   // SR reference (ticketing side)
    public LabWorkOrderStatus Status { get; set; } = LabWorkOrderStatus.Pending;

    // ── Calibration type ─────────────────────────────────────────────────────
    public string?   CalibrationSubType      { get; set; }  // null=MASS, "BalanceAndPlatform", "Weighbridge"

    // ── Intake ────────────────────────────────────────────────────────────────
    public DateTime? IntakeDate              { get; set; }
    public string?   IntakeTechnicianId      { get; set; }
    public string?   IntakeTechnicianName    { get; set; }
    public string?   IntakeConditionNotes    { get; set; }  // kept for legacy
    public string?   IntakeFormJson          { get; set; }  // structured intake data

    // ── Bench work ────────────────────────────────────────────────────────────
    public string?   BenchTechnicianId       { get; set; }
    public string?   BenchTechnicianName     { get; set; }
    public DateTime? BenchStartDate          { get; set; }
    public DateTime? BenchCompletedDate      { get; set; }
    public string?   BenchNotes              { get; set; }
    public string?   BenchChecklistJson      { get; set; }  // JSON array of checklist items
    public DateTime? BenchSubmittedAt        { get; set; }  // When technician submitted for TM review

    // ── TM review (before certificate) ───────────────────────────────────────
    public string?   TmReviewedById          { get; set; }
    public string?   TmReviewedByName        { get; set; }
    public DateTime? TmReviewedAt            { get; set; }
    public string?   TmApprovalNotes         { get; set; }
    public string?   TmRejectionReason       { get; set; }

    // ── O6 — traceability (reference standard used for this calibration) ───────
    public string?   ReferenceStandardId     { get; set; }  // FK to the register (preferred)
    public string?   TraceabilityRefFallback { get; set; }  // free-text ref when not yet in the register

    // ── Certificate / report ──────────────────────────────────────────────────
    public string?   JobNumber               { get; set; }
    public string?   CertificateNumber       { get; set; }  // O6: now system-generated CAL-{year}-{seq}
    public DateTime? CertificateIssuedAt     { get; set; }
    public string?   CertificateNotes        { get; set; }

    // ── Dispatch ──────────────────────────────────────────────────────────────
    public DateTime? DispatchDate            { get; set; }
    public string?   DispatchMethod          { get; set; }   // "Courier" | "ClientPickup" | "Delivery"
    public string?   DispatchNotes           { get; set; }
    public string?   ReceivedBy              { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    public Assignment   Assignment { get; set; } = null!;
    public LabDataSheet? DataSheet  { get; set; }
}
