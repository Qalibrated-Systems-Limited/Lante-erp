namespace OperationsService.Core.Enums;

public enum AssignmentStatus
{
    Pending, Accepted, Declined, InProgress, Completed, Cancelled,
    AwaitingProjectLink, Archived
}

public enum AssignmentPriority { Low, Normal, High, Urgent }

public enum AssignmentSourceType { Standalone, Ticket, ProjectTask }

public enum DepartmentType
{
    Technical, ICT, CRM, Sales, Safety, Fleet, HR, Finance, Quality, General
}

public enum NatureOfVisit
{
    Installation, Repair, Maintenance, Inspection, Commissioning, Training, Consultation, SiteVisit,
    Calibration, CorrectiveMaintenance, PreventiveMaintenance, Other
}

public enum LabWorkOrderStatus
{
    Pending           = 0,   // Created, awaiting instrument arrival
    IntakeComplete    = 1,   // Instruments received and logged
    InBench           = 2,   // Active bench / calibration work
    AwaitingTmReview  = 3,   // Technician submitted; pending TM sign-off
    CertificateIssued = 4,   // Certificate generated and issued
    Dispatched        = 5,   // Instruments returned to client
    TmApproved        = 6,   // TM signed off; awaiting certificate generation
}

public enum ServiceReportStatus
{
    Draft, Submitted, UnderReview, Approved, Rejected, Revised
}

public enum CheckInStatus { OnSite, CheckedOut }

public enum PhotoType { Before, During, After, Evidence }
