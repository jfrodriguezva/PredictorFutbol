using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;
using SportsPredictor.Infrastructure.Persistence.Configurations;
using SportsPredictor.Infrastructure.Predictions;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.Predictions;

public class ValueBetNotificationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;
    private readonly ValueBetNotificationService _service;

    public ValueBetNotificationServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SportsPredictorDbContext>().UseSqlite(_connection).Options;
        _dbContext = new SportsPredictorDbContext(options);
        _dbContext.Database.EnsureCreated();
        _service = new ValueBetNotificationService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>Seeds a real Match + Prediction, since ValueBetNotification has a real FK to both.</summary>
    private (Guid matchId, Guid predictionId) SeedMatchAndPrediction()
    {
        var competition = new Competition { SportId = SportConfiguration.FootballId, Name = "Test League", CompetitionType = CompetitionType.League };
        var season = new Season { CompetitionId = competition.Id, Name = "2024", StartDate = new DateOnly(2024, 8, 1), EndDate = new DateOnly(2025, 5, 1) };
        var home = new Team { SportId = SportConfiguration.FootballId, Name = "Home FC" };
        var away = new Team { SportId = SportConfiguration.FootballId, Name = "Away FC" };
        var match = new Match
        {
            CompetitionId = competition.Id, SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id,
            MatchDate = DateTime.UtcNow.AddDays(2), Status = MatchStatus.Scheduled,
        };
        var modelVersion = new ModelVersion
        {
            ModelName = "football_1x2", Sport = "Football", Version = $"v{Guid.NewGuid():N}", Algorithm = "XGBoost",
            TrainedAt = DateTime.UtcNow, TrainingStartDate = DateTime.UtcNow.AddMonths(-6), TrainingEndDate = DateTime.UtcNow,
            ArtifactPath = "models/football_1x2_v001.joblib",
        };
        var prediction = new Prediction
        {
            MatchId = match.Id, ModelVersionId = modelVersion.Id, PredictionDate = DateTime.UtcNow,
            Market = "Match Winner", Selection = "Home", Probability = 0.6, Recommended = true,
        };
        _dbContext.AddRange(competition, season, home, away, match, modelVersion, prediction);
        return (match.Id, prediction.Id);
    }

    private ValueBetNotification AddNotification(bool read = false)
    {
        var (matchId, predictionId) = SeedMatchAndPrediction();
        var notification = new ValueBetNotification
        {
            MatchId = matchId,
            PredictionId = predictionId,
            Selection = "Home",
            ExpectedValue = 0.12,
            DetectedAt = DateTime.UtcNow,
            Read = read,
        };
        _dbContext.ValueBetNotifications.Add(notification);
        return notification;
    }

    [Fact]
    public async Task GetAsync_UnreadOnlyFalse_ReturnsEverything()
    {
        AddNotification(read: true);
        AddNotification(read: false);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAsync(unreadOnly: false, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAsync_UnreadOnlyTrue_ExcludesRead()
    {
        AddNotification(read: true);
        var unread = AddNotification(read: false);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAsync(unreadOnly: true, CancellationToken.None);

        var only = Assert.Single(result);
        Assert.Equal(unread.Id, only.Id);
    }

    [Fact]
    public async Task MarkReadAsync_SetsReadTrue()
    {
        var notification = AddNotification(read: false);
        await _dbContext.SaveChangesAsync();

        var result = await _service.MarkReadAsync(notification.Id, CancellationToken.None);

        Assert.True(result.Read);
        Assert.True(_dbContext.ValueBetNotifications.Single().Read);
    }

    [Fact]
    public async Task MarkReadAsync_UnknownId_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkReadAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
