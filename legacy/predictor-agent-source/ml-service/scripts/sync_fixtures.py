"""Sincroniza fixtures de una liga+temporada hacia la base de datos.

Requiere que la liga ya exista en la tabla `leagues` (correr sync_leagues.py antes).

Uso (desde apps/ml-service, con el venv activo):
    python -m scripts.sync_fixtures --league 262 --season 2025
"""

from __future__ import annotations

import argparse
import datetime as dt

import app  # noqa: F401
from app.db.models import Fixture, League, Team
from app.db.session import get_session
from app.ingestion.api_football import ApiFootballClient


def upsert_team(session, api_team: dict) -> Team:
    team = session.query(Team).filter_by(api_football_id=api_team["id"]).one_or_none()
    if team is None:
        team = Team(api_football_id=api_team["id"], name=api_team["name"], logo_url=api_team.get("logo"))
        session.add(team)
        session.flush()
    return team


def result_1x2(home_goals: int | None, away_goals: int | None) -> str | None:
    if home_goals is None or away_goals is None:
        return None
    if home_goals > away_goals:
        return "H"
    if home_goals < away_goals:
        return "A"
    return "D"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--league", type=int, required=True)
    parser.add_argument("--season", type=int, required=True)
    args = parser.parse_args()

    session = get_session()
    league = (
        session.query(League)
        .filter_by(api_football_id=args.league, season=args.season)
        .one_or_none()
    )
    if league is None:
        raise SystemExit(
            f"Liga {args.league} temporada {args.season} no existe en DB. "
            f"Corre antes: python -m scripts.sync_leagues --league {args.league} --season {args.season}"
        )

    with ApiFootballClient() as client:
        fixtures = client.get_fixtures(args.league, args.season)

    count = 0
    for fx in fixtures:
        home_team = upsert_team(session, fx["teams"]["home"])
        away_team = upsert_team(session, fx["teams"]["away"])
        goals = fx["goals"]
        kickoff = dt.datetime.fromisoformat(fx["fixture"]["date"]).replace(tzinfo=None)

        fixture = (
            session.query(Fixture)
            .filter_by(api_football_id=fx["fixture"]["id"])
            .one_or_none()
        )
        if fixture is None:
            fixture = Fixture(api_football_id=fx["fixture"]["id"], league_id=league.id, season=args.season)
            session.add(fixture)

        fixture.home_team_id = home_team.id
        fixture.away_team_id = away_team.id
        fixture.kickoff_at = kickoff
        fixture.status = fx["fixture"]["status"]["short"]
        fixture.home_goals = goals["home"]
        fixture.away_goals = goals["away"]
        fixture.result_1x2 = result_1x2(goals["home"], goals["away"])
        fixture.venue = (fx["fixture"].get("venue") or {}).get("name")
        fixture.referee = fx["fixture"].get("referee")
        count += 1

    session.commit()
    print(f"Sincronizados {count} fixtures para liga {args.league} temporada {args.season}")


if __name__ == "__main__":
    main()
