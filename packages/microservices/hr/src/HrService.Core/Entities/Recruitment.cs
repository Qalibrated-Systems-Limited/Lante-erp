using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H12 — a request to fill a post, and the approval that authorises spending on it.
/// <para><b>The establishment arithmetic is stored, not recomputed on read.</b> Approved headcount, how many
/// are currently in post, and how many seats that leaves are captured when the requisition is raised — the
/// same reasoning as H8's <c>EligibilityNotes</c>. An approver looking at this months later needs to see what
/// the raiser saw, not what the numbers happen to be today.</para>
/// <para><b>Nothing may be advertised from an unapproved requisition</b> — a vacancy is a commitment to pay
/// somebody, and the approval is where that commitment is actually made.</para>
/// </summary>
public class JobRequisition : BaseEntity
{
    /// <summary>REQ-{year}-{seq}.</summary>
    public string RequisitionNumber { get; set; } = string.Empty;

    /// <summary>The established post being filled (H1's <see cref="Position"/>) — never a free-text job title,
    /// so the establishment check has something real to count against.</summary>
    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? BranchId { get; set; }

    public RequisitionType RequisitionType { get; set; } = RequisitionType.Replacement;
    public int HeadcountRequested { get; set; } = 1;
    /// <summary>Who is being replaced, when this is a replacement. Points at the leaver; never copies them.</summary>
    public string? ReplacingEmployeeId { get; set; }
    public string? ReplacingEmployeeName { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    /// <summary>Required for FixedTerm/Contract, exactly as H1 requires it of an employee.</summary>
    public DateTime? ContractEndDate { get; set; }

    public string? Justification { get; set; }
    /// <summary>When the post is needed filled by — drives nothing automatically, but shapes the shortlist.</summary>
    public DateTime? RequiredBy { get; set; }

    // ── The establishment snapshot, taken when the requisition was raised ──
    /// <summary>The position's approved headcount at the time. Null when the position carries no establishment.</summary>
    public int? ApprovedHeadcount { get; set; }
    /// <summary>Employees actually in that position at the time.</summary>
    public int CurrentHeadcount { get; set; }
    /// <summary>Seats already promised to other live requisitions — counted so two requisitions cannot each
    /// claim the same vacant post.</summary>
    public int CommittedHeadcount { get; set; }
    /// <summary>What the arithmetic said, in words, including "this position has no approved establishment".</summary>
    public string? EstablishmentNotes { get; set; }
    /// <summary>True when approving this takes the position past its approved establishment. Not a refusal —
    /// an expansion is a legitimate decision — but the approver is told they are making it.</summary>
    public bool ExceedsEstablishment { get; set; }

    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
    public string? RaisedBy { get; set; }
    public DateTime? RaisedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? DecidedBy { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public string? CancellationReason { get; set; }

    public Position? Position { get; set; }
}

/// <summary>
/// H12 — an approved requisition put out to the market.
/// <para>A vacancy cannot exist without an approved <see cref="JobRequisition"/>, and its headcount cannot
/// exceed what was approved. Keeping the two apart matters: the requisition is the AUTHORITY to hire and
/// survives as the audit record, while the vacancy is the ADVERT, which may be reposted, closed early or
/// cancelled without touching the authority behind it.</para>
/// </summary>
public class Vacancy : BaseEntity
{
    /// <summary>VAC-{year}-{seq}.</summary>
    public string VacancyNumber { get; set; } = string.Empty;

    public string JobRequisitionId { get; set; } = string.Empty;
    public string? RequisitionNumber { get; set; }

    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? BranchId { get; set; }

    public int Headcount { get; set; } = 1;
    /// <summary>Hires made against this vacancy. Reaching <see cref="Headcount"/> fills and closes it.</summary>
    public int HiredCount { get; set; }

    public PostingChannel PostingChannel { get; set; } = PostingChannel.Both;
    public string? JobDescription { get; set; }
    public string? MinimumQualifications { get; set; }
    public string? Responsibilities { get; set; }
    public decimal? SalaryRangeMin { get; set; }
    public decimal? SalaryRangeMax { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Applications are refused after this date — the sweep closes the vacancy when it passes.</summary>
    public DateTime? ClosingDate { get; set; }

    public VacancyStatus Status { get; set; } = VacancyStatus.Open;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? ClosureReason { get; set; }

    public JobRequisition? JobRequisition { get; set; }
    public Position? Position { get; set; }
}

/// <summary>
/// H12 — somebody who applied, and how far they got.
/// <para><b>Rejected applicants are kept, with the stage and the reason they fell at.</b> A pipeline that only
/// remembers the person hired cannot answer "why not me", cannot show a fair process, and cannot tell you
/// which sourcing channel is worth the money.</para>
/// <para>An INTERNAL applicant is an existing employee: <see cref="InternalEmployeeId"/> links to their record
/// rather than copying their details, so a promotion does not fork the person into two rows.</para>
/// </summary>
public class Applicant : BaseEntity
{
    /// <summary>APP-{year}-{seq}.</summary>
    public string ApplicantNumber { get; set; } = string.Empty;

    public string VacancyId { get; set; } = string.Empty;
    public string? VacancyNumber { get; set; }
    public string? PositionTitle { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }

    public ApplicantSource Source { get; set; } = ApplicantSource.Website;
    /// <summary>The member of staff who referred them, for a referral.</summary>
    public string? ReferredByEmployeeId { get; set; }
    public string? ReferredByName { get; set; }
    /// <summary>Set when an existing employee applies — this is a promotion or transfer, not a new person.</summary>
    public string? InternalEmployeeId { get; set; }

    public string? CvDocumentPath { get; set; }
    public string? CoverNote { get; set; }
    public int? YearsExperience { get; set; }
    public string? HighestQualification { get; set; }
    public string? CurrentEmployer { get; set; }
    public decimal? ExpectedSalary { get; set; }

    public ApplicantStatus Status { get; set; } = ApplicantStatus.Applied;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public string? ScreenedBy { get; set; }
    public DateTime? ScreenedAt { get; set; }
    public string? ScreeningNotes { get; set; }

    /// <summary>Mean of every held interview's overall score, kept here so a shortlist can be ranked without
    /// reading every interview back.</summary>
    public decimal? AverageInterviewScore { get; set; }
    public int InterviewsHeld { get; set; }

    /// <summary>The stage they fell at, and why. Both, because "not selected" is not a reason.</summary>
    public string? RejectionReason { get; set; }
    public string? RejectedAtStage { get; set; }
    public DateTime? RejectedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }

    /// <summary>The H1 employee record hiring created (H12 → P1). Null until then.</summary>
    public string? ResultingEmployeeId { get; set; }
    public string? ResultingEmployeeNumber { get; set; }
    public DateTime? HiredAt { get; set; }

    public Vacancy? Vacancy { get; set; }
}

/// <summary>
/// H12 — one interview and what it concluded.
/// <para><b>Scores are stored per criterion, not just as a total.</b> Two candidates on 7/10 are not the same
/// candidate, and an unsuccessful applicant challenging the decision is owed the breakdown rather than an
/// average. The overall is the mean of whichever criteria were actually scored, so a screening call that only
/// judged experience and communication is not penalised for skipping the technical mark.</para>
/// </summary>
public class Interview : BaseEntity
{
    public string ApplicantId { get; set; } = string.Empty;
    public string? ApplicantName { get; set; }
    public string VacancyId { get; set; } = string.Empty;

    public InterviewStage Stage { get; set; } = InterviewStage.Screening;
    public DateTime ScheduledAt { get; set; }
    /// <summary>Where, or how — a room, a link, a phone call.</summary>
    public string? Location { get; set; }
    /// <summary>Who sat on the panel, as names. Panellists need not be system users.</summary>
    public string? PanelMembers { get; set; }

    public bool Held { get; set; }
    public DateTime? HeldAt { get; set; }

    // ── Scores, each out of ten, each optional ──
    public decimal? TechnicalScore { get; set; }
    public decimal? ExperienceScore { get; set; }
    public decimal? CommunicationScore { get; set; }
    public decimal? CulturalFitScore { get; set; }
    /// <summary>The mean of the criteria that were scored. Null until the interview is held.</summary>
    public decimal? OverallScore { get; set; }

    public InterviewRecommendation Recommendation { get; set; } = InterviewRecommendation.Pending;
    public string? Notes { get; set; }
    public string? ScoredBy { get; set; }
    public string? ScoredByName { get; set; }
    public DateTime? ScoredAt { get; set; }

    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Applicant? Applicant { get; set; }
}

/// <summary>
/// H12 — the offer, its approval, and the candidate's answer.
/// <para><b>The salary is approved before the offer is issued</b>, by someone other than whoever prepared it —
/// the same segregation H8 applies to increments and H10 to final dues. An offer that goes out first and is
/// approved afterwards is not an approval, it is a ratification of a promise already made.</para>
/// <para>Accepting is what creates the employee (H12 → P1), and only from an ACCEPTED offer: a hire recorded
/// from anything else has no agreed salary or start date behind it.</para>
/// </summary>
public class JobOffer : BaseEntity
{
    /// <summary>OFF-{year}-{seq}.</summary>
    public string OfferNumber { get; set; } = string.Empty;

    public string ApplicantId { get; set; } = string.Empty;
    public string? ApplicantName { get; set; }
    public string VacancyId { get; set; } = string.Empty;
    public string? VacancyNumber { get; set; }

    public string PositionId { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? JobGrade { get; set; }
    public string? DepartmentId { get; set; }
    public string? BranchId { get; set; }

    public decimal OfferedSalary { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>The H5 structure the salary will be assigned on once they are an employee.</summary>
    public string? SalaryStructureId { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public DateTime ProposedStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int ProbationMonths { get; set; } = 3;
    public string? Terms { get; set; }

    public OfferStatus Status { get; set; } = OfferStatus.Draft;

    public string? PreparedBy { get; set; }
    public DateTime? PreparedAt { get; set; }
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? IssuedAt { get; set; }
    public string? IssuedBy { get; set; }
    /// <summary>Days the candidate gets to answer, agreed when the offer is prepared. Applied at ISSUE rather
    /// than at preparation, so an offer that sits waiting for approval does not eat the candidate's window.</summary>
    public int ResponseDays { get; set; } = 7;
    /// <summary>When the answer is due. The sweep lapses the offer when it passes, so offers do not sit open
    /// for ever holding a seat nobody can fill.</summary>
    public DateTime? ResponseDeadline { get; set; }
    /// <summary>One-way stamp so a lapse is recorded once, not every morning.</summary>
    public DateTime? LapseAlertedAt { get; set; }

    public DateTime? RespondedAt { get; set; }
    public string? DeclineReason { get; set; }
    public string? WithdrawalReason { get; set; }

    /// <summary>The employee record acceptance created. Null until the hire is recorded.</summary>
    public string? ResultingEmployeeId { get; set; }

    public Applicant? Applicant { get; set; }
    public Vacancy? Vacancy { get; set; }
}
