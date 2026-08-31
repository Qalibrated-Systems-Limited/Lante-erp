using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReportingService.Core.DTOs;

namespace ReportingService.Infrastructure.ServiceClients;

/// <summary>
/// Shared plumbing for every upstream *ServiceClient. Forwards the CALLING USER's bearer token
/// verbatim (read off the current inbound request) rather than a service-account/internal-key —
/// the target endpoints (e.g. [Authorize(Policy = "hse.read")]) expect the real user's claims,
/// not a service identity, since reporting-service has no identity of its own.
/// </summary>
public abstract class BaseServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Name registered via services.AddHttpClient("...") in Program.cs.</summary>
    protected abstract string ClientName { get; }

    /// <summary>Configuration section key, e.g. "HseService" for HseService:BaseUrl.</summary>
    protected abstract string ConfigKey { get; }

    /// <summary>
    /// For upstreams that return the full <see cref="ApiResponse{T}"/> envelope — <c>success</c>,
    /// <c>data</c>, <c>message</c>. Yields <c>default</c> when the upstream reports failure.
    /// </summary>
    protected async Task<T?> GetAsync<T>(string path)
    {
        var response = await SendGetAsync(path);
        var wrapper = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return wrapper is { Success: true } ? wrapper.Data : default;
    }

    /// <summary>
    /// For upstreams that return a BARE <c>{ "data": ... }</c> object with no <c>success</c> field.
    ///
    /// <para>HrService does this — <c>Ok(new { data = ... })</c> in PayrollRunController and
    /// LeaveController. Read through <see cref="GetAsync{T}"/> instead, the missing <c>success</c>
    /// deserialises to <c>false</c>, the <c>is { Success: true }</c> test fails, and the method returns
    /// <c>default</c> — so a perfectly good 200 with a full payload arrives as <c>null</c>. Nothing
    /// throws, so the caller's warning list stays empty and the report renders as a clean success
    /// showing zero. A payroll cost report reading KES 0 is worse than one that fails outright, which
    /// is why this is a separate method rather than a looser test inside GetAsync: relaxing GetAsync to
    /// accept a missing <c>success</c> would also make every genuine <c>success: false</c> from the
    /// other seven upstreams indistinguishable from a good empty response.</para>
    /// </summary>
    protected async Task<T?> GetUnwrappedAsync<T>(string path)
    {
        var response = await SendGetAsync(path);
        var wrapper = await response.Content.ReadFromJsonAsync<DataEnvelope<T>>(JsonOptions);
        return wrapper is null ? default : wrapper.Data;
    }

    private async Task<HttpResponseMessage> SendGetAsync(string path)
    {
        var baseUrl = config[$"{ConfigKey}:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException($"{ConfigKey}:BaseUrl is not configured.");

        var client = httpClientFactory.CreateClient(ClientName);
        var token = httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}{path}");
        if (!string.IsNullOrEmpty(token))
            request.Headers.TryAddWithoutValidation("Authorization", token);

        logger.LogDebug("Calling {ConfigKey} {Path}", ConfigKey, path);
        var response = await client.SendAsync(request);
        // Non-2xx must throw so the caller records a warning. Returning empty on a 403 would report
        // "no payroll runs" to a user who simply lacks hr.payroll.read.
        response.EnsureSuccessStatusCode();
        return response;
    }

    /// <summary>Bare <c>{ "data": ... }</c> shape. See <see cref="GetUnwrappedAsync{T}"/>.</summary>
    private sealed class DataEnvelope<T>
    {
        public T? Data { get; set; }
    }

    protected static string BuildQuery(Dictionary<string, string?> parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }
}
