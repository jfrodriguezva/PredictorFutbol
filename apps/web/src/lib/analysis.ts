import Anthropic from "@anthropic-ai/sdk";

import type { FixtureDetail, PredictResponse } from "./mlService";

const SYSTEM_PROMPT = `Eres un analista deportivo experto y apostador profesional de fútbol, con el estilo
directo y conocedor de un handicapper de casas de apuestas mexicanas (Caliente.mx, Tulotero).
Recibes datos cuantitativos ya calculados (probabilidades de un modelo de Machine Learning, Elo,
forma reciente, H2H, descanso, tabla) y tu trabajo es redactar un análisis "ultra detallado" en
español para alguien que quiere entender el partido antes de decidir su apuesta.

Reglas:
- Basa TODO tu análisis en los datos que se te dan; no inventes estadísticas, lesiones ni resultados
  que no estén en los datos.
- Estructura la respuesta en markdown con estas secciones exactas: "## Resumen del partido",
  "## Lo que dicen los números" (interpreta Elo, forma, H2H, descanso, tabla), "## Pick del día"
  (Local, Empate o Visitante, y por qué) y "## Nivel de confianza" (Alto/Medio/Bajo).
- Sé directo y con autoridad, como alguien que ha analizado miles de partidos, pero nunca prometas
  un resultado seguro ni dinero garantizado.
- Cierra siempre con un disclaimer breve de juego responsable.`;

function buildUserPrompt(fixture: FixtureDetail, prediction: PredictResponse): string {
  const payload = {
    liga: fixture.league,
    local: fixture.home_team.name,
    visitante: fixture.away_team.name,
    fecha: fixture.kickoff_at,
    estado: fixture.status,
    probabilidades_modelo: prediction.probabilities,
    features: prediction.features_summary,
    version_modelo: prediction.model_version,
  };
  return `Analiza este partido con los datos que te doy:\n\n${JSON.stringify(payload, null, 2)}`;
}

function fallbackNarrative(fixture: FixtureDetail, prediction: PredictResponse): string {
  const { home, draw, away } = prediction.probabilities;
  const top = home >= draw && home >= away ? "Local" : draw >= away ? "Empate" : "Visitante";
  const topProb = Math.max(home, draw, away);
  const confidence = topProb > 0.5 ? "Alto" : topProb > 0.4 ? "Medio" : "Bajo";
  const f = prediction.features_summary;

  return `## Resumen del partido

${fixture.home_team.name} vs ${fixture.away_team.name} — ${fixture.league.name} (${fixture.league.country}, temporada ${fixture.league.season}).

## Lo que dicen los números

- Elo: ${f.elo_home} (local) vs ${f.elo_away} (visitante)
- Forma últimos 5 partidos (puntos): local ${f.form_pts_home_last5}, visitante ${f.form_pts_away_last5}
- H2H reciente (perspectiva del local): ${f.h2h_home_wins}V-${f.h2h_draws}E-${f.h2h_away_wins}D
- Descanso: local ${f.rest_days_home} días, visitante ${f.rest_days_away} días

## Pick del día

**${top}** — probabilidad del modelo: ${(topProb * 100).toFixed(1)}%.

## Nivel de confianza

${confidence}

---
_Análisis generado a partir de un modelo estadístico. No constituye garantía de resultado. Apostar
implica riesgo — juega con responsabilidad._

_Nota: configura ANTHROPIC_API_KEY para obtener el análisis narrado completo de nuestro experto IA._`;
}

export async function generateAnalysis(
  fixture: FixtureDetail,
  prediction: PredictResponse,
): Promise<string> {
  if (!process.env.ANTHROPIC_API_KEY) {
    return fallbackNarrative(fixture, prediction);
  }

  try {
    const client = new Anthropic();
    const response = await client.messages.create({
      model: "claude-opus-5",
      max_tokens: 2048,
      system: SYSTEM_PROMPT,
      messages: [{ role: "user", content: buildUserPrompt(fixture, prediction) }],
    });

    const textBlock = response.content.find(
      (block): block is Anthropic.TextBlock => block.type === "text",
    );
    return textBlock?.text ?? fallbackNarrative(fixture, prediction);
  } catch (error) {
    if (error instanceof Anthropic.AuthenticationError) {
      console.error("ANTHROPIC_API_KEY inválida, usando narrativa de respaldo:", error.message);
    } else if (error instanceof Anthropic.RateLimitError) {
      console.error("Rate limit de Anthropic alcanzado, usando narrativa de respaldo");
    } else if (error instanceof Anthropic.APIError) {
      console.error("Error de la API de Anthropic, usando narrativa de respaldo:", error.message);
    } else {
      console.error("Error inesperado generando narrativa:", error);
    }
    return fallbackNarrative(fixture, prediction);
  }
}
