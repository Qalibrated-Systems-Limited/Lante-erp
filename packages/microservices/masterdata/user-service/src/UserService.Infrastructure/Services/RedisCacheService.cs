using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UserService.Core.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public class RedisCacheService(IDistributedCache cache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key)
    {
        var bytes = await cache.GetAsync(key);
        if (bytes == null) return default;
        return JsonSerializer.Deserialize<T>(bytes);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var options = new DistributedCacheEntryOptions();
        if (expiry.HasValue) options.SetAbsoluteExpiration(expiry.Value);
        await cache.SetAsync(key, bytes, options);
    }

    public async Task RemoveAsync(string key)
    {
        await cache.RemoveAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        var bytes = await cache.GetAsync(key);
        return bytes != null;
    }
}
