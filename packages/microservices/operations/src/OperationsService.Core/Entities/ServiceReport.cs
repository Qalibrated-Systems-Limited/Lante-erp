using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class ServiceReport : BaseEntity
{
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public DepartmentType DepartmentType { get; set; }
    public ServiceReportStatus Status { get; set; } = ServiceReportStatus.Draft;

    // Common fields across all dept types
    public string CustomerName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? LocationAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public NatureOfVisit NatureOfVisit { get; set; }
    public DateTime? StartDay { get; set; }
    public DateTime? EndDay { get; set; }
    public int? TotalMinutes { get; set; }
    public string? CustomerComments { get; set; }

    // Legacy single-signature field (kept for backward compat)
    public string? SignatureData { get; set; }
    public DateTime? SignedAt { get; set; }

    // Dual-signature fields (customer + technician draw-pad)
    public string? CustomerSignatureName { get; set; }
    public string? CustomerSignatureData { get; set; }
    public string? TechnicianSignatureName { get; set; }
    public string? TechnicianSignatureData { get; set; }

    // Department-specific fields stored as JSON
    public string? DetailsJson { get; set; }

    // O5-FSR — 8-section Field Service Report structure (§3 equipment is the FsrEquipment collection;
    // §1 customer/§2 visit/§7 time already exist above). SubmittedAt drives the 24-hour overdue check.
    public DateTime? SubmittedAt      { get; set; }   // set when the report reaches Submitted
    public string?   WorkSummary      { get; set; }   // §4 work performed / findings
    public string?   MaterialsUsed    { get; set; }   // §5 parts / materials
    public string?   Recommendations  { get; set; }   // §6 recommendations / follow-up
    public bool      FollowUpRequired { get; set; }
    public string?   FollowUpNotes    { get; set; }
    public int?      ClientRating     { get; set; }   // §8 client satisfaction 1–5 at sign-off

    // Approval
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public Assignment Assignment { get; set; } = null!;
    public ICollection<FsrEquipment> Equipment { get; set; } = new List<FsrEquipment>();
}
