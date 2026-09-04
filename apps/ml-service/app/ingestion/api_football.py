from __future__ import annotations

import httpx
from tenacity import retry, retry_if_exception_type, stop_after_attempt, wait_exponential

from app.config import settings

BASE_URL = "https://v3.football.api-sports.io"


class ApiFootballError(Exception):
    pass


class ApiFootballClient:
    """Cliente para API-Football (api-sports.io, plan directo, no RapidAPI)."""

    def __init__(self, api_key: str | None = None, timeout: float = 20.0):
        self._client = httpx.Client(
            base_url=BASE_URL,
            headers={"x-apisports-key": api_key or settings.api_football_key},
            timeout=timeout,
        )

    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> "ApiFootballClient":
        return self

    def __exit__(self, *exc_info: object) -> None:
        self.close()

    @retry(
        stop=stop_after_attempt(4),
        wait=wait_exponential(multiplier=1, min=2, max=30),
        retry=retry_if_exception_type((httpx.TransportError, ApiFootballError)),
        reraise=True,
    )
    def _get(self, path: str, params: dict | None = None) -> dict:
        resp = self._client.get(path, params=params)
        if resp.status_code == 429:
            raise ApiFootballError(f"Rate limited (429) en {path}")
        resp.raise_for_status()
        data = resp.json()
        errors = data.get("errors")
        if errors:
            raise ApiFootballError(f"API-Football error en {path}: {errors}")
        return data

    def get_leagues(
        self,
        *,
        league_id: int | None = None,
        country: str | None = None,
        season: int | None = None,
    ) -> list[dict]:
        params: dict = {}
        if league_id:
            params["id"] = league_id
        if country:
            params["country"] = country
        if season:
            params["season"] = season
        return self._get("/leagues", params)["response"]

    def get_teams(self, league_id: int, season: int) -> list[dict]:
        return self._get("/teams", {"league": league_id, "season": season})["response"]

    def get_fixtures(
        self,
        league_id: int,
        season: int,
        *,
        status: str | None = None,
        from_date: str | None = None,
        to_date: str | None = None,
    ) -> list[dict]:
        params: dict = {"league": league_id, "season": season}
        if status:
            params["status"] = status
        if from_date:
            params["from"] = from_date
        if to_date:
            params["to"] = to_date
        return self._get("/fixtures", params)["response"]

    def get_fixture_statistics(self, fixture_id: int) -> list[dict]:
        return self._get("/fixtures/statistics", {"fixture": fixture_id})["response"]

    def get_h2h(self, team1_id: int, team2_id: int, *, last: int = 10) -> list[dict]:
        return self._get(
            "/fixtures/headtohead", {"h2h": f"{team1_id}-{team2_id}", "last": last}
        )["response"]

    def get_standings(self, league_id: int, season: int) -> list[dict]:
        return self._get("/standings", {"league": league_id, "season": season})["response"]

    def get_odds(self, fixture_id: int) -> list[dict]:
        return self._get("/odds", {"fixture": fixture_id})["response"]
