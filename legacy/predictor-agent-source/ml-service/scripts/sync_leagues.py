"""Sincroniza ligas hacia la tabla `leagues`.

Uso (desde apps/ml-service, con el venv activo):
    python -m scripts.sync_leagues --league 262 --season 2025
    python -m scripts.sync_leagues --league 262 --league 39 --season 2025
    python -m scripts.sync_leagues --country Mexico --season 2025
"""

from __future__ import annotations

import argparse

import app  # noqa: F401
from app.db.models import League
from app.db.session import get_session
from app.ingestion.api_football import ApiFootballClient


def upsert_league(session, api_football_id: int, name: str, country: str, season: int) -> League:
    league = (
        session.query(League)
        .filter_by(api_football_id=api_football_id, season=season)
        .one_or_none()
    )
    if league is None:
        league = League(api_football_id=api_football_id, name=name, country=country, season=season)
        session.add(league)
    else:
        league.name = name
        league.country = country
    return league


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--league", type=int, action="append", help="ID de liga en API-Football (repetible)")
    parser.add_argument("--country", type=str, help="Sincroniza todas las ligas de tipo 'League' de este país")
    parser.add_argument("--season", type=int, required=True)
    args = parser.parse_args()

    if not args.league and not args.country:
        parser.error("Especifica --league (uno o más) o --country")

    session = get_session()
    synced = 0
    with ApiFootballClient() as client:
        entries: list[dict] = []
        if args.country:
            for item in client.get_leagues(country=args.country, season=args.season):
                if item["league"]["type"] == "League":
                    entries.append(item)
        for league_id in args.league or []:
            entries.extend(client.get_leagues(league_id=league_id, season=args.season))

        for item in entries:
            upsert_league(
                session,
                api_football_id=item["league"]["id"],
                name=item["league"]["name"],
                country=item["country"]["name"],
                season=args.season,
            )
            synced += 1

    session.commit()
    print(f"Sincronizadas {synced} ligas (temporada {args.season})")


if __name__ == "__main__":
    main()
