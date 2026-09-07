using Microsoft.Extensions.DependencyInjection;
using SportsPredictor.Application;
using Xunit;

namespace SportsPredictor.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddApplication();

        Assert.Same(services, result);
    }
}
