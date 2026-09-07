from datetime import datetime, timezone

from pydantic import BaseModel, Field


class HealthResponse(BaseModel):
    status: str
    checked_at_utc: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    service: str
    version: str


class TrainFootball1X2Request(BaseModel):
    """csv_content is exactly what GET /api/dataset-builder/{id}/export returns — this
    service never reads SQLite itself (see docs/football-model.md)."""

    csv_content: str
    model_name: str = "football_1x2"
    sport: str = "Football"


class AlgorithmResultResponse(BaseModel):
    algorithm: str
    log_loss: float
    brier_score: float
    accuracy: float


class TrainFootball1X2Response(BaseModel):
    algorithm_results: list[AlgorithmResultResponse]
    selected_algorithm: str
    version: str
    artifact_path: str
    dataset_size: int
    training_start_date: datetime
    training_end_date: datetime
    trained_at_utc: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))


class EvaluateFootball1X2Request(BaseModel):
    """Same CSV shape as TrainFootball1X2Request — this endpoint never persists
    anything, it only reports how a model/benchmarks would have performed."""

    csv_content: str
    n_windows: int = 5


class CalibrationBinResponse(BaseModel):
    bin_lower: float
    bin_upper: float
    predicted_mean: float
    actual_frequency: float
    count: int


class CalibrationReportResponse(BaseModel):
    expected_calibration_error: float
    bins: list[CalibrationBinResponse]


class BenchmarkResultResponse(BaseModel):
    name: str
    log_loss: float
    brier_score: float
    accuracy: float


class BacktestWindowResponse(BaseModel):
    window_index: int
    train_size: int
    test_size: int
    accuracy: float
    precision_macro: float
    recall_macro: float
    f1_macro: float
    log_loss: float
    brier_score: float
    calibration_error: float
    total_bets: int
    win_rate: float
    roi: float
    average_odds: float


class BacktestSummaryResponse(BaseModel):
    windows: list[BacktestWindowResponse]
    total_bets: int
    overall_roi: float
    overall_win_rate: float
    max_drawdown: float
    average_log_loss: float
    average_brier_score: float
    average_calibration_error: float


class EvaluateFootball1X2Response(BaseModel):
    calibration: CalibrationReportResponse
    backtest: BacktestSummaryResponse
    benchmarks: list[BenchmarkResultResponse]
    dataset_size: int
    evaluated_at_utc: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))


class PredictGoalsRequest(BaseModel):
    """Same CSV shape as the other endpoints (needs home_team/away_team/home_score/away_score)."""

    csv_content: str
    home_team: str
    away_team: str
    dixon_coles_rho: float | None = None


class PredictMatch1X2Request(BaseModel):
    """Predicts one specific, real match using an already-trained artifact
    (see TrainFootball1X2Response.artifact_path). No CSV needed here."""

    artifact_path: str
    features: dict[str, float] = Field(default_factory=dict)


class PredictMatch1X2Response(BaseModel):
    home: float
    draw: float
    away: float


class PredictGoalsResponse(BaseModel):
    home_expected_goals: float
    away_expected_goals: float
    over_2_5: float
    under_2_5: float
    btts_yes: float
    btts_no: float
    most_likely_home_goals: int
    most_likely_away_goals: int
    most_likely_score_probability: float
