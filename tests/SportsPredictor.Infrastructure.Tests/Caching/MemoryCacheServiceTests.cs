using Microsoft.Extensions.Caching.Memory;
using SportsPredictor.Infrastructure.Caching;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Caching;

public class MemoryCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_SecondCall_DoesNotInvokeFactoryAgain()
    {
        var service = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));
        var factoryCalls = 0;

        Task<int> Factory(CancellationToken ct)
        {
            factoryCalls++;
            return Task.FromResult(42);
        }

        var first = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));
        var second = await service.GetOrCreateAsync("key", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task GetOrCreateAsync_DifferentKeys_InvokesFactoryForEach()
    {
        var service = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()));

        var a = await service.GetOrCreateAsync("a", _ => Task.FromResult(1), TimeSpan.FromMinutes(1));
        var b = await service.GetOrCreateAsync("b", _ => Task.FromResult(2), TimeSpan.FromMinutes(1));

        Assert.Equal(1, a);
        Assert.Equal(2, b);
    }
}
