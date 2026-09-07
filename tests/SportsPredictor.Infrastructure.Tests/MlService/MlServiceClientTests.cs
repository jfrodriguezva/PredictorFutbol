using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Infrastructure.MlService;
using SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;
using Xunit;

namespace SportsPredictor.Infrastructure.Tests.MlService;

public class MlServiceClientTests
{
    private const string SuccessJson = """
        {
          "algorithm_results": [
            { "algorithm": "LogisticRegression", "log_loss": 0.95, "brier_score": 0.20, "accuracy": 0.50 },
            { "algorithm": "XGBoost", "log_loss": 0.80, "brier_score": 0.15, "accuracy": 0.55 },
            { "algorithm": "LightGBM", "log_loss": 0.85, "brier_score": 0.17, "accuracy": 0.53 }
          ],
          "selected_algorithm": "XGBoost",
          "version": "v001",
          "artifact_path": "models/football_1x2_v001.joblib",
          "dataset_size": 100,
          "training_start_date": "2024-08-01T00:00:00Z",
          "training_end_date": "2025-05-01T00:00:00Z",
          "trained_at_utc": "2026-01-01T00:00:00Z"
        }
        """;

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static MlServiceClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8001") };
        return new MlServiceClient(httpClient, NullLogger<MlServiceClient>.Instance);
    }

    [Fact]
    public async Task TrainFootball1X2Async_ParsesResponseIntoDto()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(SuccessJson));
        var client = CreateClient(handler);

        var result = await client.TrainFootball1X2Async("csv,data", "football_1x2", CancellationToken.None);

        Assert.Equal(3, result.AlgorithmResults.Count);
        Assert.Equal("XGBoost", result.SelectedAlgorithm);
        Assert.Equal("v001", result.Version);
        Assert.Equal(100, result.DatasetSize);
    }

    [Fact]
    public async Task TrainFootball1X2Async_SendsCsvContentAndModelNameInBody()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(SuccessJson));
        var client = CreateClient(handler);

        await client.TrainFootball1X2Async("match_id,result\\n1,H", "football_1x2", CancellationToken.None);

        var sentBody = await handler.Requests.Single().Content!.ReadAsStringAsync();
        Assert.Contains("football_1x2", sentBody);
        Assert.Contains("match_id", sentBody);
    }

    [Fact]
    public async Task TrainFootball1X2Async_ServerError_ThrowsMlServiceException()
    {
        var handler = FakeHttpMessageHandler.Sequence(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"detail\":\"Need at least 20 matches\"}", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<MlServiceException>(() => client.TrainFootball1X2Async("csv", "football_1x2", CancellationToken.None));
    }

    private const string EvaluateSuccessJson = """
        {
          "calibration": { "expected_calibration_error": 0.08, "bins": [
            { "bin_lower": 0.4, "bin_upper": 0.5, "predicted_mean": 0.45, "actual_frequency": 0.42, "count": 20 }
          ] },
          "backtest": {
            "windows": [
              { "window_index": 0, "train_size": 50, "test_size": 10, "accuracy": 0.5, "precision_macro": 0.4,
                "recall_macro": 0.4, "f1_macro": 0.4, "log_loss": 0.9, "brier_score": 0.2, "calibration_error": 0.05,
                "total_bets": 8, "win_rate": 0.5, "roi": 0.1, "average_odds": 2.1 }
            ],
            "total_bets": 8, "overall_roi": 0.1, "overall_win_rate": 0.5, "max_drawdown": 1.2,
            "average_log_loss": 0.9, "average_brier_score": 0.2, "average_calibration_error": 0.05
          },
          "benchmarks": [
            { "name": "always_favorite", "log_loss": 1.0, "brier_score": 0.3, "accuracy": 0.4 },
            { "name": "bookmaker_implied", "log_loss": 0.95, "brier_score": 0.25, "accuracy": 0.45 }
          ],
          "dataset_size": 150
        }
        """;

    [Fact]
    public async Task EvaluateFootball1X2Async_ParsesResponseIntoDto()
    {
        var handler = FakeHttpMessageHandler.Sequence(JsonResponse(EvaluateSuccessJson));
        var client = CreateClient(handler);

        var result = await client.EvaluateFootball1X2Async("csv,data", 5, CancellationToken.None);

        Assert.Equal(0.08, result.Calibration.ExpectedCalibrationError);
        Assert.Single(result.Backtest.Windows);
        Assert.Equal(2, result.Benchmarks.Count);
        Assert.Equal(150, result.DatasetSize);
    }

    [Fact]
    public async Task EvaluateFootball1X2Async_ServerError_ThrowsMlServiceException()
    {
        var handler = FakeHttpMessageHandler.Sequence(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent("{\"detail\":\"Need more matches\"}", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<MlServiceException>(() => client.EvaluateFootball1X2Async("csv", 5, CancellationToken.None));
    }
}
