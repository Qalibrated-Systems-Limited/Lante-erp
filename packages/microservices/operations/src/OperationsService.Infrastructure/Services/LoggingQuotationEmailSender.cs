using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// O5 — default quotation-email implementation: a config-gated logging stub. Until operations gets
/// real email infrastructure (SMTP/provider), sending a quotation records the intent to the log and
/// succeeds, so the SR pipeline (Draft → Sent → QuotationSent) is not blocked. Set
/// <c>QuotationEmail:Enabled=false</c> to make it a silent no-op. Swap this registration for a real
/// sender when email is wired.
/// </summary>
public class LoggingQuotationEmailSender(
    IConfiguration config,
    ILogger<LoggingQuotationEmailSender> logger) : IQuotationEmailSender
{
    public Task SendQuotationAsync(QuotationEmailModel model)
    {
        var enabled = config.GetValue("QuotationEmail:Enabled", true);
        if (!enabled)
        {
            logger.LogDebug("QuotationEmail disabled — skipping send of {Quot} for SR {Ref}",
                model.QuotationNumber, model.ReferenceNumber);
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "[QuotationEmail stub] Would email quotation {Quot} ({Label}) for SR {Ref} to {Name} <{Email}> — " +
            "{LineCount} line item(s), total {Total:0.00} (subtotal {Subtotal:0.00} + VAT {Vat:0.00}), valid until {ValidUntil}.",
            model.QuotationNumber, model.FormTypeLabel, model.ReferenceNumber, model.ToName, model.ToEmail,
            model.LineItems.Count, model.TotalAmount, model.Subtotal, model.VatAmount,
            model.ValidUntil?.ToString("yyyy-MM-dd") ?? "n/a");

        return Task.CompletedTask;
    }
}
