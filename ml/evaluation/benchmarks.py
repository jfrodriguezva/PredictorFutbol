"""
Benchmarks (CLAUDE.md section 27): a model is only worth using if it beats these.

Implemented here (achievable with current data):
  1. Always pick the favorite (highest market-implied probability).
  2. The bookmaker's own implied probabilities, used directly as "the model".

NOT implemented yet, deliberately (see docs/football-model.md):
  - Elo: no Elo rating feature/tracker exists yet (CLAUDE.md section 22 feature,
    not built — would need its own historical computation across all matches).
  - True opening-vs-closing line comparison: the Dataset Builder's
    market_implied_*_prob features are an average across bookmakers/snapshots, not
    specifically the closing line. Distinguishing "closing" needs a dedicated feature.
  - API-Football's own predictions: only meaningful once ApiFootballPredictionSnapshot
    rows exist for the matches in a given dataset; wire this in when there is real
    ApiFootballPredictionSnapshot data to join against.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from sklearn.metrics import log_loss

from training.dataset import RESULT_CLASSES
from training.football_1x2 import multiclass_brier_score

MARKET_PROB_COLUMNS = ["market_implied_home_prob", "market_implied_draw_prob", "market_implied_away_prob"]
LABEL_TO_INDEX = {label: i for i, label in enumerate(RESULT_CLASSES)}


@dataclass
class BenchmarkResult:
    name: str
    log_loss: float
    brier_score: float
    accuracy: float


def _market_probabilities(df: pd.DataFrame) -> np.ndarray:
    """Normalizes the raw (vig-inflated) market-implied probabilities into a proper
    distribution per match, purely so log loss/Brier can be computed — this is NOT
    the same as Phase 12's principled vig removal for Expected Value math."""
    raw = df[MARKET_PROB_COLUMNS].fillna(1.0 / 3.0).to_numpy()
    raw = np.clip(raw, 1e-6, None)
    return raw / raw.sum(axis=1, keepdims=True)


def _score(name: str, y_true_idx: np.ndarray, probabilities: np.ndarray) -> BenchmarkResult:
    predicted_idx = np.argmax(probabilities, axis=1)
    return BenchmarkResult(
        name=name,
        log_loss=float(log_loss(y_true_idx, probabilities, labels=[0, 1, 2])),
        brier_score=multiclass_brier_score(y_true_idx, probabilities),
        accuracy=float(np.mean(predicted_idx == y_true_idx)),
    )


def always_favorite_benchmark(df: pd.DataFrame) -> BenchmarkResult:
    probabilities = _market_probabilities(df)
    y_true_idx = df["result"].map(LABEL_TO_INDEX).to_numpy()
    # "Always the favorite" still needs a probability vector to score log loss/Brier
    # fairly — it commits fully (prob=1) to whichever side the market favors.
    favorite_idx = np.argmax(probabilities, axis=1)
    committed = np.zeros_like(probabilities)
    committed[np.arange(len(favorite_idx)), favorite_idx] = 1.0
    return _score("always_favorite", y_true_idx, committed)


def bookmaker_implied_benchmark(df: pd.DataFrame) -> BenchmarkResult:
    probabilities = _market_probabilities(df)
    y_true_idx = df["result"].map(LABEL_TO_INDEX).to_numpy()
    return _score("bookmaker_implied", y_true_idx, probabilities)
