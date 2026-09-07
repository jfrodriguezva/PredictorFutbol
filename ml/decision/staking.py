"""Staking decision: Kelly Criterion over the model-vs-market edge.

Pure function module, no I/O — ported from the standalone agent prototype (see
docs/football-model.md). Consumes probabilities/odds already resolved by the caller
(the C# backend, from OddsSnapshot); never fetches odds itself.
"""

from __future__ import annotations

# Fractional Kelly: the full Kelly stake is never suggested (too aggressive/volatile for
# a real bankroll) — capped at a conservative percentage of bankroll per bet.
MAX_STAKE_PCT_BANKROLL = 0.05

OUTCOME_LABELS = {"home": "Local", "draw": "Empate", "away": "Visitante"}


def implied_probability(decimal_odds: float) -> float:
    return 1.0 / decimal_odds


def kelly_fraction(model_prob: float, decimal_odds: float) -> float:
    """Optimal bankroll fraction per Kelly. b = net profit per unit staked."""
    b = decimal_odds - 1
    if b <= 0:
        return 0.0
    edge = model_prob * decimal_odds - 1
    return max(0.0, edge / b)


def recommend_stakes(probabilities: dict[str, float], odds: dict[str, float]) -> dict[str, dict]:
    """For each outcome (home/draw/away): market implied probability, the model's edge
    over it, and the suggested stake (fractional Kelly, capped at MAX_STAKE_PCT_BANKROLL)
    — only meaningful when the edge is positive (value bet)."""
    result: dict[str, dict] = {}
    for outcome in ("home", "draw", "away"):
        prob = probabilities[outcome]
        decimal_odds = odds[outcome]
        implied = implied_probability(decimal_odds)
        edge = prob - implied
        full_kelly = kelly_fraction(prob, decimal_odds)
        suggested = min(full_kelly, MAX_STAKE_PCT_BANKROLL)
        result[outcome] = {
            "label": OUTCOME_LABELS[outcome],
            "decimal_odds": round(decimal_odds, 3),
            "implied_probability": round(implied, 4),
            "model_probability": round(prob, 4),
            "edge": round(edge, 4),
            "is_value_bet": edge > 0,
            "kelly_fraction_full": round(full_kelly, 4),
            "suggested_stake_pct_bankroll": round(suggested, 4),
        }
    return result
