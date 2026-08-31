using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

/// D4-2 — real SMS provider (Tiara Connect, https://api2.tiaraconnect.io). Inert (IsEnabled
/// false) until Sms:Enabled, Sms:Tiara:ApiKey, and Sms:Tiara:SenderId are all configured.
public class TiaraSmsSender(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TiaraSmsSender> logger) : ISmsSender
{
    public bool IsEnabled =>
        config.GetSection("Sms")["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        && !string.IsNullOrWhiteSpace(config["Sms:Tiara:ApiKey"])
        && !string.IsNullOrWhiteSpace(config["Sms:Tiara:SenderId"]);

    public async Task SendAsync(string toPhone, string message, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(toPhone))
            return;

        var normalizedPhone = NormalizeKenyanMsisdn(toPhone);
        if (normalizedPhone == null)
        {
            logger.LogWarning("Skipping SMS — {Phone} isn't a recognizable Kenyan mobile number", toPhone);
            return;
        }

        try
        {
            var baseUrl = config["Sms:Tiara:BaseUrl"] ?? "https://api2.tiaraconnect.io/api/messaging/sendsms";
            var client = httpClientFactory.CreateClient("TiaraSms");
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config["Sms:Tiara:ApiKey"]);
            request.Content = JsonContent.Create(new TiaraSendSmsRequest(
                From: config["Sms:Tiara:SenderId"]!,
                To: normalizedPhone,
                Message: message,
                RefId: Guid.NewGuid().ToString("N")));

            var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Tiara SMS send failed ({StatusCode}) to {Phone}: {Body}", response.StatusCode, toPhone, body);
                return;
            }

            var result = JsonSerializer.Deserialize<TiaraSendSmsResponse>(body, JsonOpts);
            if (result?.StatusCode != "0")
                logger.LogWarning("Tiara SMS rejected for {Phone}: {Desc}", toPhone, result?.Desc);
        }
        catch (Exception ex)
        {
            // Never let an SMS-provider outage break the caller's own flow (e.g. an approval).
            logger.LogError(ex, "Tiara SMS send threw for {Phone}", toPhone);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Tiara expects "2547XXXXXXXX" (country code, no leading zero, no "+"). Numbers in our DB are
    // whatever the user typed at signup — normalize the common Kenyan formats rather than rejecting them.
    private static string? NormalizeKenyanMsisdn(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());

        // Placeholder values like "0000000000" (seeded for accounts with no real number on file)
        // pass every length/prefix check below once normalized — reject them before that so we
        // never pay Tiara to attempt a send that's guaranteed to fail.
        if (digits.Length > 0 && digits.Distinct().Count() == 1)
            return null;

        if (digits.Length == 9 && (digits.StartsWith('7') || digits.StartsWith('1')))
            return "254" + digits;
        if (digits.Length == 10 && digits.StartsWith('0') && (digits[1] == '7' || digits[1] == '1'))
            return "254" + digits[1..];
        if (digits.Length == 12 && digits.StartsWith("254") && (digits[3] == '7' || digits[3] == '1'))
            return digits;
        return null;
    }

    private record TiaraSendSmsRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("refId")] string RefId);

    private record TiaraSendSmsResponse(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("statusCode")] string? StatusCode,
        [property: JsonPropertyName("desc")] string? Desc,
        [property: JsonPropertyName("msgId")] string? MsgId);
}
