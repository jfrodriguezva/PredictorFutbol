using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPredictor.Application.Common.Interfaces;
using SportsPredictor.Infrastructure;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_RegistersDateTimeProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        var dateTimeProvider = provider.GetService<IDateTimeProvider>();

        Assert.NotNull(dateTimeProvider);
    }

    [Fact]
    public void SystemDateTimeProvider_UtcNow_IsCloseToRealTime()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        var dateTimeProvider = provider.GetRequiredService<IDateTimeProvider>();

        var delta = DateTimeOffset.UtcNow - dateTimeProvider.UtcNow;
        Assert.True(Math.Abs(delta.TotalSeconds) < 5);
    }
}
