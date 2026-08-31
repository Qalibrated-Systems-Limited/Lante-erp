namespace TicketingService.Core.Interfaces.Services;

/// D4-2 — outbound SMS channel. Gated by config ("Sms:Enabled"); the default implementation is a
/// logging no-op until a real provider (Africa's Talking / Twilio) and credentials are wired in.
public interface ISmsSender
{
    /// True when a provider is configured and enabled — callers check this before composing a message.
    bool IsEnabled { get; }

    /// Send an SMS. Never throws — failures are logged and swallowed so they can't break the caller.
    Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default);
}
