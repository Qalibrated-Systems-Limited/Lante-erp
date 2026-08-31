using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.ServiceReports;

public class FsrEquipmentDto
{
    public string? Id { get; set; }  // present on read; null on create
    public string? ServiceRequestInstrumentId { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? TagNumber { get; set; }
    public string? Description { get; set; }
    public string? ConditionBefore { get; set; }
    public string? ConditionAfter { get; set; }
    public string? WorkDone { get; set; }
    public string? Notes { get; set; }
}

public class CreateServiceReportDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public DepartmentType DepartmentType { get; set; }
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
    public string? SignatureData { get; set; }
    // Pre-serialized JSON string from the frontend
    public string? Details { get; set; }

    // O5-FSR — 8-section fields
    public string? WorkSummary      { get; set; }
    public string? MaterialsUsed    { get; set; }
    public string? Recommendations  { get; set; }
    public bool    FollowUpRequired { get; set; }
    public string? FollowUpNotes    { get; set; }
    public int?    ClientRating     { get; set; }
    public List<FsrEquipmentDto> Equipment { get; set; } = new();
}

public class UpdateServiceReportDto
{
    public string? CustomerName { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public NatureOfVisit? NatureOfVisit { get; set; }
    public DateTime? StartDay { get; set; }
    public DateTime? EndDay { get; set; }
    public int? TotalMinutes { get; set; }
    public string? CustomerComments { get; set; }
    public string? SignatureData { get; set; }
    public string? Details { get; set; }

    // O5-FSR — 8-section fields (Equipment null = leave unchanged; non-null = replace the set)
    public string? WorkSummary      { get; set; }
    public string? MaterialsUsed    { get; set; }
    public string? Recommendations  { get; set; }
    public bool?   FollowUpRequired { get; set; }
    public string? FollowUpNotes    { get; set; }
    public int?    ClientRating     { get; set; }
    public List<FsrEquipmentDto>? Equipment { get; set; }
}

public class ServiceReportReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AssignmentId { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string DepartmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string? LocationAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string NatureOfVisit { get; set; } = string.Empty;
    public DateTime? StartDay { get; set; }
    public DateTime? EndDay { get; set; }
    public int? TotalMinutes { get; set; }
    public string? CustomerComments { get; set; }
    public string? SignatureData { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? CustomerSignatureName { get; set; }
    public string? CustomerSignatureData { get; set; }
    public string? TechnicianSignatureName { get; set; }
    public string? TechnicianSignatureData { get; set; }
    public string? DetailsJson { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? WorkSummary { get; set; }
    public string? MaterialsUsed { get; set; }
    public string? Recommendations { get; set; }
    public bool FollowUpRequired { get; set; }
    public string? FollowUpNotes { get; set; }
    public int? ClientRating { get; set; }
    public List<FsrEquipmentDto> Equipment { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SignServiceReportDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerSignatureData { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string TechnicianSignatureData { get; set; } = string.Empty;
    public int? ClientRating { get; set; }  // O5-FSR — client satisfaction 1–5 captured at sign-off
}

public class ReviewServiceReportDto
{
    public bool Approved { get; set; }
    public string? Comments { get; set; }
}
