using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.MlService;
using SportsPredictor.Infrastructure.MlService.Models;

namespace SportsPredictor.Infrastructure.MlService;

/// <summary>
/// Thin HTTP wrapper over the Python ML service's /train endpoint. Sends the CSV the
/// Dataset Builder produced and gets back training metadata only — this client never
/// causes anything to be written to SQLite itself (see ModelTrainingService for that).
/// </summary>
public sealed class MlServiceClient : IMlServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MlServiceClient> _logger;

    public MlServiceClient(HttpClient httpClient, ILogger<MlServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TrainFootball1X2ResultDto> TrainFootball1X2Async(string csvContent, string modelName, CancellationToken cancellationToken)
    {
        var requestBody = new TrainRequestBody(csvContent, modelName);

        using var response = await _httpClient.PostAsJsonAsync("train/football-1x2", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ML service training request failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML service training request failed with status {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<TrainFootball1X2ResponseModel>(cancellationToken)
            ?? throw new MlServiceException("ML service returned an empty training response.");

        return new TrainFootball1X2ResultDto(
            AlgorithmResults: result.AlgorithmResults
                .Select(r => new TrainingAlgorithmResultDto(r.Algorithm, r.LogLoss, r.BrierScore, r.Accuracy))
                .ToList(),
            SelectedAlgorithm: result.SelectedAlgorithm,
            Version: result.Version,
            ArtifactPath: result.ArtifactPath,
            DatasetSize: result.DatasetSize,
            TrainingStartDate: result.TrainingStartDate,
            TrainingEndDate: result.TrainingEndDate,
            TrainedAtUtc: result.TrainedAtUtc);
    }

    public async Task<EvaluateFootball1X2ResultDto> EvaluateFootball1X2Async(string csvContent, int windows, CancellationToken cancellationToken)
    {
        var requestBody = new EvaluateRequestBody(csvContent, windows);

        using var response = await _httpClient.PostAsJsonAsync("evaluate/football-1x2", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ML service evaluation request failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML service evaluation request failed with status {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<EvaluateFootball1X2ResponseModel>(cancellationToken)
            ?? throw new MlServiceException("ML service returned an empty evaluation response.");

        return new EvaluateFootball1X2ResultDto(
            Calibration: new CalibrationReportDto(
                result.Calibration.ExpectedCalibrationError,
                result.Calibration.Bins.Select(b => new CalibrationBinDto(b.BinLower, b.BinUpper, b.PredictedMean, b.ActualFrequency, b.Count)).ToList()),
            CalibrationAfterIsotonic: result.CalibrationAfterIsotonic is { } calibrated
                ? new CalibrationReportDto(
                    calibrated.ExpectedCalibrationError,
                    calibrated.Bins.Select(b => new CalibrationBinDto(b.BinLower, b.BinUpper, b.PredictedMean, b.ActualFrequency, b.Count)).ToList())
                : null,
            Backtest: new BacktestSummaryDto(
                result.Backtest.Windows.Select(w => new BacktestWindowDto(
                    w.WindowIndex, w.TrainSize, w.TestSize, w.Accuracy, w.PrecisionMacro, w.RecallMacro,
                    w.F1Macro, w.LogLoss, w.BrierScore, w.CalibrationError, w.TotalBets, w.WinRate, w.Roi, w.AverageOdds)).ToList(),
                result.Backtest.TotalBets, result.Backtest.OverallRoi, result.Backtest.OverallWinRate,
                result.Backtest.MaxDrawdown, result.Backtest.AverageLogLoss, result.Backtest.AverageBrierScore,
                result.Backtest.AverageCalibrationError),
            Benchmarks: result.Benchmarks.Select(b => new BenchmarkResultDto(b.Name, b.LogLoss, b.BrierScore, b.Accuracy)).ToList(),
            DatasetSize: result.DatasetSize);
    }

    public async Task<PredictGoalsResultDto> PredictGoalsAsync(string csvContent, string homeTeamName, string awayTeamName, double? dixonColesRho, CancellationToken cancellationToken)
    {
        var requestBody = new PredictGoalsRequestBody(csvContent, homeTeamName, awayTeamName, dixonColesRho);

        using var response = await _httpClient.PostAsJsonAsync("predict/goals", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ML service goals prediction request failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML service goals prediction request failed with status {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<PredictGoalsResponseModel>(cancellationToken)
            ?? throw new MlServiceException("ML service returned an empty goals prediction response.");

        return new PredictGoalsResultDto(
            result.HomeExpectedGoals, result.AwayExpectedGoals, result.Over25, result.Under25,
            result.BttsYes, result.BttsNo, result.MostLikelyHomeGoals, result.MostLikelyAwayGoals,
            result.MostLikelyScoreProbability);
    }

    public async Task<PredictMatch1X2ResultDto> PredictMatch1X2Async(string artifactPath, IReadOnlyDictionary<string, double> features, CancellationToken cancellationToken)
    {
        var requestBody = new PredictMatchRequestBody(artifactPath, features);

        using var response = await _httpClient.PostAsJsonAsync("predict/football-1x2", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ML service match prediction request failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML service match prediction request failed with status {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<PredictMatch1X2ResponseModel>(cancellationToken)
            ?? throw new MlServiceException("ML service returned an empty match prediction response.");

        return new PredictMatch1X2ResultDto(result.Home, result.Draw, result.Away);
    }

    public async Task<AnalyzeFootball1X2ResultDto> AnalyzeFootball1X2Async(
        string artifactPath,
        IReadOnlyDictionary<string, double> features,
        AnalyzeFixtureContextDto fixture,
        AnalyzeOddsDto? odds,
        CancellationToken cancellationToken)
    {
        var requestBody = new AnalyzeRequestBody(
            artifactPath,
            features,
            new AnalyzeFixtureBody(fixture.HomeTeam, fixture.AwayTeam, fixture.LeagueName, fixture.Country, fixture.Season, fixture.Status),
            odds is not null ? new AnalyzeOddsBody(odds.Home, odds.Draw, odds.Away) : null);

        using var response = await _httpClient.PostAsJsonAsync("analyze/football-1x2", requestBody, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("ML service analysis request failed with status {StatusCode}: {Body}", (int)response.StatusCode, body);
            throw new MlServiceException($"ML service analysis request failed with status {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<AnalyzeFootball1X2ResponseModel>(cancellationToken)
            ?? throw new MlServiceException("ML service returned an empty analysis response.");

        return new AnalyzeFootball1X2ResultDto(
            result.Home,
            result.Draw,
            result.Away,
            result.ShapTopFeatures.Select(f => new ShapFeatureImpactDto(f.Feature, f.Impact)).ToList(),
            result.Stakes?.ToDictionary(
                kv => kv.Key,
                kv => new StakeRecommendationDto(
                    kv.Value.Label, kv.Value.DecimalOdds, kv.Value.ImpliedProbability, kv.Value.ModelProbability,
                    kv.Value.Edge, kv.Value.IsValueBet, kv.Value.KellyFractionFull, kv.Value.SuggestedStakePctBankroll)),
            result.Narrative);
    }

    private sealed record AnalyzeFixtureBody(
        [property: JsonPropertyName("home_team")] string HomeTeam,
        [property: JsonPropertyName("away_team")] string AwayTeam,
        [property: JsonPropertyName("league_name")] string LeagueName,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("season")] string Season,
        [property: JsonPropertyName("status")] string Status);

    private sealed record AnalyzeOddsBody(
        [property: JsonPropertyName("home")] double Home,
        [property: JsonPropertyName("draw")] double Draw,
        [property: JsonPropertyName("away")] double Away);

    private sealed record AnalyzeRequestBody(
        [property: JsonPropertyName("artifact_path")] string ArtifactPath,
        [property: JsonPropertyName("features")] IReadOnlyDictionary<string, double> Features,
        [property: JsonPropertyName("fixture")] AnalyzeFixtureBody Fixture,
        [property: JsonPropertyName("odds")] AnalyzeOddsBody? Odds);

    private sealed record PredictMatchRequestBody(
        [property: JsonPropertyName("artifact_path")] string ArtifactPath,
        [property: JsonPropertyName("features")] IReadOnlyDictionary<string, double> Features);

    private sealed record TrainRequestBody(
        [property: JsonPropertyName("csv_content")] string CsvContent,
        [property: JsonPropertyName("model_name")] string ModelName);

    private sealed record EvaluateRequestBody(
        [property: JsonPropertyName("csv_content")] string CsvContent,
        [property: JsonPropertyName("n_windows")] int NWindows);

    private sealed record PredictGoalsRequestBody(
        [property: JsonPropertyName("csv_content")] string CsvContent,
        [property: JsonPropertyName("home_team")] string HomeTeam,
        [property: JsonPropertyName("away_team")] string AwayTeam,
        [property: JsonPropertyName("dixon_coles_rho")] double? DixonColesRho);
}
