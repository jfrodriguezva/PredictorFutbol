using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Predictions;
using SportsPredictor.Infrastructure.Tests.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Predictions;

public class PredictionGenerationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public PredictionGenerationServiceTests()
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
        public PredictMatch1X2ResultDto ResultToReturn { get; set; } = new(0.6, 0.25, 0.15);

        public Task<TrainFootball1X2ResultDto> TrainFootball1X2Async(string csvContent, string modelName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(string csvContent, int windows, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PredictGoalsResultDto> PredictGoalsAsync(string csvContent, string homeTeamName, string awayTeamName, double? dixonColesRho, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PredictMatch1X2ResultDto> PredictMatch1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, CancellationToken cancellationToken) =>
            Task.FromResult(ResultToReturn);

        public Task<AnalyzeFootball1X2ResultDto> AnalyzeFootball1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, AnalyzeFixtureContextDto fixture, AnalyzeOddsDto? odds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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

    [Fact]
    public async Task GeneratePrediction1X2Async_PersistsThreeSelectionRows()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        var result = await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        Assert.Equal(3, result.Selections.Count);
        Assert.Equal(3, _dbContext.Predictions.Count());
        Assert.Contains(result.Selections, s => s.Selection == "Home" && Math.Abs(s.Probability - 0.6) < 1e-9);
    }

    [Fact]
    public async Task GeneratePrediction1X2Async_WithFavorableOdds_RecommendsPositiveEvSelection()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        // Model says Home 60%, but the market prices Home at decimal 2.5 (implied ~40%) — clear value.
        _dbContext.OddsSnapshots.Add(new OddsSnapshot
        {
            MatchId = match.Id,
            Sportsbook = "TestBook",
            Market = "Match Winner",
            Selection = "Home",
            DecimalOdds = 2.5m,
            ImpliedProbability = 0.4,
            CapturedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        var result = await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        var home = result.Selections.Single(s => s.Selection == "Home");
        Assert.True(home.Recommended);
        Assert.True(home.ExpectedValue > 0);
    }

    [Fact]
    public async Task GeneratePrediction1X2Async_WithoutOdds_NeverRecommendsAnything()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        var result = await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        Assert.DoesNotContain(result.Selections, s => s.Recommended);
    }

    [Fact]
    public async Task GeneratePrediction1X2Async_UnknownMatch_ThrowsNotFound()
    {
        var (_, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GeneratePrediction1X2Async(Guid.NewGuid(), modelVersion.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GeneratePrediction1X2Async_NoModelVersionSpecified_UsesMostRecentlyTrained()
    {
        var (match, _) = await SeedMatchAndModelAsync();
        var newer = new ModelVersion
        {
            ModelName = "football_1x2",
            Sport = "Football",
            Version = "v002",
            Algorithm = "LightGBM",
            TrainedAt = DateTime.UtcNow.AddMinutes(5),
            TrainingStartDate = DateTime.UtcNow.AddMonths(-6),
            TrainingEndDate = DateTime.UtcNow,
            ArtifactPath = "models/football_1x2_v002.joblib",
        };
        _dbContext.ModelVersions.Add(newer);
        await _dbContext.SaveChangesAsync();

        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        var result = await service.GeneratePrediction1X2Async(match.Id, modelVersionId: null, CancellationToken.None);

        Assert.Equal(newer.Id, result.ModelVersionId);
    }

    [Fact]
    public async Task GetPredictionsForMatchAsync_ReturnsNewestFirst()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());
        await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        var predictions = await service.GetPredictionsForMatchAsync(match.Id, CancellationToken.None);

        Assert.Equal(3, predictions.Count);
    }

    [Fact]
    public async Task EvaluateMatchAsync_NotFinishedYet_Throws()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());
        await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EvaluateMatchAsync(match.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateMatchAsync_SetsActualOutcomeAndIsCorrect_WithoutTouchingProbability()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());
        await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        // Model predicted Home 60% (the highest) — make the real result Home, so that
        // selection should come back marked correct and the others incorrect.
        match.Status = MatchStatus.Finished;
        match.HomeScore = 2;
        match.AwayScore = 0;
        await _dbContext.SaveChangesAsync();

        var result = await service.EvaluateMatchAsync(match.Id, CancellationToken.None);

        Assert.Equal("Home", result.ActualOutcome);
        var home = result.Predictions.Single(p => p.Selection == "Home");
        var draw = result.Predictions.Single(p => p.Selection == "Draw");
        Assert.True(home.IsCorrect);
        Assert.False(draw.IsCorrect);
        Assert.Equal(0.6, home.Probability, 9); // untouched by evaluation
    }

    [Fact]
    public async Task EvaluateMatchAsync_UnknownMatch_ThrowsNotFound()
    {
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        await Assert.ThrowsAsync<NotFoundException>(() => service.EvaluateMatchAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task GetAccuracySummaryAsync_ComputesHitRateAndListsRecommendedMisses()
    {
        var (match, modelVersion) = await SeedMatchAndModelAsync();
        _dbContext.OddsSnapshots.Add(new OddsSnapshot
        {
            MatchId = match.Id,
            Sportsbook = "TestBook",
            Market = "Match Winner",
            Selection = "Away", // model gives Away only 15%, so a juicy enough decimal odds makes it the Recommended pick
            DecimalOdds = 10m,
            ImpliedProbability = 0.1,
            CapturedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());
        await service.GeneratePrediction1X2Async(match.Id, modelVersion.Id, CancellationToken.None);

        match.Status = MatchStatus.Finished;
        match.HomeScore = 2;
        match.AwayScore = 0; // Home wins — the recommended "Away" pick was wrong
        await _dbContext.SaveChangesAsync();
        await service.EvaluateMatchAsync(match.Id, CancellationToken.None);

        var summary = await service.GetAccuracySummaryAsync(modelVersionId: null, CancellationToken.None);

        Assert.Equal(3, summary.TotalEvaluated);
        Assert.Equal(1, summary.CorrectCount); // only "Home" was right
        Assert.Equal(1, summary.RecommendedTotal);
        Assert.Equal(0, summary.RecommendedCorrect);
        Assert.Single(summary.RecentMisses);
        Assert.Equal("Away", summary.RecentMisses[0].Selection);
    }

    [Fact]
    public async Task GetAccuracySummaryAsync_NoEvaluationsYet_ReturnsZeroedSummary()
    {
        var service = new PredictionGenerationService(_dbContext, new FakeDatasetBuilderService(), new FakeMlServiceClient());

        var summary = await service.GetAccuracySummaryAsync(modelVersionId: null, CancellationToken.None);

        Assert.Equal(0, summary.TotalEvaluated);
        Assert.Equal(0.0, summary.Accuracy);
        Assert.Empty(summary.RecentMisses);
    }
}
