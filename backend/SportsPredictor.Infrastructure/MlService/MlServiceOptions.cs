namespace SportsPredictor.Infrastructure.MlService;

public sealed class MlServiceOptions
{
    public const string SectionName = "MlService";

    public required string BaseUrl { get; set; }

    /// <summary>Training can take a while; longer default than the API-Football client's.</summary>
    public int TimeoutSeconds { get; set; } = 300;
}
