using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Application.MlService;
using SportsPredictor.Infrastructure.ModelRegistry;
using SportsPredictor.Infrastructure.Persistence;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.ModelRegistry;

public class ModelTrainingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SportsPredictorDbContext _dbContext;

    public ModelTrainingServiceTests()
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
        public string CsvToReturn { get; set; } = "match_id,result\n1,H";

        public Task<BuildFeaturesResultDto> BuildFeaturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ExportDatasetCsvAsync(Guid trackedCompetitionId, CancellationToken cancellationToken) =>
            Task.FromResult(CsvToReturn);

        public Task<string> ExportGlobalDatasetCsvAsync(CancellationToken cancellationToken) =>
            Task.FromResult(CsvToReturn);

        public Task<IReadOnlyDictionary<string, double>> ComputeFeaturesForPredictionAsync(Guid matchId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMlServiceClient : IMlServiceClient
    {
        private int _callCount;

        public string? ReceivedCsv { get; private set; }

        public Task<TrainFootball1X2ResultDto> TrainFootball1X2Async(string csvContent, string modelName, CancellationToken cancellationToken)
        {
            ReceivedCsv = csvContent;
            _callCount++;
            var version = $"v{_callCount:000}";
            var results = new List<TrainingAlgorithmResultDto>
            {
                new("LogisticRegression", 0.95, 0.20, 0.50),
                new("XGBoost", 0.80, 0.15, 0.55),
            };
            return Task.FromResult(new TrainFootball1X2ResultDto(
                results, "XGBoost", version, $"models/football_1x2_{version}.joblib", 100,
                new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                DateTime.UtcNow));
        }

        public Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(string csvContent, int windows, CancellationToken cancellationToken)
        {
            ReceivedCsv = csvContent;
            return Task.FromResult(new EvaluateFootball1X2ResultDto(
                new CalibrationReportDto(0.05, new List<CalibrationBinDto>()),
                new BacktestSummaryDto(new List<BacktestWindowDto>(), 0, 0, 0, 0, 0, 0, 0),
                new List<BenchmarkResultDto> { new("always_favorite", 1.0, 0.3, 0.4) },
                100));
        }

        public string? ReceivedHomeTeam { get; private set; }
        public string? ReceivedAwayTeam { get; private set; }

        public Task<PredictGoalsResultDto> PredictGoalsAsync(string csvContent, string homeTeamName, string awayTeamName, double? dixonColesRho, CancellationToken cancellationToken)
        {
            ReceivedCsv = csvContent;
            ReceivedHomeTeam = homeTeamName;
            ReceivedAwayTeam = awayTeamName;
            return Task.FromResult(new PredictGoalsResultDto(1.8, 1.1, 0.55, 0.45, 0.5, 0.5, 2, 1, 0.12));
        }

        public Task<PredictMatch1X2ResultDto> PredictMatch1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AnalyzeFootball1X2ResultDto> AnalyzeFootball1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, AnalyzeFixtureContextDto fixture, AnalyzeOddsDto? odds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task TrainFootball1X2Async_RegistersModelVersionWithSelectedAlgorithmMetrics()
    {
        var datasetBuilder = new FakeDatasetBuilderService();
        var mlClient = new FakeMlServiceClient();
        var service = new ModelTrainingService(datasetBuilder, mlClient, _dbContext);

        var dto = await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("XGBoost", dto.Algorithm);
        Assert.Equal(0.80, dto.LogLoss);
        Assert.Equal(0.15, dto.BrierScore);
        Assert.Equal("football_1x2", dto.ModelName);
        Assert.Equal("Football", dto.Sport);
        Assert.False(dto.Active);
        Assert.Single(_dbContext.ModelVersions);
    }

    [Fact]
    public async Task TrainFootball1X2Async_CreatesTrainingRunLinkedToModelVersion()
    {
        var service = new ModelTrainingService(new FakeDatasetBuilderService(), new FakeMlServiceClient(), _dbContext);

        var dto = await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);

        var run = _dbContext.TrainingRuns.Single();
        Assert.Equal(dto.Id, run.ModelVersionId);
        Assert.Equal(100, run.DatasetSize);
        Assert.Contains("XGBoost", run.MetricsJson);
    }

    [Fact]
    public async Task TrainFootball1X2Async_PassesExportedCsvToMlService()
    {
        var datasetBuilder = new FakeDatasetBuilderService { CsvToReturn = "match_id,result\n42,D" };
        var mlClient = new FakeMlServiceClient();
        var service = new ModelTrainingService(datasetBuilder, mlClient, _dbContext);

        await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("match_id,result\n42,D", mlClient.ReceivedCsv);
    }

    [Fact]
    public async Task TrainFootball1X2Async_CalledTwice_CreatesTwoSeparateModelVersions()
    {
        var service = new ModelTrainingService(new FakeDatasetBuilderService(), new FakeMlServiceClient(), _dbContext);

        var first = await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);
        var second = await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, _dbContext.ModelVersions.Count());
    }

    [Fact]
    public async Task GetModelVersionsAsync_ReturnsNewestFirst()
    {
        var service = new ModelTrainingService(new FakeDatasetBuilderService(), new FakeMlServiceClient(), _dbContext);
        await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);
        await Task.Delay(10);
        var second = await service.TrainFootball1X2Async(Guid.NewGuid(), CancellationToken.None);

        var versions = await service.GetModelVersionsAsync(CancellationToken.None);

        Assert.Equal(second.Id, versions.First().Id);
    }

    [Fact]
    public async Task EvaluateFootball1X2Async_PassesExportedCsvAndDoesNotPersistAnything()
    {
        var datasetBuilder = new FakeDatasetBuilderService { CsvToReturn = "match_id,result\n1,H" };
        var mlClient = new FakeMlServiceClient();
        var service = new ModelTrainingService(datasetBuilder, mlClient, _dbContext);

        var result = await service.EvaluateFootball1X2Async(Guid.NewGuid(), windows: 5, CancellationToken.None);

        Assert.Equal("match_id,result\n1,H", mlClient.ReceivedCsv);
        Assert.Equal(100, result.DatasetSize);
        Assert.Empty(_dbContext.ModelVersions);
        Assert.Empty(_dbContext.TrainingRuns);
    }
}
