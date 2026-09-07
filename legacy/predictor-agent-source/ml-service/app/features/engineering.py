from __future__ import annotations

import datetime as dt
from collections import defaultdict, deque
from typing import Any

from app.db.models import Fixture
from app.features.elo import DEFAULT_RATING, update_elo

FORM_WINDOW = 5
H2H_WINDOW = 5

_OUTCOME_FOR_HOME = {"H": "W", "D": "D", "A": "L"}
_OUTCOME_FOR_AWAY = {"H": "L", "D": "D", "A": "W"}
_POINTS_FOR_HOME = {"H": 3, "D": 1, "A": 0}
_POINTS_FOR_AWAY = {"H": 0, "D": 1, "A": 3}


class FeatureBuilder:
    """Calcula features de cada fixture usando solo información previa a su kickoff.

    Debe alimentarse con fixtures en orden cronológico ascendente: por cada uno,
    primero se leen las features (estado acumulado hasta ANTES de este partido)
    y luego se llama a `update` para incorporar su resultado al estado, de forma
    que el siguiente fixture ya lo vea reflejado. Esto evita fuga temporal.
    """

    def __init__(self) -> None:
        self.elo: dict[int, float] = defaultdict(lambda: DEFAULT_RATING)
        self.recent_matches: dict[int, deque] = defaultdict(lambda: deque(maxlen=FORM_WINDOW))
        self.last_match_date: dict[int, dt.datetime] = {}
        self.h2h: dict[tuple[int, int], deque] = defaultdict(lambda: deque(maxlen=H2H_WINDOW))
        self.table: dict[tuple[int, int, int], dict[str, int]] = defaultdict(
            lambda: {"points": 0, "played": 0}
        )

    @staticmethod
    def _h2h_key(team_a: int, team_b: int) -> tuple[int, int]:
        return tuple(sorted((team_a, team_b)))

    def features_for(self, fixture: Fixture) -> dict[str, Any]:
        home_id, away_id = fixture.home_team_id, fixture.away_team_id

        home_form = list(self.recent_matches[home_id])
        away_form = list(self.recent_matches[away_id])

        def form_points(form: list[dict]) -> int:
            return sum({"W": 3, "D": 1, "L": 0}[m["outcome"]] for m in form)

        def avg_stat(form: list[dict], key: str) -> float | None:
            return sum(m[key] for m in form) / len(form) if form else None

        h2h_matches = list(self.h2h[self._h2h_key(home_id, away_id)])
        h2h_home_wins = sum(1 for m in h2h_matches if m["winner_team_id"] == home_id)
        h2h_away_wins = sum(1 for m in h2h_matches if m["winner_team_id"] == away_id)
        h2h_draws = sum(1 for m in h2h_matches if m["winner_team_id"] is None)

        rest_home = (
            (fixture.kickoff_at - self.last_match_date[home_id]).days
            if home_id in self.last_match_date
            else None
        )
        rest_away = (
            (fixture.kickoff_at - self.last_match_date[away_id]).days
            if away_id in self.last_match_date
            else None
        )

        table_home = self.table[(fixture.league_id, fixture.season, home_id)]
        table_away = self.table[(fixture.league_id, fixture.season, away_id)]
        ppg_home = table_home["points"] / table_home["played"] if table_home["played"] else None
        ppg_away = table_away["points"] / table_away["played"] if table_away["played"] else None

        return {
            "fixture_id": fixture.id,
            "kickoff_at": fixture.kickoff_at,
            "league_id": fixture.league_id,
            "season": fixture.season,
            "home_team_id": home_id,
            "away_team_id": away_id,
            "elo_home": self.elo[home_id],
            "elo_away": self.elo[away_id],
            "elo_diff": self.elo[home_id] - self.elo[away_id],
            "form_pts_home": form_points(home_form),
            "form_pts_away": form_points(away_form),
            "form_matches_home": len(home_form),
            "form_matches_away": len(away_form),
            "avg_goals_scored_home": avg_stat(home_form, "goals_for"),
            "avg_goals_conceded_home": avg_stat(home_form, "goals_against"),
            "avg_goals_scored_away": avg_stat(away_form, "goals_for"),
            "avg_goals_conceded_away": avg_stat(away_form, "goals_against"),
            "h2h_home_wins": h2h_home_wins,
            "h2h_away_wins": h2h_away_wins,
            "h2h_draws": h2h_draws,
            "h2h_matches": len(h2h_matches),
            "rest_days_home": rest_home,
            "rest_days_away": rest_away,
            "ppg_home": ppg_home,
            "ppg_away": ppg_away,
            "games_played_home": table_home["played"],
            "games_played_away": table_away["played"],
            "result_1x2": fixture.result_1x2,
        }

    def update(self, fixture: Fixture) -> None:
        result = fixture.result_1x2
        if result is None:
            return

        home_id, away_id = fixture.home_team_id, fixture.away_team_id

        new_home_elo, new_away_elo = update_elo(self.elo[home_id], self.elo[away_id], result)
        self.elo[home_id] = new_home_elo
        self.elo[away_id] = new_away_elo

        self.recent_matches[home_id].append(
            {
                "outcome": _OUTCOME_FOR_HOME[result],
                "goals_for": fixture.home_goals,
                "goals_against": fixture.away_goals,
            }
        )
        self.recent_matches[away_id].append(
            {
                "outcome": _OUTCOME_FOR_AWAY[result],
                "goals_for": fixture.away_goals,
                "goals_against": fixture.home_goals,
            }
        )

        winner_team_id = home_id if result == "H" else (away_id if result == "A" else None)
        self.h2h[self._h2h_key(home_id, away_id)].append({"winner_team_id": winner_team_id})

        self.last_match_date[home_id] = fixture.kickoff_at
        self.last_match_date[away_id] = fixture.kickoff_at

        table_home = self.table[(fixture.league_id, fixture.season, home_id)]
        table_away = self.table[(fixture.league_id, fixture.season, away_id)]
        table_home["points"] += _POINTS_FOR_HOME[result]
        table_home["played"] += 1
        table_away["points"] += _POINTS_FOR_AWAY[result]
        table_away["played"] += 1
