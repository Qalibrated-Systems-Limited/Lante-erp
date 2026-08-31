namespace FinanceService.Core.Enums;

/// IFRS/accounting classification of a chart-of-accounts node.
public enum AccountClassification { Asset, Liability, Equity, Income, Expense }

/// The side a balance naturally sits on.
public enum NormalBalance { Debit, Credit }

public enum FiscalYearStatus { Open, Closed }

/// A period is Open (postable), Closed (checklist done, CFO signed) then Locked (no backdating).
public enum PeriodStatus { Open, Closed, Locked }

/// Journal lifecycle. Approval posts; posted entries can only be reversed (FIN segregation of duties).
public enum JournalStatus { Draft, PendingReview, PendingApproval, Posted, Reversed }

/// Where a currency's exchange rate came from.
public enum CurrencySource { Manual, Api }

/// Annual is the original plan for a fiscal year; Revised supersedes it (see Budget.IsActive).
public enum BudgetType { Annual, Revised }
