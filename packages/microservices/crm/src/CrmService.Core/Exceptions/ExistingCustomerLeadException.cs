namespace CrmService.Core.Exceptions;

/// <summary>
/// Thrown when a lead is captured for a contact who is already a customer.
///
/// This is a redirect, not a dead end: the right record for further business with an existing
/// client is an opportunity against their account, not a second lead — leads that shadow a live
/// customer split the account history and skew pipeline reporting. Carries the customer so the
/// caller can offer that action directly instead of making the user go and find them.
/// </summary>
public class ExistingCustomerLeadException(string customerId, string customerName, string message)
    : InvalidOperationException(message)
{
    public string CustomerId { get; } = customerId;
    public string CustomerName { get; } = customerName;
}
