from __future__ import annotations

import datetime as dt

from fastapi import APIRouter, HTTPException, Query
from sqlalchemy import or_
from sqlalchemy.orm import aliased

from app.db.models import Fixture, League, Team
from app.db.session import get_session

router = APIRouter()


@router.get("/leagues")
def list_leagues() -> list[dict]:
    session = get_session()
    leagues = (
        session.query(League)
        .order_by(League.country, League.name, League.season.desc())
        .all()
    )
    return [
        {
            "id": lg.id,
            "api_football_id": lg.api_football_id,
            "name": lg.name,
            "country": lg.country,
            "season": lg.season,
        }
        for lg in leagues
    ]


def _serialize_fixture_row(fx: Fixture, home: Team, away: Team, lg: League) -> dict:
    return {
        "fixture_id": fx.id,
        "league": {"id": lg.id, "name": lg.name, "country": lg.country, "season": lg.season},
        "home_team": {"id": home.id, "name": home.name, "logo_url": home.logo_url},
        "away_team": {"id": away.id, "name": away.name, "logo_url": away.logo_url},
        "kickoff_at": fx.kickoff_at.isoformat(),
        "status": fx.status,
        "home_goals": fx.home_goals,
        "away_goals": fx.away_goals,
    }


@router.get("/fixtures")
def search_fixtures(
    league_id: int | None = None,
    team: str | None = None,
    date_from: str | None = None,
    date_to: str | None = None,
    status: str | None = None,
    limit: int = Query(50, le=200),
) -> list[dict]:
    session = get_session()
    home = aliased(Team)
    away = aliased(Team)

    query = (
        session.query(Fixture, home, away, League)
        .join(home, Fixture.home_team_id == home.id)
        .join(away, Fixture.away_team_id == away.id)
        .join(League, Fixture.league_id == League.id)
    )
    if league_id:
        query = query.filter(League.id == league_id)
    if status:
        query = query.filter(Fixture.status == status)
    if team:
        like = f"%{team}%"
        query = query.filter(or_(home.name.ilike(like), away.name.ilike(like)))
    if date_from:
        query = query.filter(Fixture.kickoff_at >= dt.datetime.fromisoformat(date_from))
    if date_to:
        query = query.filter(Fixture.kickoff_at <= dt.datetime.fromisoformat(date_to))

    rows = query.order_by(Fixture.kickoff_at.desc()).limit(limit).all()
    return [_serialize_fixture_row(fx, h, a, lg) for fx, h, a, lg in rows]


@router.get("/fixtures/{fixture_id}")
def get_fixture(fixture_id: int) -> dict:
    session = get_session()
    fixture = session.get(Fixture, fixture_id)
    if fixture is None:
        raise HTTPException(status_code=404, detail=f"Fixture {fixture_id} no existe")
    return _serialize_fixture_row(fixture, fixture.home_team, fixture.away_team, fixture.league) | {
        "venue": fixture.venue,
        "referee": fixture.referee,
    }
