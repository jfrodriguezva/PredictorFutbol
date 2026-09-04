"""Cliente para The Odds API (https://the-odds-api.com) - cuotas de mercado reales,
usadas para comparar contra el modelo y detectar value bets (Fase 6).

NOTA: el mapeo LEAGUE_SPORT_KEY es best-effort a partir de la documentación pública de
The Odds API. Debe validarse contra `GET /v4/sports?apiKey=...` en cuanto haya una
ODDS_API_KEY real, ya que los sport_keys disponibles dependen del plan contratado.
Sin ODDS_API_KEY configurada, o si la liga no está mapeada, o si no se encuentra el
partido en el feed de cuotas vigentes, esto devuelve None sin lanzar error — el resto
del pipeline (agente de análisis) sigue funcionando sin datos de mercado.
"""

from __future__ import annotations

import httpx

from app.config import settings

BASE_URL = "https://api.the-odds-api.com/v4"

LEAGUE_SPORT_KEY: dict[int, str] = {
    39: "soccer_epl",  # Premier League
    262: "soccer_mexico_ligamx",  # Liga MX
}


class OddsApiClient:
    def __init__(self, api_key: str | None = None, timeout: float = 15.0):
        self.api_key = api_key or settings.odds_api_key
        self._client = httpx.Client(base_url=BASE_URL, timeout=timeout)

    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> "OddsApiClient":
        return self

    def __exit__(self, *exc_info: object) -> None:
        self.close()

    def get_odds_for_league(self, sport_key: str) -> list[dict]:
        resp = self._client.get(
            f"/sports/{sport_key}/odds",
            params={
                "apiKey": self.api_key,
                "regions": "us,uk,eu",
                "markets": "h2h",
                "oddsFormat": "decimal",
            },
        )
        resp.raise_for_status()
        return resp.json()


def _normalize(name: str) -> str:
    return "".join(ch.lower() for ch in name if ch.isalnum())


def find_odds_for_fixture(
    league_api_football_id: int, home_team: str, away_team: str
) -> dict[str, float] | None:
    """Promedio de cuotas 1X2 entre casas disponibles para este partido, o None si no
    hay ODDS_API_KEY, la liga no está mapeada, o el partido no aparece en el feed
    (por ejemplo, por ser un partido ya jugado — The Odds API solo cubre próximos)."""
    sport_key = LEAGUE_SPORT_KEY.get(league_api_football_id)
    if not sport_key or not settings.odds_api_key:
        return None

    try:
        with OddsApiClient() as client:
            events = client.get_odds_for_league(sport_key)
    except httpx.HTTPError:
        return None

    home_n, away_n = _normalize(home_team), _normalize(away_team)
    for event in events:
        if _normalize(event.get("home_team", "")) != home_n:
            continue
        if _normalize(event.get("away_team", "")) != away_n:
            continue

        home_odds, draw_odds, away_odds = [], [], []
        for bookmaker in event.get("bookmakers", []):
            for market in bookmaker.get("markets", []):
                if market.get("key") != "h2h":
                    continue
                for outcome in market.get("outcomes", []):
                    if outcome["name"] == event["home_team"]:
                        home_odds.append(outcome["price"])
                    elif outcome["name"] == event["away_team"]:
                        away_odds.append(outcome["price"])
                    elif outcome["name"] == "Draw":
                        draw_odds.append(outcome["price"])

        if home_odds and draw_odds and away_odds:
            return {
                "home": sum(home_odds) / len(home_odds),
                "draw": sum(draw_odds) / len(draw_odds),
                "away": sum(away_odds) / len(away_odds),
            }
    return None
