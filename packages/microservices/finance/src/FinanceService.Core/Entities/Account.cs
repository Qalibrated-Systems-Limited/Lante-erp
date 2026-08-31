using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// Seeded account types (Asset/Liability/Equity/Income/Expense) with their normal balance side.
public class AccountType : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountClassification Classification { get; set; }
    public NormalBalance NormalBalance { get; set; }
}

/// Process 3 — hierarchical chart of accounts. Only direct-posting (leaf) accounts accept journals.
public class ChartOfAccount : BaseEntity
{
    public string Code { get; set; } = string.Empty;          // e.g. "1100"
    public string Name { get; set; } = string.Empty;
    public string AccountTypeId { get; set; } = string.Empty;
    public string? ParentId { get; set; }                      // self-FK hierarchy
    public string? CurrencyId { get; set; }                    // per-currency account (null = base)
    public bool IsDirectPosting { get; set; } = true;          // header/parent accounts = false
    public bool CostCentreRequired { get; set; }
    public bool IsBank { get; set; }
    public bool IsActive { get; set; } = true;
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    public AccountType? AccountType { get; set; }
    public ChartOfAccount? Parent { get; set; }
}

/// Process 4 — cost centre (department / business line), hierarchical, optionally branch-scoped.
public class CostCenter : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public string? BranchId { get; set; }                      // string FK → user-service branch
    public string? DepartmentId { get; set; }                  // string FK → user-service department
    public bool IsActive { get; set; } = true;

    public CostCenter? Parent { get; set; }
}
