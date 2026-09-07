namespace SportsPredictor.Application.Common.Interfaces;

/// <summary>
/// Cache abstraction. Backed by <c>IMemoryCache</c> initially; a Redis-backed
/// implementation can be swapped in later without touching callers.
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
