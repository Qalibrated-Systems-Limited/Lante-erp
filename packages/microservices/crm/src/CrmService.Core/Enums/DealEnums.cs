namespace CrmService.Core.Enums;

// P6 — deal close. A deal is created from a Won opportunity; closing it fires the Finance invoice
// (one-way latch) and can one-click create the Module 5 project.
public enum DealStatus
{
    Open,
    Closed,
    Cancelled,
}

public enum ContractStatus
{
    Draft,
    Active,
    Expired,
    Terminated,
    Renewed,
}
