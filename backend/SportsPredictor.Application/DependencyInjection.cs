using Microsoft.Extensions.DependencyInjection;

namespace SportsPredictor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
