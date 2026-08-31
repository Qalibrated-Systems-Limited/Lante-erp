namespace CrmService.Core.Enums;

// C11 (P12) — after-sales & retention.

public enum SurveyStatus { Pending, Completed, Expired }
public enum SurveySource { Manual, ProjectClose }

public enum ServiceContractType { Calibration, Maintenance, Support, Other }
public enum ServiceContractStatus { Active, Expired, Renewed, Cancelled }

public enum ComplaintSeverity { Low, Medium, High, Critical }
public enum ComplaintStatus { Open, Assigned, InProgress, Resolved, Closed }

public enum NpsCategory { None, Detractor, Passive, Promoter }
public enum NpsStatus { Pending, Completed, Expired }
