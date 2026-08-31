using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using UserService.Core.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public class MemoryCacheService(IMemoryCache cache) : ICacheService
{
    public Task<T?> GetAsync<T>(string key)
    {
        cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue) options.SetAbsoluteExpiration(expiry.Value);
        cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        return Task.FromResult(cache.TryGetValue(key, out _));
    }
}
