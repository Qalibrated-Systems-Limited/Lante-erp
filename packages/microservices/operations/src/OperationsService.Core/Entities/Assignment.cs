using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Assignment : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AssignmentSourceType SourceType { get; set; } = AssignmentSourceType.Standalone;
    public string? LinkedTicketId { get; set; }
    public string? LinkedProjectTaskId { get; set; }
    public string DepartmentId { get; set; } = string.Empty;
    public DepartmentType DepartmentType { get; set; }
    public string ManagerId { get; set; } = string.Empty;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
    public AssignmentPriority Priority { get; set; } = AssignmentPriority.Normal;
    public NatureOfVisit NatureOfVisit { get; set; }
    public string? ServiceType { get; set; }
    public DateTime? Deadline { get; set; }
    public string? LocationName { get; set; }
    public string? LocationAddress { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string? Notes { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    // O5-FSR — set once when the background sweep flags this completed assignment as overdue for its
    // Field Service Report (not submitted within the SLA window), so it alerts at most once.
    public DateTime? FsrOverdueAlertedAt { get; set; }

    // Service Request linkage
    public string? ServiceRequestId   { get; set; }
    public string? ServiceRequestDataJson { get; set; }

    // Standalone → project linkage
    public string? LinkedProjectId { get; set; }
    public string? LinkedMilestoneId { get; set; }
    public DateTime? LinkedAt { get; set; }
    public string? LinkedBy { get; set; }
    public string? ArchiveReason { get; set; }

    public ICollection<AssignedTechnician> Technicians { get; set; } = new List<AssignedTechnician>();
    public ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public ICollection<Photo> Photos { get; set; } = new List<Photo>();
    public ICollection<ServiceReport> ServiceReports { get; set; } = new List<ServiceReport>();
    public ICollection<Requisition> Requisitions { get; set; } = new List<Requisition>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<PettyCashAdvanceForm> PettyCashForms { get; set; } = new List<PettyCashAdvanceForm>();
    public ICollection<PerDiemReturnForm> PerDiemForms { get; set; } = new List<PerDiemReturnForm>();
    public ICollection<AdvanceReturnForm> AdvanceReturnForms { get; set; } = new List<AdvanceReturnForm>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<VehicleDispatch> VehicleDispatches { get; set; } = new List<VehicleDispatch>();
    public LabWorkOrder? LabWorkOrder { get; set; }
}
