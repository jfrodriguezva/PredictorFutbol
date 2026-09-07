using Microsoft.Extensions.Caching.Memory;
using SportsPredictor.Application.Common.Interfaces;

namespace SportsPredictor.Infrastructure.Caching;

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);
        _cache.Set(key, value, ttl);
        return value;
    }
}
