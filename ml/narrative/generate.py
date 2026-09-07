"""Generates the "expert analyst" narrative for a football_1x2 analysis — ported from
the standalone agent prototype (see docs/football-model.md).

Uses Claude (claude-opus-5) with the native web_search tool to bring in recent context
(injuries, absences, expected lineups) ONLY for matches not yet played — searching news
for a match that already finished adds nothing and can confuse the model. Without
ANTHROPIC_API_KEY it falls back to a template narrative built from the same quantitative
data, so the product still works without that key configured.
"""

from __future__ import annotations

import json

import anthropic

from app.config import settings

SYSTEM_PROMPT = """Eres un analista deportivo experto y apostador profesional de fútbol, con el
estilo directo y conocedor de un handicapper de casas de apuestas mexicanas (Caliente.mx,
Tulotero). Recibes datos cuantitativos ya calculados (probabilidades de un modelo de Machine
Learning, la explicación SHAP de qué features pesaron más en la predicción, y si están
disponibles, cuotas de mercado reales y el tamaño de apuesta sugerido por Kelly Criterion).

Si tienes la herramienta de búsqueda web disponible, puedes usarla para revisar noticias
recientes y relevantes del partido (bajas, lesiones, alineaciones probables) — pero nunca
inventes datos que no puedas verificar, y si no encuentras nada relevante simplemente omite
esa sección.

Reglas:
- Basa el análisis cuantitativo SOLO en los datos que se te dan; no inventes estadísticas.
- Estructura la respuesta en markdown con estas secciones (omite "Contexto reciente" si no
  buscaste o no encontraste nada útil, y omite "Value bet y stake sugerido" si no hay cuotas):
  "## Resumen del partido", "## Lo que dicen los números" (interpreta las features SHAP),
  "## Contexto reciente", "## Pick del día", "## Nivel de confianza",
  "## Value bet y stake sugerido".
- Sé directo y con autoridad, como alguien que ha analizado miles de partidos, pero nunca
  prometas un resultado seguro ni dinero garantizado.
- Cierra siempre con un disclaimer breve de juego responsable."""

_LABEL_ES = {"home": "Local", "draw": "Empate", "away": "Visitante"}

# API-FOOTBALL fixture status codes that mean "already played" — for anything else the
# match is upcoming and web_search is worth enabling.
_FINISHED_STATUSES = {"FT", "AET", "PEN", "CANC", "PST"}


def _fallback_narrative(
    fixture: dict,
    prediction: dict,
    shap_top_features: list[dict],
    odds: dict | None,
    stakes: dict | None,
) -> str:
    probs = prediction["probabilities"]
    top = max(probs, key=probs.get)
    confidence = "Alto" if probs[top] > 0.5 else "Medio" if probs[top] > 0.4 else "Bajo"

    lines = [
        "## Resumen del partido",
        "",
        f"{fixture['home_team']} vs {fixture['away_team']} — {fixture['league_name']} "
        f"({fixture['country']}, temporada {fixture['season']}).",
        "",
        "## Lo que dicen los números",
        "",
    ]
    for item in shap_top_features:
        direction = "a favor del local" if item["impact"] > 0 else "a favor del visitante"
        lines.append(f"- **{item['feature']}**: impacto {item['impact']:+.3f} ({direction})")

    lines += [
        "",
        "## Pick del día",
        "",
        f"**{_LABEL_ES[top]}** — probabilidad del modelo: {probs[top] * 100:.1f}%.",
        "",
        "## Nivel de confianza",
        "",
        confidence,
    ]

    if odds and stakes:
        lines += ["", "## Value bet y stake sugerido", ""]
        for outcome, data in stakes.items():
            marker = "value bet" if data["is_value_bet"] else "sin valor"
            lines.append(
                f"- {data['label']}: cuota {data['decimal_odds']:.2f}, "
                f"edge {data['edge'] * 100:+.1f}pp ({marker}), "
                f"stake sugerido {data['suggested_stake_pct_bankroll'] * 100:.1f}% del bankroll"
            )

    lines += [
        "",
        "---",
        "_Análisis generado a partir de un modelo estadístico. No constituye garantía de "
        "resultado. Apostar implica riesgo — juega con responsabilidad._",
        "",
        "_Nota: configura ANTHROPIC_API_KEY en el servicio ml para obtener el análisis "
        "narrado completo de nuestro experto IA, incluyendo contexto de noticias recientes._",
    ]
    return "\n".join(lines)


def generate_narrative(
    fixture: dict,
    prediction: dict,
    shap_top_features: list[dict],
    odds: dict | None,
    stakes: dict | None,
) -> str:
    if not settings.anthropic_api_key:
        return _fallback_narrative(fixture, prediction, shap_top_features, odds, stakes)

    payload = {
        "partido": fixture,
        "probabilidades_modelo": prediction["probabilities"],
        "features_explicativas_shap": shap_top_features,
        "cuotas_mercado": odds,
        "recomendacion_stake_kelly": stakes,
    }

    is_upcoming = fixture.get("status") not in _FINISHED_STATUSES
    extra_kwargs: dict = {}
    if is_upcoming:
        extra_kwargs["tools"] = [{"type": "web_search_20260209", "name": "web_search", "max_uses": 3}]

    try:
        client = anthropic.Anthropic(api_key=settings.anthropic_api_key)
        response = client.messages.create(
            model="claude-opus-5",
            max_tokens=2048,
            system=SYSTEM_PROMPT,
            **extra_kwargs,
            messages=[
                {
                    "role": "user",
                    "content": (
                        "Analiza este partido con estos datos:\n\n"
                        f"{json.dumps(payload, ensure_ascii=False, indent=2, default=str)}"
                    ),
                }
            ],
        )
        text = "\n\n".join(block.text for block in response.content if block.type == "text").strip()
        return text or _fallback_narrative(fixture, prediction, shap_top_features, odds, stakes)
    except anthropic.APIError as exc:
        print(f"Error llamando a Claude, usando narrativa de respaldo: {exc}")
        return _fallback_narrative(fixture, prediction, shap_top_features, odds, stakes)
