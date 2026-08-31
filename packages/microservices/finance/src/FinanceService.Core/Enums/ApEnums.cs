namespace FinanceService.Core.Enums;

public enum SupplierInvoiceStatus { Received, Approved, PartPaid, Paid, Cancelled }

/// PO → GRN → Invoice match. The match itself is owned by Procurement; Finance stores the result.
public enum ThreeWayMatchStatus { NotRequired, Pending, Matched, Exception }

public enum VoucherStatus { Draft, PendingApproval, Approved, Paid, Rejected }

public enum ApprovalAction { Approved, Rejected }
