import logging

from fastapi import FastAPI, HTTPException

from app.config import settings
from app.schemas import (
    AlgorithmResultResponse,
    AnalyzeFootball1X2Request,
    AnalyzeFootball1X2Response,
    BacktestSummaryResponse,
    BacktestWindowResponse,
    BenchmarkResultResponse,
    CalibrationBinResponse,
    CalibrationReportResponse,
    EvaluateFootball1X2Request,
    EvaluateFootball1X2Response,
    HealthResponse,
    PredictGoalsRequest,
    PredictGoalsResponse,
    PredictMatch1X2Request,
    PredictMatch1X2Response,
    ShapFeatureImpactResponse,
    StakeRecommendationResponse,
    TrainFootball1X2Request,
    TrainFootball1X2Response,
)
from backtesting.walk_forward import walk_forward_backtest
from decision.staking import recommend_stakes
from evaluation import benchmarks as benchmarks_module
from evaluation.calibration import compute_calibration_report
from features.shap_explain import top_shap_features
from narrative.generate import generate_narrative
from training import football_1x2, goals_poisson
from training.dataset import load_dataset, prepare_features

logging.basicConfig(
    level=logging.INFO,
    format='{"timestamp":"%(asctime)s","level":"%(levelname)s","logger":"%(name)s","message":"%(message)s"}',
)
logger = logging.getLogger("sportspredictor.ml")

app = FastAPI(
    title=settings.service_name,
    version=settings.service_version,
)


@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    logger.info("Health check requested")
    return HealthResponse(
        status="Healthy",
        service=settings.service_name,
        version=settings.service_version,
    )


@app.post("/train/football-1x2", response_model=TrainFootball1X2Response)
def train_football_1x2(request: TrainFootball1X2Request) -> TrainFootball1X2Response:
    """
    Trains and compares Logistic Regression / XGBoost / LightGBM on the Home/Draw/Away
    target (CLAUDE.md section 20), selects the best by Log Loss, and persists the
    artifact under ml/models/. Returns metadata only — the caller (the C# backend) is
    the one that registers ModelVersion/TrainingRun in SQLite (this service never
    writes to the database itself).
    """
    try:
        df = load_dataset(request.csv_content)
        summary = football_1x2.train_and_select(df)
    except ValueError as exc:
        logger.warning("Training request rejected: %s", exc)
        raise HTTPException(status_code=422, detail=str(exc)) from exc

    version = football_1x2.next_version(request.model_name, "models")
    artifact_path = football_1x2.save_artifact(summary, request.model_name, version, "models")

    logger.info(
        "Trained %s for %s: selected=%s dataset_size=%d",
        request.model_name, request.sport, summary.selected_algorithm, summary.dataset_size,
    )

    return TrainFootball1X2Response(
        algorithm_results=[
            AlgorithmResultResponse(
                algorithm=r.algorithm, log_loss=r.log_loss, brier_score=r.brier_score, accuracy=r.accuracy
            )
            for r in summary.algorithm_results
        ],
        selected_algorithm=summary.selected_algorithm,
        version=version,
        artifact_path=artifact_path,
        dataset_size=summary.dataset_size,
        training_start_date=summary.training_start_date,
        training_end_date=summary.training_end_date,
    )


@app.post("/evaluate/football-1x2", response_model=EvaluateFootball1X2Response)
def evaluate_football_1x2(request: EvaluateFootball1X2Request) -> EvaluateFootball1X2Response:
    """
    Phase 10 (CLAUDE.md sections 26-27): calibration of the selected model, a
    walk-forward backtest (never a random split), and comparison against the
    benchmarks that are currently achievable (always-favorite, bookmaker-implied
    probabilities). Elo and true closing-line benchmarks are deliberately not
    implemented yet — see docs/football-model.md. This endpoint persists nothing;
    it only reports numbers for a human to judge the model by.
    """
    try:
        df = load_dataset(request.csv_content)
        summary = football_1x2.train_and_select(df)

        x, y = prepare_features(df)
        x_train, x_test, _, y_test = football_1x2._chronological_split(x, y)
        y_test_idx = y_test.map(football_1x2.LABEL_TO_INDEX).to_numpy()
        x_test_scaled = summary.scaler.transform(x_test)
        probabilities = summary.model.predict_proba(x_test_scaled)
        calibration = compute_calibration_report(y_test_idx, probabilities)

        backtest = walk_forward_backtest(df, n_windows=request.n_windows)
        benchmark_results = [
            benchmarks_module.always_favorite_benchmark(df),
            benchmarks_module.bookmaker_implied_benchmark(df),
        ]
    except ValueError as exc:
        logger.warning("Evaluation request rejected: %s", exc)
        raise HTTPException(status_code=422, detail=str(exc)) from exc

    return EvaluateFootball1X2Response(
        calibration=CalibrationReportResponse(
            expected_calibration_error=calibration.expected_calibration_error,
            bins=[
                CalibrationBinResponse(
                    bin_lower=b.bin_lower, bin_upper=b.bin_upper,
                    predicted_mean=b.predicted_mean, actual_frequency=b.actual_frequency, count=b.count,
                )
                for b in calibration.bins
            ],
        ),
        backtest=BacktestSummaryResponse(
            windows=[
                BacktestWindowResponse(
                    window_index=w.window_index, train_size=w.train_size, test_size=w.test_size,
                    accuracy=w.accuracy, precision_macro=w.precision_macro, recall_macro=w.recall_macro,
                    f1_macro=w.f1_macro, log_loss=w.log_loss, brier_score=w.brier_score,
                    calibration_error=w.calibration_error, total_bets=w.total_bets,
                    win_rate=w.win_rate, roi=w.roi, average_odds=w.average_odds,
                )
                for w in backtest.windows
            ],
            total_bets=backtest.total_bets,
            overall_roi=backtest.overall_roi,
            overall_win_rate=backtest.overall_win_rate,
            max_drawdown=backtest.max_drawdown,
            average_log_loss=backtest.average_log_loss,
            average_brier_score=backtest.average_brier_score,
            average_calibration_error=backtest.average_calibration_error,
        ),
        benchmarks=[
            BenchmarkResultResponse(name=b.name, log_loss=b.log_loss, brier_score=b.brier_score, accuracy=b.accuracy)
            for b in benchmark_results
        ],
        dataset_size=len(df),
    )


@app.post("/predict/football-1x2", response_model=PredictMatch1X2Response)
def predict_match_1x2(request: PredictMatch1X2Request) -> PredictMatch1X2Response:
    """
    Real, single-match prediction: loads a previously trained+saved artifact
    (from /train/football-1x2) and scores one match's current feature values.
    This is what actually answers "what does the model think will happen in
    this specific upcoming match" — /train only compares algorithms.
    """
    try:
        artifact = football_1x2.load_artifact(request.artifact_path)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=f"Model artifact not found: {request.artifact_path}") from exc

    probabilities = football_1x2.predict_single(artifact, request.features)
    return PredictMatch1X2Response(home=probabilities["H"], draw=probabilities["D"], away=probabilities["A"])


@app.post("/predict/goals", response_model=PredictGoalsResponse)
def predict_goals(request: PredictGoalsRequest) -> PredictGoalsResponse:
    """
    Expected Goals (CLAUDE.md section 21) — independent from the 1X2 classifier.
    Fits a Poisson goal model (optionally with the Dixon-Coles low-score adjustment)
    from the given dataset and predicts Home/Away Expected Goals, Over/Under 2.5,
    BTTS, and the most likely scoreline for the requested fixture.
    """
    try:
        df = load_dataset(request.csv_content)
        model = goals_poisson.fit_poisson_model(df)
        home_xg, away_xg = goals_poisson.predict_expected_goals(model, request.home_team, request.away_team)
        summary = goals_poisson.derive_markets(home_xg, away_xg, rho=request.dixon_coles_rho)
    except ValueError as exc:
        logger.warning("Goals prediction request rejected: %s", exc)
        raise HTTPException(status_code=422, detail=str(exc)) from exc

    return PredictGoalsResponse(
        home_expected_goals=summary.home_expected_goals,
        away_expected_goals=summary.away_expected_goals,
        over_2_5=summary.over_2_5,
        under_2_5=summary.under_2_5,
        btts_yes=summary.btts_yes,
        btts_no=summary.btts_no,
        most_likely_home_goals=summary.most_likely_score[0],
        most_likely_away_goals=summary.most_likely_score[1],
        most_likely_score_probability=summary.most_likely_score_probability,
    )


@app.post("/analyze/football-1x2", response_model=AnalyzeFootball1X2Response)
def analyze_football_1x2(request: AnalyzeFootball1X2Request) -> AnalyzeFootball1X2Response:
    """
    Full "expert analyst" explanation layered on top of the raw H/D/A prediction:
    SHAP feature attribution (features/shap_explain.py), Kelly-Criterion stake sizing
    when market odds are supplied (decision/staking.py), and a Claude-generated
    narrative that falls back to a template without ANTHROPIC_API_KEY
    (narrative/generate.py). Like /predict/football-1x2, this is entirely stateless —
    it never touches SQLite or API-FOOTBALL; all match/odds context comes from the
    caller (the C# backend), which is the only one that persists the result.
    """
    try:
        artifact = football_1x2.load_artifact(request.artifact_path)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=f"Model artifact not found: {request.artifact_path}") from exc

    probabilities = football_1x2.predict_single(artifact, request.features)
    probs_named = {"home": probabilities["H"], "draw": probabilities["D"], "away": probabilities["A"]}
    predicted_label = max(probabilities, key=probabilities.get)
    predicted_index = list(probabilities).index(predicted_label)

    shap_top_features = top_shap_features(artifact, request.features, predicted_index)

    odds_dict = None
    stakes = None
    if request.odds is not None:
        odds_dict = {"home": request.odds.home, "draw": request.odds.draw, "away": request.odds.away}
        stakes = recommend_stakes(probs_named, odds_dict)

    narrative_text = generate_narrative(
        request.fixture.model_dump(), {"probabilities": probs_named}, shap_top_features, odds_dict, stakes
    )

    return AnalyzeFootball1X2Response(
        home=probs_named["home"],
        draw=probs_named["draw"],
        away=probs_named["away"],
        shap_top_features=[ShapFeatureImpactResponse(**f) for f in shap_top_features],
        stakes={k: StakeRecommendationResponse(**v) for k, v in stakes.items()} if stakes else None,
        narrative=narrative_text,
    )
