namespace CrmService.Core.Enums;

// C12 (P13) — legal & contract register.

public enum NdaStatus { Active, Expired, Terminated }
public enum FrameworkStatus { Active, UnderReview, Renewed, Expired, Terminated }
public enum SubcontractStatus { Active, Completed, Expired, Terminated }

// Carrier vetting is a GATE — a carrier may not be used until Approved.
public enum CarrierVettingStatus { Pending, Approved, Rejected }
public enum CarrierStatus { Active, Suspended, Expired }
