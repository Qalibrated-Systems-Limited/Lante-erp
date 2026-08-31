namespace ProcurementService.Core.Enums;

/// <summary>P1 — supplier lifecycle in the Approved Supplier Register. A supplier may only be used in a
/// Purchase Order once <see cref="Approved"/> and not <see cref="Blacklisted"/>.</summary>
public enum SupplierStatus
{
    Pending,          // created, documents/conflict-check outstanding
    ConflictFlagged,  // PROC-007 conflict found → suspended pending MD review
    Approved,         // procurement-manager approved (is_approved=true)
    Suspended,        // manually suspended
    Blacklisted,      // MD-approved blacklist (reason mandatory)
}

/// <summary>Compliance document kinds tracked per supplier (PROC-001).</summary>
public enum SupplierDocumentType
{
    CertificateOfIncorporation,
    KraPinCertificate,
    TaxCompliance,
    Cr12,
    BankDetails,
    Other,
}

/// <summary>ASR audit event kinds (DS5 AUDIT_LOG — PROC-005).</summary>
public enum AsrAuditAction
{
    SupplierCreated,
    DocumentUploaded,
    ConflictFlagged,
    ConflictCleared,
    Approved,
    Suspended,
    Blacklisted,
    Reinstated,
    GiftDeclared,
    Seeded,
    // P3 — quotation & comparative analysis
    QuotationRecorded,
    ComparisonCompleted,
    // P4 — LPO generation & approval
    LpoGenerated,
    LpoSigned,
    LpoIssued,
    LpoRejected,
    // P6 — 3-way match & payment voucher
    MatchRun,
    MatchExceptionRaised,
    MatchExceptionResolved,
    PaymentVoucherRaised,
    // P7 — international sourcing
    IntlPoCreated,
    IntlTtApproved,
    IntlTtSent,
    IntlShipmentUpdated,
    IntlCustomsDeclared,
    IntlLandedCostComputed,
    // P8 — emergency procurement
    EmergencyDeclared,
    EmergencyMdApproved,
    EmergencyPostHocPrRaised,
    EmergencyBoardPackMarked,
    // P9 — supplier performance review
    PerformanceReviewRun,
    PerformanceMdEscalated,
}
