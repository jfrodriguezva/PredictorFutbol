"""
Expected Goals model (CLAUDE.md section 21): a Poisson goal model independent from the
1X2 classifier (training/football_1x2.py) — separate module, separate concerns.

Approach (standard Dixon-Coles-style Poisson regression):
  log(home_goals_expected) = attack[home_team] + defense[away_team] + home_advantage
  log(away_goals_expected) = attack[away_team] + defense[home_team]

Fitted via statsmodels' Poisson GLM over a "doubled" dataset (one row per team per
match: its own goals scored, against its opponent, with a home/away flag).

Dixon-Coles' low-score correlation adjustment (tau) is implemented for scorelines
0-0, 1-0, 0-1, 1-1 — the correction the original paper found necessary because plain
independent Poisson underestimates how often low-scoring matches actually occur.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

import numpy as np
import pandas as pd
import statsmodels.api as sm
import statsmodels.formula.api as smf

MAX_GOALS = 10
MIN_MATCHES_REQUIRED = 20


@dataclass
class PoissonGoalModel:
    result: object  # fitted statsmodels GLM results
    known_teams: set[str]


def _build_team_goal_dataset(df: pd.DataFrame) -> pd.DataFrame:
    home_rows = pd.DataFrame({
        "team": df["home_team"],
        "opponent": df["away_team"],
        "goals": df["home_score"],
        "is_home": 1,
    })
    away_rows = pd.DataFrame({
        "team": df["away_team"],
        "opponent": df["home_team"],
        "goals": df["away_score"],
        "is_home": 0,
    })
    return pd.concat([home_rows, away_rows], ignore_index=True)


def fit_poisson_model(df: pd.DataFrame) -> PoissonGoalModel:
    if len(df) < MIN_MATCHES_REQUIRED:
        raise ValueError(f"Need at least {MIN_MATCHES_REQUIRED} finished matches to fit a goal model; got {len(df)}.")

    goal_df = _build_team_goal_dataset(df)
    known_teams = set(goal_df["team"]) | set(goal_df["opponent"])

    model = smf.glm(
        formula="goals ~ is_home + C(team) + C(opponent)",
        data=goal_df,
        family=sm.families.Poisson(),
    ).fit()

    return PoissonGoalModel(result=model, known_teams=known_teams)


def predict_expected_goals(model: PoissonGoalModel, home_team: str, away_team: str) -> tuple[float, float]:
    for team in (home_team, away_team):
        if team not in model.known_teams:
            raise ValueError(f"Team '{team}' was not seen during training; cannot predict expected goals for it.")

    home_row = pd.DataFrame({"team": [home_team], "opponent": [away_team], "is_home": [1]})
    away_row = pd.DataFrame({"team": [away_team], "opponent": [home_team], "is_home": [0]})

    home_xg = float(model.result.predict(home_row).iloc[0])
    away_xg = float(model.result.predict(away_row).iloc[0])
    return home_xg, away_xg


def _dixon_coles_tau(home_goals: int, away_goals: int, home_xg: float, away_xg: float, rho: float) -> float:
    """The classic Dixon-Coles low-score correlation adjustment (Dixon & Coles, 1997)."""
    if home_goals == 0 and away_goals == 0:
        return 1 - (home_xg * away_xg * rho)
    if home_goals == 0 and away_goals == 1:
        return 1 + (home_xg * rho)
    if home_goals == 1 and away_goals == 0:
        return 1 + (away_xg * rho)
    if home_goals == 1 and away_goals == 1:
        return 1 - rho
    return 1.0


def score_probability_matrix(home_xg: float, away_xg: float, max_goals: int = MAX_GOALS, rho: float | None = None) -> np.ndarray:
    """
    Returns a (max_goals+1) x (max_goals+1) matrix where cell [i, j] is
    P(home scores i, away scores j), independent Poisson unless rho is given
    (Dixon-Coles low-score adjustment).
    """
    home_probs = np.array([np.exp(-home_xg) * home_xg**i / math.factorial(i) for i in range(max_goals + 1)])
    away_probs = np.array([np.exp(-away_xg) * away_xg**j / math.factorial(j) for j in range(max_goals + 1)])
    matrix = np.outer(home_probs, away_probs)

    if rho is not None:
        for i in range(min(2, max_goals + 1)):
            for j in range(min(2, max_goals + 1)):
                matrix[i, j] *= _dixon_coles_tau(i, j, home_xg, away_xg, rho)
        matrix /= matrix.sum()  # renormalize after the low-score adjustment

    return matrix


@dataclass
class GoalsMarketSummary:
    home_expected_goals: float
    away_expected_goals: float
    over_2_5: float
    under_2_5: float
    btts_yes: float
    btts_no: float
    most_likely_score: tuple[int, int]
    most_likely_score_probability: float


def derive_markets(home_xg: float, away_xg: float, max_goals: int = MAX_GOALS, rho: float | None = None) -> GoalsMarketSummary:
    matrix = score_probability_matrix(home_xg, away_xg, max_goals, rho)

    total_goals = np.add.outer(np.arange(max_goals + 1), np.arange(max_goals + 1))
    over_2_5 = float(matrix[total_goals > 2].sum())

    both_scored = np.outer(np.arange(max_goals + 1) > 0, np.arange(max_goals + 1) > 0)
    btts_yes = float(matrix[both_scored].sum())

    best_index = np.unravel_index(np.argmax(matrix), matrix.shape)

    return GoalsMarketSummary(
        home_expected_goals=home_xg,
        away_expected_goals=away_xg,
        over_2_5=over_2_5,
        under_2_5=1.0 - over_2_5,
        btts_yes=btts_yes,
        btts_no=1.0 - btts_yes,
        most_likely_score=(int(best_index[0]), int(best_index[1])),
        most_likely_score_probability=float(matrix[best_index]),
    )
