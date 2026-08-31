using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H1 (HR-001) — EMPLOYEE, the employment master record and the anchor for every other HR table.
/// <para><b>HR-DEC-2:</b> this record owns the <i>employment</i> relationship; the login identity stays in
/// user-service and is linked through <see cref="UserId"/>. Compliance, HSE, procurement and operations all
/// already key their staff records on that user id, so HR deliberately does not re-key the estate —
/// <see cref="UserId"/> is how HR joins to them.</para>
/// <para><b>HR-DEC-3:</b> department and branch are user-service ids (that module owns them); only
/// <see cref="PositionId"/> and the reporting line are HR's own. Names are denormalised for display so
/// listing employees does not need a cross-service call per row.</para>
/// </summary>
public class Employee : BaseEntity
{
    public string EmployeeNumber { get; set; } = string.Empty;   // EMP-{yr}-{seq}

    /// <summary>The user-service account. Null until the account is created — the employee record is the
    /// master and is never blocked by an account-creation failure (see <see cref="AccountCreationError"/>).</summary>
    public string? UserId { get; set; }

    // ── Personal (HR-001) ──
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? OtherNames { get; set; }
    public string? NationalId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? PersonalEmail { get; set; }
    public string? PersonalPhone { get; set; }
    public string? PhysicalAddress { get; set; }

    // ── Statutory identifiers (needed by H6 payroll and the P9 tax certificate) ──
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? HelbNumber { get; set; }

    // ── Employment ──
    public string WorkEmail { get; set; } = string.Empty;
    public string? WorkPhone { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ConfirmationDate { get; set; }          // set by H2 on probation confirmation
    public EmploymentStatus Status { get; set; } = EmploymentStatus.OnProbation;
    /// <summary>H10 — the day they actually left. Set when a separation is PAID, not when it is approved:
    /// somebody is on the payroll until they have been paid what they are owed.</summary>
    public DateTime? ExitDate { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    /// <summary>Required for FixedTerm/Contract — drives the H2 renewal alerts (HR-006).</summary>
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    // ── Organisational placement (department/branch are user-service ids — HR-DEC-3) ──
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? PositionId { get; set; }
    public string? JobTitle { get; set; }

    /// <summary>H4 (ATT-001, P27 step 27.1 "get work_location") — decides how this person may clock in: office
    /// staff by desktop or biometric, field staff by mobile with a GPS stamp (ATT-005).</summary>
    public WorkMode WorkMode { get; set; } = WorkMode.Office;
    /// <summary>Line manager, as another employee. The single source of truth for the org chart (P32);
    /// OrgChartNode is a projection of this, not a parallel hierarchy.</summary>
    public string? ReportsToId { get; set; }

    // ── Onboarding state ──
    /// <summary>True once the mandatory documents are on file (signed contract + ID copy).</summary>
    public bool OnboardingComplete { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
    /// <summary>Why the user-service account creation last failed, if it did — kept so the retry endpoint
    /// can explain itself rather than silently doing nothing.</summary>
    public string? AccountCreationError { get; set; }

    public string FullName => string.Join(' ', new[] { FirstName, OtherNames, LastName }
        .Where(p => !string.IsNullOrWhiteSpace(p)));

    public ICollection<EmployeeEmergencyContact> EmergencyContacts { get; set; } = new List<EmployeeEmergencyContact>();
    public ICollection<EmployeeEducation> Education { get; set; } = new List<EmployeeEducation>();
    public ICollection<EmployeeEmploymentHistory> EmploymentHistory { get; set; } = new List<EmployeeEmploymentHistory>();
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<EmployeeBankDetail> BankDetails { get; set; } = new List<EmployeeBankDetail>();
    public ICollection<EmployeeCertification> Certifications { get; set; } = new List<EmployeeCertification>();
}
