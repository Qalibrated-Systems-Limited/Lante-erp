namespace OperationsService.Core.Enums;

public enum HandoverStatus { Draft, InProgress, Completed }

/// <summary>The four mandatory handover signatories.</summary>
public enum HandoverSignatureRole { ProjectManager, DepartmentHead, ClientRepresentative, QualityAssurance }

public enum NegligenceStatus { Logged, UnderReview, Responded, Closed }

public enum NegligenceSeverity { Minor, Moderate, Major, Critical }
