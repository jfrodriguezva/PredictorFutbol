"""
Walk-forward backtesting (CLAUDE.md section 26): rolling windows, never a random
split — each window trains on everything strictly before it and evaluates on the
next chronological slice, simulating how the model would actually have been used.

Betting-style metrics (ROI/Yield/WinRate/MaxDrawdown/TotalBets/AverageOdds) use
*approximate* decimal odds derived as 1 / market_implied_probability, because the
Dataset Builder's export only carries the averaged implied probability, not the raw
per-bookmaker decimal odds (see docs/football-model.md). This is a documented
approximation, not real bookmaker prices.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score, log_loss, precision_score, recall_score
from sklearn.preprocessing import StandardScaler

from evaluation.calibration import compute_calibration_report
from training.dataset import RESULT_CLASSES, prepare_features
from training.football_1x2 import multiclass_brier_score

LABEL_TO_INDEX = {label: i for i, label in enumerate(RESULT_CLASSES)}
MARKET_PROB_COLUMNS = {
    "H": "market_implied_home_prob",
    "D": "market_implied_draw_prob",
    "A": "market_implied_away_prob",
}
MIN_WINDOW_MATCHES = 10


@dataclass
class WindowResult:
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


@dataclass
class BacktestSummary:
    windows: list[WindowResult] = field(default_factory=list)
    total_bets: int = 0
    overall_roi: float = 0.0
    overall_win_rate: float = 0.0
    max_drawdown: float = 0.0
    average_log_loss: float = 0.0
    average_brier_score: float = 0.0
    average_calibration_error: float = 0.0


def _approx_decimal_odds(df: pd.DataFrame, predicted_labels: np.ndarray) -> np.ndarray:
    odds = np.full(len(df), np.nan)
    for label, column in MARKET_PROB_COLUMNS.items():
        mask = predicted_labels == label
        if not mask.any():
            continue
        probs = df.loc[mask, column].to_numpy()
        with np.errstate(divide="ignore"):
            odds[mask] = np.where(probs > 0, 1.0 / probs, np.nan)
    return odds


def _evaluate_window(train_df: pd.DataFrame, test_df: pd.DataFrame, window_index: int) -> WindowResult | None:
    x_train, y_train = prepare_features(train_df)
    x_test, y_test = prepare_features(test_df)
    y_train_idx = y_train.map(LABEL_TO_INDEX).to_numpy()
    y_test_idx = y_test.map(LABEL_TO_INDEX).to_numpy()

    if len(set(y_train_idx)) < 2:
        # Can't fit a classifier that never sees at least two outcome classes.
        return None

    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(x_train)
    x_test_scaled = scaler.transform(x_test)

    model = LogisticRegression(max_iter=1000)
    model.fit(x_train_scaled, y_train_idx)
    probabilities = model.predict_proba(x_test_scaled)
    predicted_idx = np.argmax(probabilities, axis=1)
    predicted_labels = np.array(RESULT_CLASSES)[predicted_idx]

    calibration = compute_calibration_report(y_test_idx, probabilities)

    odds = _approx_decimal_odds(test_df, predicted_labels)
    won = predicted_idx == y_test_idx
    valid_bet = ~np.isnan(odds)
    profits = np.where(won & valid_bet, odds - 1, np.where(valid_bet, -1.0, 0.0))
    total_bets = int(valid_bet.sum())

    return WindowResult(
        window_index=window_index,
        train_size=len(train_df),
        test_size=len(test_df),
        accuracy=float(np.mean(predicted_idx == y_test_idx)),
        precision_macro=float(precision_score(y_test_idx, predicted_idx, average="macro", zero_division=0)),
        recall_macro=float(recall_score(y_test_idx, predicted_idx, average="macro", zero_division=0)),
        f1_macro=float(f1_score(y_test_idx, predicted_idx, average="macro", zero_division=0)),
        log_loss=float(log_loss(y_test_idx, probabilities, labels=[0, 1, 2])),
        brier_score=multiclass_brier_score(y_test_idx, probabilities),
        calibration_error=calibration.expected_calibration_error,
        total_bets=total_bets,
        win_rate=float(won[valid_bet].mean()) if total_bets > 0 else 0.0,
        roi=float(profits[valid_bet].sum() / total_bets) if total_bets > 0 else 0.0,
        average_odds=float(odds[valid_bet].mean()) if total_bets > 0 else 0.0,
    ), profits[valid_bet]


def walk_forward_backtest(df: pd.DataFrame, n_windows: int = 5) -> BacktestSummary:
    if len(df) < MIN_WINDOW_MATCHES * (n_windows + 1):
        raise ValueError(
            f"Need at least {MIN_WINDOW_MATCHES * (n_windows + 1)} finished matches for "
            f"{n_windows} walk-forward windows; got {len(df)}."
        )

    window_size = len(df) // (n_windows + 1)
    windows: list[WindowResult] = []
    all_profits: list[np.ndarray] = []

    for i in range(n_windows):
        train_end = window_size * (i + 1)
        test_end = window_size * (i + 2)
        train_df = df.iloc[:train_end]
        test_df = df.iloc[train_end:test_end]
        if len(test_df) == 0:
            continue

        evaluated = _evaluate_window(train_df, test_df, i)
        if evaluated is None:
            continue
        result, profits = evaluated
        windows.append(result)
        all_profits.append(profits)

    if not windows:
        raise ValueError("No walk-forward window produced a usable train/test split.")

    combined_profits = np.concatenate(all_profits) if all_profits else np.array([])
    cumulative = np.cumsum(combined_profits) if len(combined_profits) > 0 else np.array([0.0])
    running_max = np.maximum.accumulate(cumulative) if len(cumulative) > 0 else np.array([0.0])
    max_drawdown = float(np.max(running_max - cumulative)) if len(cumulative) > 0 else 0.0

    total_bets = sum(w.total_bets for w in windows)
    overall_roi = float(combined_profits.sum() / total_bets) if total_bets > 0 else 0.0
    overall_win_rate = float(np.mean([w.win_rate for w in windows if w.total_bets > 0])) if total_bets > 0 else 0.0

    return BacktestSummary(
        windows=windows,
        total_bets=total_bets,
        overall_roi=overall_roi,
        overall_win_rate=overall_win_rate,
        max_drawdown=max_drawdown,
        average_log_loss=float(np.mean([w.log_loss for w in windows])),
        average_brier_score=float(np.mean([w.brier_score for w in windows])),
        average_calibration_error=float(np.mean([w.calibration_error for w in windows])),
    )
