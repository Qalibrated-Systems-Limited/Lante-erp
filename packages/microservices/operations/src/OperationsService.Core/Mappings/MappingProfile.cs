using AutoMapper;
using OperationsService.Core.DTOs.Approvals;
using OperationsService.Core.Enums;
using OperationsService.Core.DTOs.Assignments;
using OperationsService.Core.DTOs.Attachments;
using OperationsService.Core.DTOs.Budget;
using OperationsService.Core.DTOs.CheckIns;
using OperationsService.Core.DTOs.Claims;
using OperationsService.Core.DTOs.DailySummaries;
using OperationsService.Core.DTOs.Financial;
using OperationsService.Core.DTOs.Milestones;
using OperationsService.Core.DTOs.Performance;
using OperationsService.Core.DTOs.Photos;
using OperationsService.Core.DTOs.Projects;
using OperationsService.Core.DTOs.Requisitions;
using OperationsService.Core.DTOs.Resources;
using OperationsService.Core.DTOs.ServiceReports;
using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.Handovers;
using OperationsService.Core.DTOs.Negligence;
using OperationsService.Core.DTOs.Tasks;
using OperationsService.Core.DTOs.Timesheets;
using OperationsService.Core.DTOs.Variations;
using OperationsService.Core.Entities;

namespace OperationsService.Core.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Projects
        CreateMap<Project, ProjectReadDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RiskLevel, o => o.MapFrom(s => s.RiskLevel.ToString()))
            .ForMember(d => d.MilestoneCount, o => o.MapFrom(s => s.Milestones.Count))
            .ForMember(d => d.TaskCount, o => o.MapFrom(s => s.Milestones.Sum(m => m.Tasks.Count)))
            .ForMember(d => d.Milestones, o => o.Ignore())    // populated only on detail (GetByIdAsync)
            .ForMember(d => d.Approvals, o => o.Ignore());    // populated only on detail (GetByIdAsync)
        CreateMap<CreateProjectDto, Project>();
        // PATCH semantics. The obvious `.ForAllMembers(Condition(srcMember != null))` does NOT work
        // where a nullable DTO member maps onto a non-nullable entity member: that overload hands the
        // condition the DESTINATION-typed value, so an omitted int? arrives as 0, the guard passes,
        // and the entity is overwritten with a default. That silently reset milestone Order to 0,
        // IsBillable to false and DueDate to 0001-01-01 whenever a caller PATCHed only ProgressPct.
        // Member-level PreCondition receives the SOURCE, so HasValue is checked on what was actually
        // sent. Reference-typed members are safe under Condition and are left to ForAllMembers.
        CreateMap<UpdateProjectDto, Project>()
            .ForMember(d => d.RiskLevel,       o => o.PreCondition((UpdateProjectDto s) => s.RiskLevel.HasValue))
            .ForMember(d => d.ContractValue,   o => o.PreCondition((UpdateProjectDto s) => s.ContractValue.HasValue))
            .ForMember(d => d.PlannedBudget,   o => o.PreCondition((UpdateProjectDto s) => s.PlannedBudget.HasValue))
            .ForMember(d => d.LdRatePerDay,    o => o.PreCondition((UpdateProjectDto s) => s.LdRatePerDay.HasValue))
            .ForMember(d => d.LdCapPct,        o => o.PreCondition((UpdateProjectDto s) => s.LdCapPct.HasValue))
            .ForMember(d => d.StartDate,       o => o.PreCondition((UpdateProjectDto s) => s.StartDate.HasValue))
            .ForMember(d => d.ExpectedEndDate, o => o.PreCondition((UpdateProjectDto s) => s.ExpectedEndDate.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Milestones
        CreateMap<Milestone, MilestoneReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.TaskCount, o => o.MapFrom(s => s.Tasks.Count))
            .ForMember(d => d.CompletedTaskCount, o => o.MapFrom(s => s.Tasks.Count(t => t.Status.ToString() == "Completed")));
        CreateMap<CreateMilestoneDto, Milestone>();
        CreateMap<UpdateMilestoneDto, Milestone>()
            .ForMember(d => d.Order,       o => o.PreCondition((UpdateMilestoneDto s) => s.Order.HasValue))
            .ForMember(d => d.DueDate,     o => o.PreCondition((UpdateMilestoneDto s) => s.DueDate.HasValue))
            .ForMember(d => d.Status,      o => o.PreCondition((UpdateMilestoneDto s) => s.Status.HasValue))
            .ForMember(d => d.IsBillable,  o => o.PreCondition((UpdateMilestoneDto s) => s.IsBillable.HasValue))
            .ForMember(d => d.LdApplies,   o => o.PreCondition((UpdateMilestoneDto s) => s.LdApplies.HasValue))
            .ForMember(d => d.ProgressPct, o => o.PreCondition((UpdateMilestoneDto s) => s.ProgressPct.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Tasks
        CreateMap<ProjectTask, TaskReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
        CreateMap<CreateTaskDto, ProjectTask>();
        CreateMap<UpdateTaskDto, ProjectTask>()
            .ForMember(d => d.Status, o => o.PreCondition((UpdateTaskDto s) => s.Status.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Assignments
        CreateMap<Assignment, AssignmentReadDto>()
            .ForMember(d => d.SourceType, o => o.MapFrom(s => s.SourceType.ToString()))
            .ForMember(d => d.DepartmentType, o => o.MapFrom(s => s.DepartmentType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Priority, o => o.MapFrom(s => s.Priority.ToString()))
            .ForMember(d => d.NatureOfVisit, o => o.MapFrom(s => s.NatureOfVisit.ToString()));
        CreateMap<AssignedTechnician, AssignedTechnicianDto>();
        CreateMap<CreateAssignmentDto, Assignment>()
            .ForMember(d => d.DepartmentType, o => o.MapFrom(s => ParseDepartmentType(s.DepartmentType)));
        // Same PATCH-semantics defect as the project/milestone/task maps above: Priority and
        // NatureOfVisit are nullable on the DTO but not on the entity, so an omitted value arrived
        // as 0 and silently rewrote an Urgent job to Low and a Calibration visit to Installation.
        // NatureOfVisit also drives DetermineSheetType, so a reset could change which calibration
        // data sheet a lab work order uses.
        CreateMap<UpdateAssignmentDto, Assignment>()
            .ForMember(d => d.Priority,      o => o.PreCondition((UpdateAssignmentDto s) => s.Priority.HasValue))
            .ForMember(d => d.NatureOfVisit, o => o.PreCondition((UpdateAssignmentDto s) => s.NatureOfVisit.HasValue))
            .ForAllMembers(o => o.Condition((src, dest, member) => member != null));

        // Check-ins
        CreateMap<CheckIn, CheckInReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.CheckInLatitude, o => o.MapFrom(s => s.Latitude))
            .ForMember(d => d.CheckInLongitude, o => o.MapFrom(s => s.Longitude))
            .ForMember(d => d.CheckedInAt, o => o.MapFrom(s => s.CheckInTime))
            .ForMember(d => d.CheckedOutAt, o => o.MapFrom(s => s.CheckOutTime))
            .ForMember(d => d.CheckOutLatitude, o => o.Ignore())
            .ForMember(d => d.CheckOutLongitude, o => o.Ignore())
            .ForMember(d => d.DurationMinutes, o => o.MapFrom(s =>
                s.CheckOutTime.HasValue
                    ? (int?)(s.CheckOutTime.Value - s.CheckInTime).TotalMinutes
                    : null));

        // Photos
        CreateMap<Photo, PhotoReadDto>()
            .ForMember(d => d.PhotoType, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Url, o => o.MapFrom(s => s.StorageUrl ?? s.FilePath))
            .ForMember(d => d.UploadedAt, o => o.MapFrom(s => s.CreatedAt));

        // Service Reports
        CreateMap<FsrEquipment, FsrEquipmentDto>();

        // O4 — Timesheets
        CreateMap<TimesheetEntry, TimesheetEntryReadDto>()
            .ForMember(d => d.Source, o => o.MapFrom(s => s.Source.ToString()));
        CreateMap<Timesheet, TimesheetReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // O9 — Handover + negligence
        CreateMap<ProjectHandoverSignature, HandoverSignatureReadDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()));
        CreateMap<ProjectHandover, HandoverReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.MissingMandatoryRoles, o => o.Ignore());
        CreateMap<NegligenceResponse, NegligenceResponseReadDto>();
        CreateMap<NegligenceIncident, NegligenceIncidentReadDto>()
            .ForMember(d => d.Severity, o => o.MapFrom(s => s.Severity.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));

        // O7 — Variation orders
        CreateMap<VariationOrderLine, VariationOrderLineDto>();
        CreateMap<VariationOrder, VariationOrderReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.BudgetCategory, o => o.MapFrom(s => s.BudgetCategory.ToString()));

        // O6 — Calibration
        CreateMap<CreateReferenceStandardDto, ReferenceStandard>();
        CreateMap<ReferenceStandard, ReferenceStandardReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.IsExpired, o => o.Ignore());
        CreateMap<CalibrationCertificate, CalibrationCertificateReadDto>();
        CreateMap<ServiceReport, ServiceReportReadDto>()
            .ForMember(d => d.DepartmentType, o => o.MapFrom(s => s.DepartmentType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.NatureOfVisit, o => o.MapFrom(s => s.NatureOfVisit.ToString()));

        // Requisitions
        CreateMap<Requisition, RequisitionReadDto>()
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RequestedByUserId, o => o.MapFrom(s => s.TechnicianId))
            .ForMember(d => d.RequestedByName, o => o.MapFrom(s => s.RequestedByName))
            .ForMember(d => d.ApprovedAmount, o => o.MapFrom(s => s.ApprovedAmount))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.TmReviewedBy))
            .ForMember(d => d.ManagerReviewedAt, o => o.MapFrom(s => s.TmReviewedAt))
            .ForMember(d => d.ManagerComments, o => o.MapFrom(s => s.TmComments))
            .ForMember(d => d.ReviewedByCfoId, o => o.MapFrom(s => s.CfoReviewedBy))
            .ForMember(d => d.CfoReviewedAt, o => o.MapFrom(s => s.CfoReviewedAt))
            .ForMember(d => d.CfoComments, o => o.MapFrom(s => s.CfoComments))
            .ForMember(d => d.LineItems, o => o.MapFrom(s =>
                string.IsNullOrEmpty(s.LineItemsJson)
                    ? new List<RequisitionLineItemDto>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<RequisitionLineItemDto>>(s.LineItemsJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<RequisitionLineItemDto>()));

        // Claims
        CreateMap<Claim, ClaimReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ClaimantUserId, o => o.MapFrom(s => s.TechnicianId))
            .ForMember(d => d.ClaimantName, o => o.MapFrom(s => s.TechnicianName))
            .ForMember(d => d.ApprovedAmount, o => o.MapFrom(s => s.ApprovedAmount))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.ManagerReviewedBy))
            .ForMember(d => d.ReviewedByCfoId, o => o.MapFrom(s => s.CfoReviewedBy))
            .ForMember(d => d.ReceiptUrl, o => o.MapFrom(s => s.AttachmentPath));

        // Petty cash
        CreateMap<PettyCashAdvanceForm, PettyCashReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RequestedByUserId, o => o.MapFrom(s => s.PreparedBy))
            .ForMember(d => d.RequestedByName, o => o.MapFrom(s => s.RequestedByName))
            .ForMember(d => d.Amount, o => o.MapFrom(s => s.Sum))
            .ForMember(d => d.ApprovedAmount, o => o.MapFrom(s => s.ApprovedAmount))
            .ForMember(d => d.Purpose, o => o.MapFrom(s => s.Description))
            .ForMember(d => d.ReviewComments, o => o.MapFrom(s => s.ApprovalComments))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.ApprovedBy))
            .ForMember(d => d.ManagerReviewedAt, o => o.MapFrom(s => s.ApprovedAt))
            .ForMember(d => d.ReviewedByCfoId, o => o.MapFrom(s => s.CfoReviewedBy))
            .ForMember(d => d.CfoReviewedAt, o => o.MapFrom(s => s.CfoReviewedAt));

        // Per diem
        CreateMap<PerDiemReturnForm, PerDiemReturnReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.SubmittedByUserId, o => o.MapFrom(s => s.TechnicianId))
            .ForMember(d => d.SubmittedByName, o => o.MapFrom(s => s.SubmittedByName))
            .ForMember(d => d.TotalAdvanced, o => o.MapFrom(s => s.TotalAmount))
            .ForMember(d => d.TotalSpent, o => o.MapFrom(s => s.TotalSpent))
            .ForMember(d => d.Balance, o => o.MapFrom(s => s.TotalAmount - s.TotalSpent))
            .ForMember(d => d.Notes, o => o.MapFrom(s => s.Notes))
            .ForMember(d => d.DetailsJson, o => o.MapFrom(s => s.DetailsJson))
            .ForMember(d => d.ManagerComments, o => o.MapFrom(s => s.ManagerComments))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.ApprovedBy))
            .ForMember(d => d.ManagerReviewedAt, o => o.MapFrom(s => s.ApprovedAt))
            .ForMember(d => d.CfoComments, o => o.MapFrom(s => s.CfoComments))
            .ForMember(d => d.ReviewedByCfoId, o => o.MapFrom(s => s.CfoReviewedBy))
            .ForMember(d => d.CfoReviewedAt, o => o.MapFrom(s => s.CfoReviewedAt))
            .ForMember(d => d.LineItems, o => o.MapFrom(s =>
                string.IsNullOrEmpty(s.LineItemsJson)
                    ? new List<PerDiemLineItemDto>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<PerDiemLineItemDto>>(s.LineItemsJson,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new List<PerDiemLineItemDto>()));

        // Advance returns
        CreateMap<AdvanceReturnForm, AdvanceReturnReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.SubmittedByUserId, o => o.MapFrom(s => s.TechnicianId))
            .ForMember(d => d.SubmittedByName, o => o.MapFrom(s => s.SubmittedByName))
            .ForMember(d => d.TotalAdvanced, o => o.MapFrom(s => s.TotalAmount))
            .ForMember(d => d.TotalAccountedFor, o => o.MapFrom(s => s.TotalAccountedFor))
            .ForMember(d => d.AmountReturned, o => o.MapFrom(s => s.AmountReturned))
            .ForMember(d => d.Notes, o => o.MapFrom(s => s.Notes))
            .ForMember(d => d.DetailsJson, o => o.MapFrom(s => s.DetailsJson))
            .ForMember(d => d.ManagerComments, o => o.MapFrom(s => s.ManagerComments))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.ApprovedBy))
            .ForMember(d => d.ManagerReviewedAt, o => o.MapFrom(s => s.ApprovedAt))
            .ForMember(d => d.CfoComments, o => o.MapFrom(s => s.CfoComments))
            .ForMember(d => d.ReviewedByCfoId, o => o.MapFrom(s => s.CfoReviewedBy))
            .ForMember(d => d.CfoReviewedAt, o => o.MapFrom(s => s.CfoReviewedAt))
            .ForMember(d => d.LineItems, o => o.MapFrom(s => s.LineItems.Select(li => new AdvanceReturnLineItemDto
            {
                Description = li.Description,
                Amount = li.Amount,
                ReceiptUrl = li.ReceiptNumber
            }).ToList()));

        // Refunds
        CreateMap<Refund, RefundReadDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.RequestedByUserId, o => o.MapFrom(s => s.TechnicianId))
            .ForMember(d => d.RequestedByName, o => o.MapFrom(s => s.TechnicianName))
            .ForMember(d => d.Reason, o => o.MapFrom(s => s.Description))
            .ForMember(d => d.ReceiptUrl, o => o.MapFrom(s => s.AttachmentPath))
            .ForMember(d => d.ReviewComments, o => o.MapFrom(s =>
                new[] { s.ManagerComments, s.CfoComments }
                    .Where(c => !string.IsNullOrEmpty(c))
                    .FirstOrDefault()))
            .ForMember(d => d.ReviewedByManagerId, o => o.MapFrom(s => s.ManagerReviewedBy))
            .ForMember(d => d.ProcessedAt, o => o.MapFrom(s => s.ReceivedAt));

        // Project approvals
        CreateMap<ProjectApproval, ProjectApprovalReadDto>()
            .ForMember(d => d.ApprovalType, o => o.MapFrom(s => s.ApprovalType.ToString()))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ReviewedByName, o => o.Ignore())
            .ForMember(d => d.AllocatedBudget, o => o.Ignore());

        // Budget
        CreateMap<BudgetLine, BudgetLineReadDto>()
            .ForMember(d => d.Category, o => o.MapFrom(s => s.Category.ToString()))
            .ForMember(d => d.Variance, o => o.MapFrom(s => s.PlannedAmount - s.ActualAmount));
        CreateMap<CreateBudgetLineDto, BudgetLine>();

        CreateMap<ProjectAlertLog, ProjectAlertLogReadDto>();
        CreateMap<MilestoneUpdateLog, MilestoneUpdateLogReadDto>();

        CreateMap<ProjectHistory, ProjectHistoryReadDto>()
            .ForMember(d => d.FromValue, o => o.MapFrom(s => s.OldValue))
            .ForMember(d => d.ToValue,   o => o.MapFrom(s => s.NewValue));

        CreateMap<CreateProjectDailyReportDto, ProjectDailyReport>();
        CreateMap<ProjectDailyReport, ProjectDailyReportReadDto>();
        CreateMap<CostEntry, CostEntryReadDto>()
            .ForMember(d => d.Category, o => o.MapFrom(s => s.Category.ToString()))
            .ForMember(d => d.MilestoneTitle, o => o.MapFrom(s => s.Milestone != null ? s.Milestone.Title : null))
            .ForMember(d => d.RecordedByUserId, o => o.MapFrom(s => s.CreatedBy));

        // Resources
        CreateMap<ProjectResource, ProjectResourceReadDto>()
            .ForMember(d => d.AddedAt, o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.IsActive, o => o.MapFrom(s => !s.IsDeleted));

        // Attachments
        CreateMap<Attachment, AttachmentReadDto>()
            .ForMember(d => d.Url, o => o.MapFrom(s => s.StorageUrl))
            .ForMember(d => d.UploadedAt, o => o.MapFrom(s => s.CreatedAt))
            .ForMember(d => d.UploadedByName, o => o.Ignore());

        // Daily summaries
        CreateMap<DailySummary, DailySummaryReadDto>();

        // Performance metrics. TechnicianName/DepartmentId/Month/Year aren't entity properties --
        // PerformanceService fills them in after mapping (see ToReadDtoAsync).
        CreateMap<PerformanceMetrics, PerformanceMetricsReadDto>()
            .ForMember(d => d.TechnicianName, o => o.Ignore())
            .ForMember(d => d.DepartmentId, o => o.Ignore())
            .ForMember(d => d.Month, o => o.Ignore())
            .ForMember(d => d.Year, o => o.Ignore())
            .ForMember(d => d.ComputedAt, o => o.MapFrom(s => s.UpdatedAt));
    }

    private static DepartmentType ParseDepartmentType(string? value) =>
        Enum.TryParse<DepartmentType>(value, true, out var result) ? result : DepartmentType.General;
}
