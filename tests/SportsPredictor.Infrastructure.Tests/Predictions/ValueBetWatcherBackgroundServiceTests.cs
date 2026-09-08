using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Predictions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using SportsPredictor.Infrastructure.Predictions;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Predictions;

public class ValueBetWatcherBackgroundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public ValueBetWatcherBackgroundServiceTests()
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

    /// <summary>Selection to report — mirrors the real PredictionGenerationService's
    /// invariant that every returned PredictionDto corresponds to an already-persisted
    /// Prediction row (ValueBetNotification.PredictionId has a real FK to it).</summary>
    private sealed record FakeSelection(string Selection, double Probability, double? ExpectedValue, string? ValueCategory, bool Recommended);

    private sealed class FakePredictionGenerationService : IPredictionGenerationService
    {
        private readonly SportsPredictorDbContext _dbContext;

        public FakePredictionGenerationService(SportsPredictorDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private Guid? _modelVersionId;

        public int CallCount { get; private set; }
        public Func<Guid, List<FakeSelection>> SelectionsFactory { get; set; } = _ => [];

        public async Task<GeneratePredictionResultDto> GeneratePrediction1X2Async(Guid matchId, Guid? modelVersionId, CancellationToken cancellationToken)
        {
            CallCount++;
            var predictionDate = DateTime.UtcNow;
            var dtos = new List<PredictionDto>();

            if (_modelVersionId is null)
            {
                var version = new ModelVersion
                {
                    ModelName = "football_1x2", Sport = "Football", Version = $"v{Guid.NewGuid():N}", Algorithm = "XGBoost",
                    TrainedAt = DateTime.UtcNow, TrainingStartDate = DateTime.UtcNow.AddMonths(-6), TrainingEndDate = DateTime.UtcNow,
                    ArtifactPath = "models/football_1x2_test.joblib",
                };
                _dbContext.ModelVersions.Add(version);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _modelVersionId = version.Id;
            }

            foreach (var s in SelectionsFactory(matchId))
            {
                var entity = new Prediction
                {
                    MatchId = matchId,
                    ModelVersionId = _modelVersionId.Value,
                    PredictionDate = predictionDate,
                    Market = "Match Winner",
                    Selection = s.Selection,
                    Probability = s.Probability,
                    ExpectedValue = s.ExpectedValue,
                    Recommended = s.Recommended,
                };
                _dbContext.Predictions.Add(entity);
                dtos.Add(new PredictionDto(
                    entity.Id, matchId, entity.ModelVersionId, predictionDate, "Match Winner",
                    s.Selection, s.Probability, s.ExpectedValue, s.ValueCategory, s.Recommended, null, null));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return new GeneratePredictionResultDto(matchId, Guid.NewGuid(), predictionDate, dtos);
        }

        public Task<IReadOnlyList<PredictionDto>> GetPredictionsForMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PredictionDto>> GetRecommendedPredictionsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<EvaluateMatchResultDto> EvaluateMatchAsync(Guid matchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AccuracySummaryDto> GetAccuracySummaryAsync(Guid? modelVersionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private IServiceScopeFactory BuildScopeFactory(IPredictionGenerationService predictionService)
    {
        var services = new ServiceCollection();
        // Same open in-memory connection as _dbContext, so writes/seeds made through
        // either DbContext instance are visible to the other (SQLite :memory: data
        // lives on the connection, not on any single DbContext instance).
        services.AddDbContext<SportsPredictorDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton(predictionService);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static ValueBetWatcherBackgroundService BuildWatcher(IServiceScopeFactory scopeFactory) =>
        new(scopeFactory, NullLogger<ValueBetWatcherBackgroundService>.Instance);

    private async Task<(Competition competition, Season season, Team home, Team away, TrackedCompetition tracked)> SeedTrackedCompetitionAsync(bool enabled = true)
    {
        var competition = new Competition { SportId = SportConfiguration.FootballId, Name = "Test League", CompetitionType = CompetitionType.League };
        var season = new Season { CompetitionId = competition.Id, Name = "2024", StartDate = new DateOnly(2024, 8, 1), EndDate = new DateOnly(2025, 5, 1) };
        var home = new Team { SportId = SportConfiguration.FootballId, Name = "Home FC" };
        var away = new Team { SportId = SportConfiguration.FootballId, Name = "Away FC" };
        var tracked = new TrackedCompetition { CompetitionId = competition.Id, ExternalLeagueId = 39, Season = 2024, Enabled = enabled };
        _dbContext.AddRange(competition, season, home, away, tracked);
        await _dbContext.SaveChangesAsync();
        return (competition, season, home, away, tracked);
    }

    private Match AddScheduledMatch(Competition competition, Season season, Team home, Team away, DateTime matchDate)
    {
        var match = new Match
        {
            CompetitionId = competition.Id,
            SeasonId = season.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            MatchDate = matchDate,
            Status = MatchStatus.Scheduled,
        };
        _dbContext.Matches.Add(match);
        return match;
    }

    private ModelVersion AddModelVersion()
    {
        var version = new ModelVersion
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
        _dbContext.ModelVersions.Add(version);
        return version;
    }

    [Fact]
    public async Task RunOnceAsync_ValueSelection_CreatesNotification()
    {
        var (competition, season, home, away, _) = await SeedTrackedCompetitionAsync();
        var match = AddScheduledMatch(competition, season, home, away, DateTime.UtcNow.AddDays(2));
        await _dbContext.SaveChangesAsync();

        var fakePredictionService = new FakePredictionGenerationService(_dbContext)
        {
            SelectionsFactory = _ =>
            [
                new FakeSelection("Home", 0.6, 0.15, "Value", true),
                new FakeSelection("Draw", 0.25, -0.1, "Neutral", false),
            ],
        };

        var watcher = BuildWatcher(BuildScopeFactory(fakePredictionService));
        await watcher.RunOnceAsync(CancellationToken.None);

        var notification = _dbContext.ValueBetNotifications.Single();
        Assert.Equal(match.Id, notification.MatchId);
        Assert.Equal("Home", notification.Selection);
        Assert.False(notification.Read);
    }

    [Fact]
    public async Task RunOnceAsync_OnlyNeutralSelections_NoNotificationCreated()
    {
        var (competition, season, home, away, _) = await SeedTrackedCompetitionAsync();
        AddScheduledMatch(competition, season, home, away, DateTime.UtcNow.AddDays(2));
        await _dbContext.SaveChangesAsync();

        var fakePredictionService = new FakePredictionGenerationService(_dbContext)
        {
            SelectionsFactory = _ => [new FakeSelection("Home", 0.4, 0.0, "Neutral", false)],
        };

        var watcher = BuildWatcher(BuildScopeFactory(fakePredictionService));
        await watcher.RunOnceAsync(CancellationToken.None);

        Assert.Empty(_dbContext.ValueBetNotifications);
    }

    [Fact]
    public async Task RunOnceAsync_MatchAlreadyHasPrediction_NeverCallsGenerate()
    {
        var (competition, season, home, away, _) = await SeedTrackedCompetitionAsync();
        var match = AddScheduledMatch(competition, season, home, away, DateTime.UtcNow.AddDays(2));
        var modelVersion = AddModelVersion();
        _dbContext.Predictions.Add(new Prediction
        {
            MatchId = match.Id, ModelVersionId = modelVersion.Id, PredictionDate = DateTime.UtcNow,
            Market = "Match Winner", Selection = "Home", Probability = 0.5, Recommended = false,
        });
        await _dbContext.SaveChangesAsync();

        var fakePredictionService = new FakePredictionGenerationService(_dbContext);
        var watcher = BuildWatcher(BuildScopeFactory(fakePredictionService));
        await watcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, fakePredictionService.CallCount);
    }

    [Fact]
    public async Task RunOnceAsync_DisabledTrackedCompetition_NeverCallsGenerate()
    {
        var (competition, season, home, away, _) = await SeedTrackedCompetitionAsync(enabled: false);
        AddScheduledMatch(competition, season, home, away, DateTime.UtcNow.AddDays(2));
        await _dbContext.SaveChangesAsync();

        var fakePredictionService = new FakePredictionGenerationService(_dbContext);
        var watcher = BuildWatcher(BuildScopeFactory(fakePredictionService));
        await watcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, fakePredictionService.CallCount);
    }

    [Fact]
    public async Task RunOnceAsync_MatchBeyondLookaheadWindow_NeverCallsGenerate()
    {
        var (competition, season, home, away, _) = await SeedTrackedCompetitionAsync();
        AddScheduledMatch(competition, season, home, away, DateTime.UtcNow.AddDays(30));
        await _dbContext.SaveChangesAsync();

        var fakePredictionService = new FakePredictionGenerationService(_dbContext);
        var watcher = BuildWatcher(BuildScopeFactory(fakePredictionService));
        await watcher.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, fakePredictionService.CallCount);
    }
}
