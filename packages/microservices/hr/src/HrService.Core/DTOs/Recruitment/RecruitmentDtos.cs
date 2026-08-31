namespace HrService.Core.DTOs.Recruitment;

/// <summary>The result shape every H12 write returns, matching H5–H11.</summary>
public record RecruitmentActionResult(string Status, string Message, string? Id = null)
{
    public List<string> Warnings { get; init; } = [];
}

// ══════════════════════════════════════════════════════════════════════════════
// Requisitions
// ══════════════════════════════════════════════════════════════════════════════
public class JobRequisitionDto
{
    public string Id { get; set; } = string.Empty;
    public string RequisitionNumber { get; set; } = string.Empty;
    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? BranchId { get; set; }
    public string RequisitionType { get; set; } = string.Empty;
    public int HeadcountRequested { get; set; }
    public string? ReplacingEmployeeId { get; set; }
    public string? ReplacingEmployeeName { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime? ContractEndDate { get; set; }
    public string? Justification { get; set; }
    public DateTime? RequiredBy { get; set; }

    public int? ApprovedHeadcount { get; set; }
    public int CurrentHeadcount { get; set; }
    public int CommittedHeadcount { get; set; }
    public string? EstablishmentNotes { get; set; }
    public bool ExceedsEstablishment { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime? RaisedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Vacancies already raised from this requisition, and the heads they account for.</summary>
    public int VacanciesRaised { get; set; }
    public int HeadcountPosted { get; set; }
    /// <summary>Approved heads not yet put into a vacancy.</summary>
    public int HeadcountRemaining { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class RaiseRequisitionDto
{
    public string PositionId { get; set; } = string.Empty;
    /// <summary>Replacement | NewRole | Expansion.</summary>
    public string RequisitionType { get; set; } = "Replacement";
    public int HeadcountRequested { get; set; } = 1;
    public string? ReplacingEmployeeId { get; set; }
    /// <summary>Permanent | FixedTerm | Contract | Intern | Casual.</summary>
    public string? EmploymentType { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string? BranchId { get; set; }
    public string? Justification { get; set; }
    public DateTime? RequiredBy { get; set; }
    /// <summary>Submit for approval straight away rather than leaving it in draft.</summary>
    public bool SubmitNow { get; set; }
}

public class DecideRequisitionDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
    /// <summary>Approve fewer heads than were asked for. Defaults to the number requested.</summary>
    public int? ApprovedHeadcount { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
// Vacancies
// ══════════════════════════════════════════════════════════════════════════════
public class VacancyDto
{
    public string Id { get; set; } = string.Empty;
    public string VacancyNumber { get; set; } = string.Empty;
    public string JobRequisitionId { get; set; } = string.Empty;
    public string? RequisitionNumber { get; set; }
    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int Headcount { get; set; }
    public int HiredCount { get; set; }
    public string PostingChannel { get; set; } = string.Empty;
    public string? JobDescription { get; set; }
    public string? MinimumQualifications { get; set; }
    public string? Responsibilities { get; set; }
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public DateTime PostedAt { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public string? ClosureReason { get; set; }

    // ── Pipeline counts, so a list of vacancies is readable without opening each one ──
    public int Applicants { get; set; }
    public int Shortlisted { get; set; }
    public int Interviewing { get; set; }
    public int OffersOut { get; set; }
    public int Rejected { get; set; }
    /// <summary>True once the closing date has passed — applications are refused from here.</summary>
    public bool ClosedToApplications { get; set; }
    public int? DaysToClose { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class PostVacancyDto
{
    public string JobRequisitionId { get; set; } = string.Empty;
    /// <summary>Defaults to the requisition's remaining approved heads.</summary>
    public int? Headcount { get; set; }
    /// <summary>Internal | External | Both.</summary>
    public string? PostingChannel { get; set; }
    public string? JobDescription { get; set; }
    public string? MinimumQualifications { get; set; }
    public string? Responsibilities { get; set; }
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }
    public DateTime? ClosingDate { get; set; }
}

public class CloseVacancyDto
{
    public string? Reason { get; set; }
    /// <summary>Cancel outright rather than close. Refused once anyone has been hired against it.</summary>
    public bool Cancel { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
// Applicants
// ══════════════════════════════════════════════════════════════════════════════
public class ApplicantDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicantNumber { get; set; } = string.Empty;
    public string VacancyId { get; set; } = string.Empty;
    public string? VacancyNumber { get; set; }
    public string? PositionTitle { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ReferredByEmployeeId { get; set; }
    public string? ReferredByName { get; set; }
    public string? InternalEmployeeId { get; set; }
    public string? CvDocumentPath { get; set; }
    public string? CoverNote { get; set; }
    public int? YearsExperience { get; set; }
    public string? HighestQualification { get; set; }
    public string? CurrentEmployer { get; set; }
    public decimal? ExpectedSalary { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public DateTime? ScreenedAt { get; set; }
    public string? ScreeningNotes { get; set; }
    public decimal? AverageInterviewScore { get; set; }
    public int InterviewsHeld { get; set; }
    public int InterviewsScheduled { get; set; }
    public string? RejectionReason { get; set; }
    public string? RejectedAtStage { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? ResultingEmployeeId { get; set; }
    public string? ResultingEmployeeNumber { get; set; }
    public DateTime? HiredAt { get; set; }

    /// <summary>The live offer, when there is one.</summary>
    public string? OfferId { get; set; }
    public string? OfferNumber { get; set; }
    public string? OfferStatus { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class ReceiveApplicationDto
{
    public string VacancyId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    /// <summary>Website | JobBoard | Referral | Internal | Agency | WalkIn.</summary>
    public string? Source { get; set; }
    public string? ReferredByEmployeeId { get; set; }
    /// <summary>Set when an existing employee is applying — a promotion, not a new person.</summary>
    public string? InternalEmployeeId { get; set; }
    public string? CvDocumentPath { get; set; }
    public string? CoverNote { get; set; }
    public int? YearsExperience { get; set; }
    public string? HighestQualification { get; set; }
    public string? CurrentEmployer { get; set; }
    public decimal? ExpectedSalary { get; set; }
}

public class ScreenApplicantDto
{
    /// <summary>Shortlist | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Notes { get; set; }
    /// <summary>Required when rejecting — "not selected" is not a reason.</summary>
    public string? Reason { get; set; }
}

public class RejectApplicantDto
{
    public string Reason { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════════════════════════
// Interviews
// ══════════════════════════════════════════════════════════════════════════════
public class InterviewDto
{
    public string Id { get; set; } = string.Empty;
    public string ApplicantId { get; set; } = string.Empty;
    public string? ApplicantName { get; set; }
    public string VacancyId { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? PanelMembers { get; set; }
    public bool Held { get; set; }
    public DateTime? HeldAt { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? ExperienceScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? CulturalFitScore { get; set; }
    public decimal? OverallScore { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ScoredByName { get; set; }
    public DateTime? ScoredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    /// <summary>True when the scheduled time has passed and no score has been recorded.</summary>
    public bool Overdue { get; set; }
}

public class ScheduleInterviewDto
{
    public string ApplicantId { get; set; } = string.Empty;
    /// <summary>Screening | Technical | Panel | Final.</summary>
    public string Stage { get; set; } = "Screening";
    public DateTime ScheduledAt { get; set; }
    public string? Location { get; set; }
    public string? PanelMembers { get; set; }
}

public class ScoreInterviewDto
{
    /// <summary>Each out of ten, each optional — score only what the interview actually judged.</summary>
    public decimal? TechnicalScore { get; set; }
    public decimal? ExperienceScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? CulturalFitScore { get; set; }
    /// <summary>Proceed | Hold | Reject.</summary>
    public string Recommendation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? HeldAt { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
// Offers
// ══════════════════════════════════════════════════════════════════════════════
public class JobOfferDto
{
    public string Id { get; set; } = string.Empty;
    public string OfferNumber { get; set; } = string.Empty;
    public string ApplicantId { get; set; } = string.Empty;
    public string? ApplicantName { get; set; }
    public string VacancyId { get; set; } = string.Empty;
    public string? VacancyNumber { get; set; }
    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public decimal OfferedSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    public string? SalaryStructureId { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime ProposedStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int ProbationMonths { get; set; }
    public string? Terms { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PreparedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ResponseDeadline { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? DeclineReason { get; set; }
    public string? WithdrawalReason { get; set; }
    public string? ResultingEmployeeId { get; set; }
    /// <summary>True once the response deadline has passed with no answer.</summary>
    public bool ResponseOverdue { get; set; }
    /// <summary>Where the offer sits against the vacancy's advertised range, when one was given.</summary>
    public string? SalaryRangeNote { get; set; }
    public string NextStep { get; set; } = string.Empty;
}

public class PrepareOfferDto
{
    public string ApplicantId { get; set; } = string.Empty;
    public decimal OfferedSalary { get; set; }
    public string? CurrencyCode { get; set; }
    public string? SalaryStructureId { get; set; }
    /// <summary>Permanent | FixedTerm | Contract | Intern | Casual. Defaults to the requisition's.</summary>
    public string? EmploymentType { get; set; }
    public DateTime ProposedStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int? ProbationMonths { get; set; }
    public string? Terms { get; set; }
    /// <summary>Days the candidate has to answer once it is issued. Defaults to seven.</summary>
    public int? ResponseDays { get; set; }
    public bool SubmitNow { get; set; }
}

public class DecideOfferDto
{
    /// <summary>Approve | Reject.</summary>
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class RespondToOfferDto
{
    /// <summary>Accept | Decline.</summary>
    public string Response { get; set; } = string.Empty;
    public string? Reason { get; set; }
    /// <summary>Overrides the proposed start date when the candidate negotiated a different one.</summary>
    public DateTime? StartDate { get; set; }
}

public class WithdrawOfferDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// H12 — what hiring needs that the offer does not already carry: the statutory ids and personal details an
/// employee record requires but a candidate was never asked for.
/// </summary>
public class HireApplicantDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? OtherNames { get; set; }
    /// <summary>The login identity. Defaults to the address they applied with.</summary>
    public string? WorkEmail { get; set; }
    public string? WorkPhone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? HelbNumber { get; set; }
    public string? ReportsToId { get; set; }
    /// <summary>Office | Field | Hybrid.</summary>
    public string? WorkMode { get; set; }
    /// <summary>Overrides the agreed start date, which is otherwise the hire date.</summary>
    public DateTime? HireDate { get; set; }
    /// <summary>
    /// Create the user-service login account now. Off by default: the account is provisioned during
    /// onboarding, once the signed contract is on file, via <c>POST /employees/{id}/user-account</c>.
    /// </summary>
    public bool CreateUserAccount { get; set; }
    public List<string>? RoleIds { get; set; }
}

// ══════════════════════════════════════════════════════════════════════════════
// Summary and sweep
// ══════════════════════════════════════════════════════════════════════════════
public class RecruitmentSummaryDto
{
    public int DraftRequisitions { get; set; }
    public int RequisitionsAwaitingApproval { get; set; }
    public int ApprovedRequisitions { get; set; }
    /// <summary>Approved heads with no vacancy raised against them yet.</summary>
    public int HeadcountApprovedNotPosted { get; set; }

    public int OpenVacancies { get; set; }
    public int VacanciesClosingIn7Days { get; set; }
    public int OpenHeadcount { get; set; }

    public int TotalApplicants { get; set; }
    public int AwaitingScreening { get; set; }
    public int Shortlisted { get; set; }
    public int Interviewing { get; set; }

    public int InterviewsScheduled { get; set; }
    public int InterviewsAwaitingScore { get; set; }

    public int OffersAwaitingApproval { get; set; }
    public int OffersOutstanding { get; set; }
    public int OffersOverdue { get; set; }

    public int HiresThisYear { get; set; }
    /// <summary>Offers answered this year, and how many were accepted — the acceptance rate.</summary>
    public int OffersAnsweredThisYear { get; set; }
    public int OffersAcceptedThisYear { get; set; }
    /// <summary>Mean days from application to hire, for hires made this year.</summary>
    public decimal? AverageDaysToHire { get; set; }
}

/// <summary>H12 — which sourcing channels actually produce hires, not just applications.</summary>
public class SourceEffectivenessDto
{
    public string Source { get; set; } = string.Empty;
    public int Applicants { get; set; }
    public int Shortlisted { get; set; }
    public int Hired { get; set; }
    public decimal ShortlistRate { get; set; }
    public decimal HireRate { get; set; }
}

public class RecruitmentSweepResultDto
{
    public int VacanciesClosed { get; set; }
    public int OffersLapsed { get; set; }
    public int InterviewsOverdue { get; set; }
    public List<string> Notes { get; set; } = [];
}
