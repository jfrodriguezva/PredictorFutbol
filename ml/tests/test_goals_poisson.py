"""
Tests for the Expected Goals (Poisson/Dixon-Coles) model. All match data is
synthetically generated purely to exercise the pipeline's mechanics — never
presented as real football data (CLAUDE.md section 37).
"""

from __future__ import annotations

import random
from datetime import datetime, timedelta, timezone
from io import StringIO

import numpy as np
import pandas as pd
import pytest

from training import goals_poisson

TEAMS = ["Strong FC", "Weak FC", "Mid FC", "Solid FC"]
TEAM_STRENGTH = {"Strong FC": 2.2, "Mid FC": 1.4, "Solid FC": 1.2, "Weak FC": 0.6}


def _synthetic_goals_df(num_matches: int, seed: int = 7) -> pd.DataFrame:
    rng = random.Random(seed)
    base_date = datetime(2024, 8, 1, tzinfo=timezone.utc)
    rows = []
    for i in range(num_matches):
        home, away = rng.sample(TEAMS, 2)
        home_lambda = TEAM_STRENGTH[home] * 1.2  # home advantage
        away_lambda = TEAM_STRENGTH[away] * 0.85  # facing the home team's defense, roughly
        home_score = rng.choices(range(6), weights=[np.exp(-home_lambda) * home_lambda**k for k in range(6)])[0]
        away_score = rng.choices(range(6), weights=[np.exp(-away_lambda) * away_lambda**k for k in range(6)])[0]
        rows.append({
            "match_id": f"m{i}",
            "match_date_utc": (base_date + timedelta(days=i)).isoformat(),
            "home_team": home,
            "away_team": away,
            "home_score": home_score,
            "away_score": away_score,
            "result": "H" if home_score > away_score else ("A" if away_score > home_score else "D"),
        })
    return pd.DataFrame(rows)


def test_fit_poisson_model_too_few_matches_raises():
    df = _synthetic_goals_df(5)

    with pytest.raises(ValueError):
        goals_poisson.fit_poisson_model(df)


def test_fit_poisson_model_ranks_stronger_team_higher():
    df = _synthetic_goals_df(200)

    model = goals_poisson.fit_poisson_model(df)
    strong_xg, weak_xg = goals_poisson.predict_expected_goals(model, "Strong FC", "Weak FC")

    assert strong_xg > weak_xg


def test_predict_expected_goals_unknown_team_raises():
    df = _synthetic_goals_df(200)
    model = goals_poisson.fit_poisson_model(df)

    with pytest.raises(ValueError):
        goals_poisson.predict_expected_goals(model, "Strong FC", "Unknown FC")


def test_score_probability_matrix_sums_to_one():
    matrix = goals_poisson.score_probability_matrix(home_xg=1.5, away_xg=1.1, max_goals=10)

    assert matrix.sum() == pytest.approx(1.0, abs=1e-6)


def test_score_probability_matrix_with_dixon_coles_still_sums_to_one():
    matrix = goals_poisson.score_probability_matrix(home_xg=1.5, away_xg=1.1, max_goals=10, rho=-0.1)

    assert matrix.sum() == pytest.approx(1.0, abs=1e-6)


def test_derive_markets_probabilities_are_consistent():
    summary = goals_poisson.derive_markets(home_xg=1.8, away_xg=1.2)

    assert summary.over_2_5 == pytest.approx(1.0 - summary.under_2_5, abs=1e-9)
    assert summary.btts_yes == pytest.approx(1.0 - summary.btts_no, abs=1e-9)
    assert 0.0 <= summary.most_likely_score_probability <= 1.0
    assert summary.home_expected_goals == 1.8
    assert summary.away_expected_goals == 1.2


def test_derive_markets_higher_expected_goals_increase_over_2_5():
    low_scoring = goals_poisson.derive_markets(home_xg=0.5, away_xg=0.4)
    high_scoring = goals_poisson.derive_markets(home_xg=2.5, away_xg=2.0)

    assert high_scoring.over_2_5 > low_scoring.over_2_5
