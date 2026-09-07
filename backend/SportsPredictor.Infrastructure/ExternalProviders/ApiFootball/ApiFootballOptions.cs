namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

public sealed class ApiFootballOptions
{
    public const string SectionName = "ApiFootball";

    public required string BaseUrl { get; set; }

    /// <summary>
    /// Populated exclusively from the API_FOOTBALL_KEY environment variable
    /// (see DependencyInjection.AddInfrastructure), never from appsettings.json.
    /// </summary>
    public string? ApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 3;
}
