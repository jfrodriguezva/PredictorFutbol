using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Infrastructure.Caching;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Predictions;
using SportsPredictor.Infrastructure.Tests.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Predictions;

public class PredictionAnalysisServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public PredictionAnalysisServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SportsPredictorDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SportsPredictorDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeDatasetBuilderService : IDatasetBuilderService
    {
        public Task<BuildFeaturesResultDto> BuildFeaturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ExportDatasetCsvAsync(Guid trackedCompetitionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ExportGlobalDatasetCsvAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, double>> ComputeFeaturesForPredictionAsync(Guid matchId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double> { ["home_points_before"] = 40 });
    }

    private sealed class FakeMlServiceClient : IMlServiceClient
    {
        public int AnalyzeCallCount { get; private set; }

        public AnalyzeFootball1X2ResultDto ResultToReturn { get; set; } = new(
            0.6, 0.25, 0.15,
            [new ShapFeatureImpactDto("home_points_before", 0.5)],
            null,
            "Fallback narrative");

        public Task<TrainFootball1X2ResultDto> TrainFootball1X2Async(string csvContent, string modelName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(string csvContent, int windows, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PredictGoalsResultDto> PredictGoalsAsync(string csvContent, string homeTeamName, string awayTeamName, double? dixonColesRho, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PredictMatch1X2ResultDto> PredictMatch1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AnalyzeFootball1X2ResultDto> AnalyzeFootball1X2Async(
            string artifactPath, IReadOnlyDictionary<string, double> features, AnalyzeFixtureContextDto fixture, AnalyzeOddsDto? odds, CancellationToken cancellationToken)
        {
            AnalyzeCallCount++;
            return Task.FromResult(ResultToReturn);
        }
    }

    private async Task<(Match match, ModelVersion modelVersion)> SeedMatchAndModelAsync()
    {
        var sport = TestDataBuilder.Sport();
        var competition = TestDataBuilder.Competition(sport);
        var season = TestDataBuilder.Season(competition);
        var home = TestDataBuilder.Team(sport, "Home FC");
        var away = TestDataBuilder.Team(sport, "Away FC");
        var match = TestDataBuilder.Match(competition, season, home, away);
        var modelVersion = new ModelVersion
        {
            ModelName = "football_1x2",
            Sport = "Football",
            Version = "v001",
            Algorithm = "XGBoost",
            TrainedAt = DateTime.UtcNow,
            TrainingStartDate = DateTime.UtcNow.AddMonths(-6),
            TrainingEndDate = DateTime.UtcNow,
            ArtifactPath = "models/football_1x2_v001.joblib",
        };

        _dbContext.AddRange(sport, competition, season, home, away, match, modelVersion);
        await _dbContext.SaveChangesAsync();

        return (match, modelVersion);
    }

    private static PredictionAnalysisService BuildService(SportsPredictorDbContext dbContext, FakeMlServiceClient mlClient) =>
        new(dbContext, new FakeDatasetBuilderService(), mlClient, new MemoryCacheService(new MemoryCache(new MemoryCacheOptions())), new AnalysisRateLimiter(new MemoryCache(new MemoryCacheOptions())));

    [Fact]
    public async Task AnalyzeMatchAsync_CalledTwiceWithSameInputs_ReusesCachedMlServiceResult()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var mlClient = new FakeMlServiceClient();
        var service = BuildService(_dbContext, mlClient);

        await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);
        await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);

        Assert.Equal(1, mlClient.AnalyzeCallCount);
        // A new snapshot is still persisted on every call, even on a cache hit.
        Assert.Equal(2, _dbContext.PredictionExplanations.Count());
    }

    [Fact]
    public async Task AnalyzeMatchAsync_PersistsProbabilitiesAndNarrativeFromMlService()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = BuildService(_dbContext, new FakeMlServiceClient());

        var result = await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);

        Assert.Equal(0.6, result.Home);
        Assert.Equal("Fallback narrative", result.Narrative);
        Assert.Single(result.ShapTopFeatures);
    }

    [Fact]
    public async Task AnalyzeMatchAsync_SixthCallWithinWindow_ThrowsRateLimitExceeded()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = BuildService(_dbContext, new FakeMlServiceClient());

        for (var i = 0; i < 5; i++)
        {
            await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);
        }

        await Assert.ThrowsAsync<RateLimitExceededException>(
            () => service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetAnalysisHistoryForMatchAsync_ReturnsAllSnapshotsMostRecentFirst()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = BuildService(_dbContext, new FakeMlServiceClient());
        await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);
        await service.AnalyzeMatchAsync(match.Id, modelVersion.Id, CancellationToken.None);

        var history = await service.GetAnalysisHistoryForMatchAsync(match.Id, CancellationToken.None);

        Assert.Equal(2, history.Count);
        Assert.True(history[0].GeneratedAt >= history[1].GeneratedAt);
    }
}
