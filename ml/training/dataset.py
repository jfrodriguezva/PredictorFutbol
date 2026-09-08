"""
Loads the CSV produced by the C# Dataset Builder
(GET /api/dataset-builder/{trackedCompetitionId}/export — see docs/football-model.md).

This module never talks to SQLite or API-Football directly: the C# backend is the
only owner of the database (CLAUDE.md section 1 / Phase 8 decision). It only ever
receives a CSV payload handed to it by the backend.
"""

from __future__ import annotations

from io import StringIO

import pandas as pd

FEATURE_COLUMNS = [
    "home_points_before",
    "away_points_before",
    "home_goal_diff_before",
    "away_goal_diff_before",
    "home_rank_before",
    "away_rank_before",
    "home_form_points_last5",
    "away_form_points_last5",
    "home_injuries_count",
    "away_injuries_count",
    "home_lineup_announced",
    "away_lineup_announced",
    "market_implied_home_prob",
    "market_implied_draw_prob",
    "market_implied_away_prob",
    "home_elo_before",
    "away_elo_before",
    "h2h_home_win_rate",
    "home_rest_days",
    "away_rest_days",
    "is_cup_competition",
    "elo_diff",
    "points_diff",
    "rank_diff",
    "goal_diff_diff",
    "form_diff",
    "rest_diff",
    "home_form_at_home_last5",
    "away_form_away_last5",
    "poisson_draw_probability",
]

NON_FEATURE_COLUMNS = ["match_id", "match_date_utc", "home_team", "away_team", "home_score", "away_score", "result"]

# Optional benchmark-only columns (evaluation/benchmarks.py's closing_line_benchmark) —
# not model features (deliberately excluded from FEATURE_COLUMNS) and deliberately NOT
# part of load_dataset's required-columns check below, so CSVs without them (older
# exports, synthetic test data) still load fine — closing_line_benchmark itself
# tolerates their absence by returning None.
CLOSING_LINE_COLUMNS = [
    "closing_market_implied_home_prob",
    "closing_market_implied_draw_prob",
    "closing_market_implied_away_prob",
]

RESULT_CLASSES = ["H", "D", "A"]


def load_dataset(csv_content: str) -> pd.DataFrame:
    """Parses the Dataset Builder's CSV export into a DataFrame, sorted chronologically."""
    df = pd.read_csv(StringIO(csv_content))
    missing = [c for c in NON_FEATURE_COLUMNS + FEATURE_COLUMNS if c not in df.columns]
    if missing:
        raise ValueError(f"Dataset is missing expected columns: {missing}")

    df["match_date_utc"] = pd.to_datetime(df["match_date_utc"], utc=True)
    return df.sort_values("match_date_utc").reset_index(drop=True)


def prepare_features(df: pd.DataFrame) -> tuple[pd.DataFrame, pd.Series]:
    """
    Splits a loaded dataset into (X, y). Missing feature values (a match with no
    standings/odds snapshot yet) are filled with 0 rather than dropped — dropping
    would silently shrink an already-small dataset; the model can learn that
    "0 / unknown" is itself informative for early-season matches.
    """
    x = df[FEATURE_COLUMNS].fillna(0.0)
    y = df["result"]
    return x, y
