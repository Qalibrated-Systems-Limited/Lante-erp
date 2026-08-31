namespace OperationsService.Core.Enums;

public enum RequisitionType
{
    MaterialRequisition, CashAdvance, CashReturn, PerDiemReturn
}

public enum RequisitionStatus
{
    Pending, TmApproved, TmRejected, CfoApproved, CfoRejected, CfoProcessing, Paid
}

public enum ClaimStatus
{
    Pending, ManagerApproved, ManagerRejected, CfoApproved, CfoRejected, Disbursed
}

public enum PettyCashStatus { Pending, Approved, Rejected, Disbursed }

public enum ReturnFormStatus { Pending, Approved, Rejected }

public enum RefundStatus
{
    Pending, ManagerApproved, ManagerRejected, CfoReceived, CfoRejected
}

public enum AlertLevel { None, Amber, Red }

public enum PaymentMethod { Cash, BankTransfer, Mpesa, Cheque, Other }
