"""
Tests for the 1X2 training pipeline. All match data here is synthetically generated
purely to exercise the pipeline's mechanics (chronological split, metric computation,
model selection) — it is never presented as real football data (CLAUDE.md section 37).
"""

from __future__ import annotations

import random
from datetime import datetime, timedelta, timezone
from io import StringIO

import pandas as pd
import pytest

from training import football_1x2
from training.dataset import FEATURE_COLUMNS, NON_FEATURE_COLUMNS, load_dataset, prepare_features


def _synthetic_csv(num_matches: int, seed: int = 42) -> str:
    rng = random.Random(seed)
    base_date = datetime(2024, 8, 1, tzinfo=timezone.utc)
    rows = []
    for i in range(num_matches):
        home_points = rng.randint(0, 60)
        away_points = rng.randint(0, 60)
        # Bias the outcome toward the team with more points, so the synthetic
        # features are at least weakly predictive (a pipeline sanity check, not a
        # claim about real football).
        if home_points > away_points + 10:
            home_score, away_score = rng.choice([(2, 0), (1, 0), (3, 1)])
        elif away_points > home_points + 10:
            home_score, away_score = rng.choice([(0, 2), (0, 1), (1, 3)])
        else:
            home_score, away_score = rng.choice([(1, 1), (0, 0), (2, 2)])

        rows.append(
            {
                "match_id": f"match-{i}",
                "match_date_utc": (base_date + timedelta(days=i)).isoformat(),
                "home_team": "Home FC",
                "away_team": "Away FC",
                "home_score": home_score,
                "away_score": away_score,
                "result": "H" if home_score > away_score else ("A" if away_score > home_score else "D"),
                "home_points_before": home_points,
                "away_points_before": away_points,
                "home_goal_diff_before": rng.randint(-20, 20),
                "away_goal_diff_before": rng.randint(-20, 20),
                "home_rank_before": rng.randint(1, 20),
                "away_rank_before": rng.randint(1, 20),
                "home_form_points_last5": rng.randint(0, 15),
                "away_form_points_last5": rng.randint(0, 15),
                "home_injuries_count": rng.randint(0, 5),
                "away_injuries_count": rng.randint(0, 5),
                "home_lineup_announced": rng.choice([0, 1]),
                "away_lineup_announced": rng.choice([0, 1]),
                "market_implied_home_prob": rng.uniform(0.2, 0.6),
                "market_implied_draw_prob": rng.uniform(0.2, 0.35),
                "market_implied_away_prob": rng.uniform(0.2, 0.6),
                "home_elo_before": rng.uniform(1400, 1600),
                "away_elo_before": rng.uniform(1400, 1600),
                "h2h_home_win_rate": rng.uniform(0.0, 1.0),
                "home_rest_days": rng.uniform(3, 10),
                "away_rest_days": rng.uniform(3, 10),
                "is_cup_competition": rng.choice([0, 1]),
                "elo_diff": rng.uniform(-200, 200),
                "points_diff": home_points - away_points,
                "rank_diff": rng.randint(-19, 19),
                "goal_diff_diff": rng.randint(-40, 40),
                "form_diff": rng.randint(-15, 15),
                "rest_diff": rng.uniform(-7, 7),
                "home_form_at_home_last5": rng.randint(0, 15),
                "away_form_away_last5": rng.randint(0, 15),
                "poisson_draw_probability": rng.uniform(0.15, 0.35),
            }
        )

    df = pd.DataFrame(rows)
    buffer = StringIO()
    df.to_csv(buffer, index=False)
    return buffer.getvalue()


def test_load_dataset_parses_all_expected_columns():
    csv_content = _synthetic_csv(30)

    df = load_dataset(csv_content)

    for column in NON_FEATURE_COLUMNS + FEATURE_COLUMNS:
        assert column in df.columns


def test_load_dataset_missing_column_raises():
    df = pd.DataFrame({"match_id": [1], "result": ["H"]})
    buffer = StringIO()
    df.to_csv(buffer, index=False)

    with pytest.raises(ValueError):
        load_dataset(buffer.getvalue())


def test_load_dataset_sorts_chronologically():
    rows = pd.DataFrame(
        {
            "match_id": ["b", "a"],
            "match_date_utc": ["2024-09-01T00:00:00Z", "2024-08-01T00:00:00Z"],
            "home_team": ["X", "X"],
            "away_team": ["Y", "Y"],
            "home_score": [1, 1],
            "away_score": [0, 0],
            "result": ["H", "H"],
            **{col: [0, 0] for col in FEATURE_COLUMNS},
        }
    )
    buffer = StringIO()
    rows.to_csv(buffer, index=False)

    df = load_dataset(buffer.getvalue())

    assert list(df["match_id"]) == ["a", "b"]


def test_train_and_select_too_few_matches_raises():
    df = load_dataset(_synthetic_csv(5))

    with pytest.raises(ValueError):
        football_1x2.train_and_select(df)


def test_train_and_select_compares_all_candidates_including_ensemble():
    df = load_dataset(_synthetic_csv(60))

    summary = football_1x2.train_and_select(df)

    algorithms = {r.algorithm for r in summary.algorithm_results}
    assert algorithms == {"LogisticRegression", "XGBoost", "LightGBM", "Ensemble"}
    assert summary.selected_algorithm in algorithms


def test_train_and_select_picks_lowest_log_loss_not_highest_accuracy():
    df = load_dataset(_synthetic_csv(60))

    summary = football_1x2.train_and_select(df)

    best_log_loss = min(r.log_loss for r in summary.algorithm_results)
    selected = next(r for r in summary.algorithm_results if r.algorithm == summary.selected_algorithm)
    assert selected.log_loss == best_log_loss


def test_train_and_select_splits_chronologically_not_randomly():
    df = load_dataset(_synthetic_csv(60))
    x, y = prepare_features(df)

    x_train, x_test, _, _ = football_1x2._chronological_split(x, y)

    # Test set must be the most recent matches — the tail of the sorted dataframe.
    assert x_test.index.min() > x_train.index.max()


def test_multiclass_brier_score_is_zero_for_perfect_predictions():
    import numpy as np

    y_true_idx = np.array([0, 1, 2])
    probs = np.array([[1, 0, 0], [0, 1, 0], [0, 0, 1]], dtype=float)

    score = football_1x2.multiclass_brier_score(y_true_idx, probs)

    assert score == pytest.approx(0.0)


def test_save_artifact_and_next_version(tmp_path):
    df = load_dataset(_synthetic_csv(60))
    summary = football_1x2.train_and_select(df)
    models_dir = str(tmp_path)

    version1 = football_1x2.next_version("football_1x2", models_dir)
    assert version1 == "v001"
    path1 = football_1x2.save_artifact(summary, "football_1x2", version1, models_dir)
    assert path1.endswith("football_1x2_v001.joblib")

    version2 = football_1x2.next_version("football_1x2", models_dir)
    assert version2 == "v002"  # never reuses a version once an artifact exists
