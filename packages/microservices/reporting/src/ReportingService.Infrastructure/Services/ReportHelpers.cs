using Microsoft.Extensions.Logging;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// For multi-source reports we run upstream calls in parallel via Task.WhenAll, but one failing
/// source must not sink the others. Each call is wrapped to capture its own warning message
/// instead of throwing, and instead of mutating a shared List&lt;string&gt; concurrently (which
/// isn't thread-safe), each result carries its own warning to be merged back on the calling
/// thread after Task.WhenAll completes.
/// </summary>
internal static class ReportHelpers
{
    public static async Task<(T? Data, string? Warning)> SafeCallAsync<T>(Func<Task<T?>> call, string label, ILogger logger)
    {
        try
        {
            var data = await call();
            return (data, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch {Label}", label);
            return (default, $"Failed to fetch {label}.");
        }
    }
}
