using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>H1 (HR-001) — EMPLOYEE_EMERGENCY_CONTACT. At most one primary contact per employee.</summary>
public class EmployeeEmergencyContact : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public bool IsPrimary { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>H1 (HR-001) — EMPLOYEE_EDUCATION.</summary>
public class EmployeeEducation : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string? FieldOfStudy { get; set; }
    public int? YearCompleted { get; set; }
    public string? CertificateUrl { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>H1 (HR-001) — EMPLOYEE_EMPLOYMENT_HISTORY: employment prior to joining.</summary>
public class EmployeeEmploymentHistory : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string EmployerName { get; set; } = string.Empty;
    public string? JobTitle { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ReasonForLeaving { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H1 (HR-002) — EMPLOYEE_DOCUMENT_VAULT. Uploaded unverified, then confirmed by a <b>second</b> HR officer
/// (P2 step 2.3): <see cref="VerifiedBy"/> must differ from <see cref="UploadedBy"/>, so the same person
/// cannot both file and attest a document.
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public EmployeeDocumentType DocumentType { get; set; } = EmployeeDocumentType.Other;
    public string? DocumentName { get; set; }
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>H3 (HR-DEC-6) — set when this document was uploaded to satisfy a leave request's document rule
    /// (medical certificate, antenatal booking, …). The vault stays the single home for employee documents
    /// instead of a parallel LEAVE_DOCUMENT table.</summary>
    public string? LeaveRequestId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }

    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNotes { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>H1 (HR-001) — EMPLOYEE_BANK_DETAIL. The primary active account is where H6 payroll pays and what
/// the H6 bank payment file exports.</summary>
public class EmployeeBankDetail : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public Employee? Employee { get; set; }
}

/// <summary>
/// H1 (HR-002/HR-028/HR-035) — EMPLOYEE_CERTIFICATION. Professional bodies (ICPAK, EBK, IHRM) with expiry
/// tracking. Per HR-DEC-6 this is the single certification table: the DFD's separate CERTIFICATE_VAULT
/// (Process 24) carried the same fields, so it is folded in here rather than duplicated.
/// <para><see cref="Alert30SentAt"/> is stamped by the H2 scheduler so the 30-day renewal alert fires once
/// rather than every day. An expired certification blocks the H8 salary increment (HR-035).</para>
/// </summary>
public class EmployeeCertification : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string CertificationName { get; set; } = string.Empty;
    public string? IssuingBody { get; set; }
    public string? CertificationNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? CertificateUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? Alert30SentAt { get; set; }

    public Employee? Employee { get; set; }
}
