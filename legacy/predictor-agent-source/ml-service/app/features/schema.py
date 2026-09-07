"""Contrato de features compartido entre entrenamiento (scripts/train_model.py)
e inferencia (app/inference.py) — debe mantenerse en un solo lugar para que
ambos coincidan exactamente."""

LABEL_MAP = {"A": 0, "D": 1, "H": 2}
LABEL_NAMES = ["away", "draw", "home"]

FEATURE_COLUMNS = [
    "league_id",
    "elo_home",
    "elo_away",
    "elo_diff",
    "form_pts_home",
    "form_pts_away",
    "form_matches_home",
    "form_matches_away",
    "avg_goals_scored_home",
    "avg_goals_conceded_home",
    "avg_goals_scored_away",
    "avg_goals_conceded_away",
    "h2h_home_wins",
    "h2h_away_wins",
    "h2h_draws",
    "h2h_matches",
    "rest_days_home",
    "rest_days_away",
    "ppg_home",
    "ppg_away",
    "games_played_home",
    "games_played_away",
]
CATEGORICAL = ["league_id"]
