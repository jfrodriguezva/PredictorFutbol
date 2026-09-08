using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPredictor.Application.Common.Interfaces;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.ExternalData;
using SportsPredictor.Application.MlService;
using SportsPredictor.Application.Matches;
using SportsPredictor.Application.ModelRegistry;
using SportsPredictor.Application.Notifications;
using SportsPredictor.Application.Predictions;
using SportsPredictor.Application.Progol;
using SportsPredictor.Application.ReferenceData;
using SportsPredictor.Infrastructure.Caching;
using SportsPredictor.Infrastructure.Common;
using SportsPredictor.Infrastructure.Datasets;
using SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;
using SportsPredictor.Infrastructure.Matches;
using SportsPredictor.Infrastructure.MlService;
using SportsPredictor.Infrastructure.ModelRegistry;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Predictions;

namespace SportsPredictor.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        var connectionString = configuration.GetConnectionString("SportsPredictorDb");
        services.AddDbContext<SportsPredictorDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        services.AddScoped<IDatasetBuilderService, DatasetBuilderService>();
        services.AddScoped<IPredictionGenerationService, PredictionGenerationService>();
        services.AddScoped<IPredictionAnalysisService, PredictionAnalysisService>();
        services.AddSingleton<IAnalysisRateLimiter, AnalysisRateLimiter>();
        services.AddScoped<IMatchQueryService, MatchQueryService>();
        services.AddSingleton<IProgolOptimizer, ProgolOptimizer>();
        services.AddScoped<IValueBetNotificationService, ValueBetNotificationService>();
        services.AddHostedService<ValueBetWatcherBackgroundService>();

        AddApiFootball(services, configuration);
        AddMlService(services, configuration);

        return services;
    }

    private static void AddMlService(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MlServiceOptions>(configuration.GetSection(MlServiceOptions.SectionName));

        services.AddHttpClient<MlServiceClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptionsMonitor<MlServiceOptions>>().CurrentValue;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        // Forwards to the typed HttpClient instance above (AddHttpClient<MlServiceClient>
        // already wires HttpClient injection correctly) rather than re-resolving it.
        services.AddScoped<IMlServiceClient>(sp => sp.GetRequiredService<MlServiceClient>());
        services.AddScoped<IModelTrainingService, ModelTrainingService>();
    }

    private static void AddApiFootball(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiFootballOptions>(configuration.GetSection(ApiFootballOptions.SectionName));

        // The API key is deliberately never read from appsettings.json — only from
        // this exact environment variable, per CLAUDE.md section 4.
        services.PostConfigure<ApiFootballOptions>(options =>
            options.ApiKey = Environment.GetEnvironmentVariable("API_FOOTBALL_KEY"));

        services.AddTransient<ApiFootballAuthHandler>();
        services.AddTransient<ApiFootballRateLimitHandler>();

        services.AddHttpClient<ApiFootballClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<ApiFootballOptions>>().CurrentValue;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            })
            .AddHttpMessageHandler<ApiFootballAuthHandler>()
            .AddHttpMessageHandler<ApiFootballRateLimitHandler>();

        services.AddScoped<IApiFootballClient, ApiFootballSyncService>();
        services.AddScoped<IFootballReferenceService, FootballReferenceSyncService>();
        services.AddScoped<IFootballFixtureSyncService, FootballFixtureSyncService>();
        services.AddScoped<IFootballStandingsSyncService, FootballStandingsSyncService>();
        services.AddScoped<IFootballInjurySyncService, FootballInjurySyncService>();
        services.AddScoped<IFootballLineupSyncService, FootballLineupSyncService>();
        services.AddScoped<IFootballOddsSyncService, FootballOddsSyncService>();
        services.AddScoped<IFootballPredictionSyncService, FootballPredictionSyncService>();
    }
}
