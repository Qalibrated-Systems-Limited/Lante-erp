namespace HrService.Core.DTOs.Employees;

// ── Create / update ──
public class CreateEmployeeDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? OtherNames { get; set; }
    public string WorkEmail { get; set; } = string.Empty;
    public string? WorkPhone { get; set; }

    public string? NationalId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? PersonalEmail { get; set; }
    public string? PersonalPhone { get; set; }
    public string? PhysicalAddress { get; set; }

    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? HelbNumber { get; set; }

    public DateTime HireDate { get; set; }
    /// <summary>Permanent | FixedTerm | Contract | Intern | Casual</summary>
    public string? EmploymentType { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    public string? DepartmentId { get; set; }
    public string? BranchId { get; set; }
    public string? PositionId { get; set; }
    public string? ReportsToId { get; set; }

    /// <summary>H4 — Office | Field | Hybrid. Decides how they may clock in: field staff need a GPS stamp.</summary>
    public string? WorkMode { get; set; }

    /// <summary>Create the user-service login account as part of onboarding (P1 design note). Best-effort:
    /// the employee record is still created if the account cannot be made, and can be retried.</summary>
    public bool CreateUserAccount { get; set; } = true;
    public List<string>? RoleIds { get; set; }
}

public class UpdateEmployeeDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? OtherNames { get; set; }
    public string? WorkEmail { get; set; }
    public string? WorkPhone { get; set; }
    public string? NationalId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? PersonalEmail { get; set; }
    public string? PersonalPhone { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? HelbNumber { get; set; }
    public string? EmploymentType { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string? DepartmentId { get; set; }
    public string? BranchId { get; set; }
    public string? PositionId { get; set; }
    public string? ReportsToId { get; set; }
    /// <summary>H4 — Office | Field | Hybrid.</summary>
    public string? WorkMode { get; set; }
    /// <summary>OnProbation | Active | OnLeave | Suspended | Resigned | Terminated. Confirmation (probation →
    /// active) belongs to H2 and separation to H10; this is for corrections.</summary>
    public string? Status { get; set; }
}

// ── Sub-records ──
public class EmergencyContactDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public bool IsPrimary { get; set; }
}

public class EducationDto
{
    public string Id { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? YearCompleted { get; set; }
    public string? CertificateUrl { get; set; }
}

public class EmploymentHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ReasonForLeaving { get; set; }
}

public class BankDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
}

public class DocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNotes { get; set; }
    /// <summary>True when the document has an expiry date that has passed.</summary>
    public bool IsExpired { get; set; }
}

public class CertificationDto
{
    public string Id { get; set; } = string.Empty;
    public string CertificationName { get; set; } = string.Empty;
    public string? IssuingBody { get; set; }
    public string? CertificationNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? CertificateUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime? Alert30SentAt { get; set; }
    public bool IsExpired { get; set; }
    /// <summary>Expiring within 30 days — the HR-028 renewal window.</summary>
    public bool IsExpiringSoon { get; set; }
}

// ── Reads ──
public class EmployeeReadDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
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

    public string? KraPin { get; set; }
    public string? NssfNumber { get; set; }
    public string? ShaNumber { get; set; }
    public string? HelbNumber { get; set; }

    public string WorkEmail { get; set; } = string.Empty;
    public string? WorkPhone { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ConfirmationDate { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>H10 — the day they actually left, set when the separation is paid.</summary>
    public DateTime? ExitDate { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? PositionId { get; set; }
    public string? JobTitle { get; set; }
    public string? ReportsToId { get; set; }
    public string? ReportsToName { get; set; }
    public string WorkMode { get; set; } = string.Empty;

    public bool OnboardingComplete { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
    public bool HasUserAccount { get; set; }
    public string? AccountCreationError { get; set; }
    /// <summary>Which mandatory documents (signed contract, ID copy) are still missing.</summary>
    public List<string> MissingMandatoryDocuments { get; set; } = new();
    /// <summary>Any expired professional certification — blocks the H8 salary increment (HR-035).</summary>
    public bool HasExpiredCertification { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<EmergencyContactDto> EmergencyContacts { get; set; } = new();
    public List<EducationDto> Education { get; set; } = new();
    public List<EmploymentHistoryDto> EmploymentHistory { get; set; } = new();
    public List<DocumentDto> Documents { get; set; } = new();
    public List<BankDetailDto> BankDetails { get; set; } = new();
    public List<CertificationDto> Certifications { get; set; } = new();
}

public class EmployeeRowDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string WorkEmail { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? JobTitle { get; set; }
    public string WorkMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>H10 — the day they actually left, set when the separation is paid.</summary>
    public DateTime? ExitDate { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public bool OnboardingComplete { get; set; }
    public bool HasUserAccount { get; set; }
}

public class EmployeeFilterParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? DepartmentId { get; set; }
    public string? PositionId { get; set; }
    public bool? OnboardingComplete { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public record EmployeeListResult(List<EmployeeRowDto> Items, int Total);

public class EmployeeSummaryDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int OnProbation { get; set; }
    public int OnLeave { get; set; }
    public int Suspended { get; set; }
    public int Separated { get; set; }
    public int OnboardingIncomplete { get; set; }
    public int WithoutUserAccount { get; set; }
    public int DocumentsAwaitingVerification { get; set; }
    public int CertificationsExpiringIn30Days { get; set; }
    public int ExpiredCertifications { get; set; }
}

public record EmployeeActionResult(string Status, string Message, string? EmployeeId = null);

// ── Documents & certifications ──
public class UploadDocumentDto
{
    /// <summary>SignedContract | IdCopy | AcademicCertificate | ProfessionalCertificate | MedicalRecord |
    /// KraPinCertificate | NssfCard | ShaCard | PassportPhoto | Other</summary>
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public class VerifyDocumentDto
{
    public string? Notes { get; set; }
}

public class CreateCertificationDto
{
    public string CertificationName { get; set; } = string.Empty;
    public string? IssuingBody { get; set; }
    public string? CertificationNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? CertificateUrl { get; set; }
}
