namespace OperationsService.Core.Enums;

// O5 — Service Request / Calibration Request domain, migrated from ticketing.
public enum ServiceRequestFormType
{
    SRF,        // Service Request Form
    CRF_NAWI,   // Calibration Request Form — Non-Automatic Weighing Instruments
    CRF_MASS,   // Calibration Request Form — Mass Standards / Weights
}

public enum ServiceLocationType
{
    OnSite,   // Technician travels to client location
    InLab,    // Instruments brought to Lante lab
}

public enum ServiceRequestStatus
{
    PendingVerification,   // OTP not yet confirmed
    Submitted,             // OTP verified, awaiting TM review
    UnderReview,           // TM has opened the request
    QuotationDraft,        // TM is building the quotation
    QuotationSent,         // Quotation emailed to client
    QuotationApproved,     // Client accepted / LPO received
    QuotationRejected,     // Client declined
    InProgress,            // Assignment created, work underway
    Completed,             // Work done
    Rejected,              // Rejected by TM at review stage
    Cancelled,             // Cancelled by client or system
}

public enum QuotationStatus
{
    Draft,
    Sent,
    Accepted,
    Rejected,
    Expired,
}
