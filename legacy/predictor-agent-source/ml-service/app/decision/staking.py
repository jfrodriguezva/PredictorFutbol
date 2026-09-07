"""Regla de decisión de staking: Kelly Criterion sobre el edge modelo-vs-mercado.

No es un árbol de decisión de scikit-learn (el modelo de predicción ya es LightGBM,
más preciso que un árbol simple) — es la política de decisión real de un apostador
profesional: cuánto apostar dado el edge, no solo qué resultado es más probable.
"""

from __future__ import annotations

# Kelly fraccionado: nunca se sugiere el Kelly completo (demasiado agresivo/volátil
# para bankroll real), se tope a un porcentaje conservador del bankroll por apuesta.
MAX_STAKE_PCT_BANKROLL = 0.05

OUTCOME_LABELS = {"home": "Local", "draw": "Empate", "away": "Visitante"}


def implied_probability(decimal_odds: float) -> float:
    return 1.0 / decimal_odds


def kelly_fraction(model_prob: float, decimal_odds: float) -> float:
    """Fracción óptima de bankroll según Kelly. b = ganancia neta por unidad apostada."""
    b = decimal_odds - 1
    if b <= 0:
        return 0.0
    edge = model_prob * decimal_odds - 1
    return max(0.0, edge / b)


def recommend_stakes(probabilities: dict[str, float], odds: dict[str, float]) -> dict[str, dict]:
    """Para cada resultado (home/draw/away): probabilidad implícita del mercado, edge
    del modelo sobre esa probabilidad, y stake sugerido (Kelly fraccionado, con tope
    MAX_STAKE_PCT_BANKROLL) — solo cuando el edge es positivo (value bet)."""
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
