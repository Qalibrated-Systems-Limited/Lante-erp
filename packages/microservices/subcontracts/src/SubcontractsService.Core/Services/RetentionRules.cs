namespace SubcontractsService.Core.Services;

/// <summary>
/// Arithmetic invariants on a certified subcontractor payment.
///
/// <para><c>PaymentRetentionsController</c> took <c>CertifiedAmount</c>, <c>RetentionHeld</c> and
/// <c>Wht</c> straight from the request body with no validation of any kind — this service has no
/// validation layer at all. Accepting a certificate whose deductions exceed the certified amount, or
/// whose retention is negative, is wrong however the figures were arrived at.</para>
///
/// <para><b>What this deliberately does not do is compute retention.</b> #367: confirmed with the
/// repo owner that the server keeps accepting the figure off the payment certificate a QS prepared,
/// rather than deriving it — the standard rate varies by contract, often steps down at practical
/// completion, and neither is in today's data model. These rules bound whatever figure arrives.</para>
/// </summary>
public static class RetentionRules
{
    /// <summary>What the subcontractor is actually paid: certified less retention and withholding.</summary>
    public static decimal NetPayable(decimal certifiedAmount, decimal retentionHeld, decimal wht) =>
        certifiedAmount - retentionHeld - wht;

    /// <summary>
    /// Throws when the figures cannot describe a real payment certificate.
    /// </summary>
    public static void Validate(decimal certifiedAmount, decimal retentionHeld, decimal wht)
    {
        if (certifiedAmount <= 0)
            throw new InvalidOperationException("Certified amount must be greater than zero.");

        // Negative deductions INCREASE what is paid out. A negative retention is not a correction,
        // it is a payment the certificate does not support — corrections belong in their own
        // certificate, where they are visible.
        if (retentionHeld < 0)
            throw new InvalidOperationException("Retention held cannot be negative.");

        if (wht < 0)
            throw new InvalidOperationException("Withholding tax cannot be negative.");

        // Equality is allowed: a certificate can net to zero when the deductions consume it. Below
        // zero it is no longer a payment, and nothing downstream treats a negative payable as a
        // debt owed back.
        if (retentionHeld + wht > certifiedAmount)
            throw new InvalidOperationException(
                $"Deductions ({retentionHeld + wht:N2}) exceed the certified amount ({certifiedAmount:N2}).");
    }
}
