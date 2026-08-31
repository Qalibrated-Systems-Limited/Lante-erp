namespace CrmService.Core.Enums;

// P8 — account-ownership transfer. 3 sequential approvals then a 4-signature handover, after which
// the account owner changes and open opportunities/tasks reassign.
public enum TransferStatus
{
    PendingHeadBd,     // awaiting Head of BD endorsement
    PendingCfo,        // awaiting CFO review (outstanding invoices + pricing)
    PendingMd,         // awaiting MD final written approval
    PendingHandover,   // approved — awaiting completed & signed Status Handover Document
    Completed,
    Rejected,
}
