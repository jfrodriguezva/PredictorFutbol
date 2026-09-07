# Bitácora del modelo Football 1X2 — historial y estado actual

Este documento existe para que **cualquier sesión de Claude (u otra persona)
que retome este proyecto en otra máquina** entienda de inmediato qué se probó,
qué funcionó, qué no, y qué falta — sin tener que redescubrirlo. La memoria
conversacional de Claude vive en el equipo donde corrió la sesión, no viaja
con la carpeta del proyecto; este archivo sí.

## Estado actual (última versión: `football_1x2_v009`)

- **Algoritmo activo:** `Ensemble` (promedio de probabilidades de
  LogisticRegression + XGBoost + LightGBM, cada uno ya entrenado por
  separado — ver `training/football_1x2.py`, clase `AveragingEnsemble`).
- **Dataset:** 26,713 partidos reales terminados, 2020–2026, de 15
  competiciones (ver lista abajo), todos con features calculadas.
- **Accuracy (split cronológico único):** ~51.2% · **Log loss:** ~1.001 ·
  **Brier:** ~0.598.
- **Backtest walk-forward (10 ventanas cronológicas, componente LogReg):**
  accuracy promedio ~50.1% (rango 47.7–52.1%), error de calibración esperado
  (ECE) ~0.027 — muy bien calibrado.
- **Sin datos de odds reales:** verificado explícitamente contra la cuenta
  real de API-Football — cero cobertura incluso en Premier League, La Liga,
  Serie A y Ligue 1. Los benchmarks del módulo `evaluation/benchmarks.py`
  caen a un placeholder uniforme (33.3%/33.3%/33.3%) cuando no hay odds —
  **no son una comparación real contra una casa de apuestas**, aunque el
  código los llame `bookmaker_implied`. No confundir esto con un benchmark
  de mercado genuino.

## Competiciones importadas (`TrackedCompetition`, temporadas 2020–2026)

Liga MX (262), Brasil Serie A (71), Argentina Liga Profesional (128), USA MLS
(253), España La Liga (140), Francia Ligue 1 (61), Inglaterra Premier League
(39), Escocia Premiership (179), Portugal Primeira Liga (94), Italia Serie A
(135), UEFA Champions League (2), UEFA Europa League (3), CONCACAF Champions
League (16), CONMEBOL Libertadores (13), CONMEBOL Sudamericana (11).

## Progresión de accuracy — qué movió la aguja y qué no

| Versión | Cambio | Accuracy |
|---|---|---|
| v001 | Solo Liga MX, 36 partidos | 37.5% |
| v002 | Fix: standings-antes-del-kickoff calculado desde resultados reales (no snapshots, ver abajo) | 46.4% |
| v003 | 15 competiciones combinadas, 3 temporadas, 13,567 partidos | ~49.0% |
| v006 | +6 temporadas (2020–2026), Elo, head-to-head, rest-days, cup-flag, hiperparámetros de XGBoost/LightGBM ajustados | ~50.2% |
| v008 | +forma específica por sede (local/visitante), +Ensemble de los 3 modelos | ~51.2% |
| v009 | +`poisson_draw_probability` (ver "Empates" abajo) | ~50.9% (sin cambio neto significativo) |

**Lo que dio ganancias reales:** arreglar bugs de datos (+9 puntos), sumar
más ligas y temporadas (+2.6 puntos), afinar hiperparámetros de los árboles
(+1.2 puntos), ensemblar los 3 modelos (+0.4 puntos).

**Lo que NO ayudó:**
- Features "diff" explícitas (`home_elo_before - away_elo_before`, etc.):
  matemáticamente redundantes para un modelo lineal — LogisticRegression ya
  puede formar esa combinación con sus propios coeficientes. Confirmado:
  resultados idénticos a 15 decimales con y sin ellas.
- Más temporadas históricas por sí solas, una vez agotado el "salto" inicial
  de pasar de un dataset pequeño a uno grande — rendimientos claramente
  decrecientes.

## El bug de standings-antes-del-kickoff (el fix de mayor impacto)

`API-Football` solo expone la tabla de posiciones **actual** vía
`/standings` — nunca un snapshot histórico "a esa fecha". Un
`StandingSnapshot` capturado hoy siempre tiene `CapturedAt` posterior a
cualquier partido ya jugado, así que buscar "el snapshot más reciente antes
del kickoff" nunca encontraba nada para partidos históricos: la feature
estaba vacía en el 100% de los casos.

**Fix:** `DatasetBuilderService.ComputeStandingsToDateAsync` reconstruye la
tabla de posiciones directamente desde los resultados de partidos ya
guardados en SQLite (solo partidos con `MatchDate` estrictamente anterior),
sin depender de ningún snapshot externo. Sigue siendo 100% anti-leakage y
ahora tiene cobertura ~94-100% en vez de ~0%.

## El diagnóstico de los empates (hallazgo más importante de errores)

Análisis de errores por categoría sobre el conjunto de prueba (5,343
partidos nunca vistos en entrenamiento) reveló un patrón sistemático, no
ruido aleatorio:

- El modelo predice "Empate" como resultado más probable en solo 14-26 de
  5,343 partidos (0.3–0.7%), aunque los empates son ~25% de los resultados
  reales.
- La probabilidad de empate promedio que asigna el modelo es casi idéntica
  entre partidos que sí terminaron en empate (25.5%) y los que no (24.2–24.5%)
  — casi no hay separación, con o sin la feature de Poisson agregada en v009.
- **Causa raíz:** Elo/puntos/forma miden "quién es más fuerte", no "qué tan
  poco van a anotar ambos equipos" — que es lo que realmente predice un
  empate. Se agregó `poisson_draw_probability` (tasa de gol reciente de
  ataque/defensa vía aproximación Poisson independiente) para atacar esto
  directamente — ayudó un poco (recall de empates casi se duplicó, de 0.3%
  a 0.7%) pero sigue siendo estructuralmente difícil: un empate rara vez es
  la opción de MAYOR probabilidad de las tres, incluso cuando el partido es
  genuinamente cerrado y de poco gol.

## Catálogo completo de features

Ver [`FootballFeatureNames.cs`](../backend/SportsPredictor.Application/Datasets/FootballFeatureNames.cs)
— es la fuente de verdad. Resumen por grupo:

- **Tabla de posiciones antes del kickoff:** puntos, diferencia de gol,
  posición (calculados desde resultados, ver arriba).
- **Forma reciente:** puntos en últimos 5 partidos (general), y también
  específica por sede (últimos 5 como local / últimos 5 como visitante).
- **Elo:** rating global por equipo, across todas las competiciones/temporadas
  (nunca se reinicia), con ventaja de local de 60 puntos, K=20.
- **Head-to-head:** % de victorias del equipo local en los últimos 5
  enfrentamientos directos (0.5 = neutral si no hay historial).
- **Días de descanso:** desde el último partido de cada equipo (7 días =
  default si no hay partido previo).
- **`is_cup_competition`:** 1 si es copa/eliminatoria, 0 si es liga.
- **Lesiones/alineaciones:** conteo de lesiones en ventana de 14 días,
  bandera de alineación anunciada.
- **Odds de mercado:** siempre vacías en este dataset (ver arriba).
- **`poisson_draw_probability`:** P(mismo marcador) vía Poisson independiente
  usando tasas de gol de los últimos 5 partidos — ver diagnóstico de empates.
- **Diffs explícitos** (`elo_diff`, `points_diff`, etc.): presentes en el
  catálogo pero confirmados sin efecto (ver arriba) — no se quitaron porque
  no hacen daño, solo no ayudan.

## Qué falta — próximos pasos legítimos, en orden de costo/beneficio esperado

1. **Fallback para standings faltantes** (~4% de partidos, equipos nuevos o
   inicio de temporada): hoy esas filas quedan sin esta feature y su
   accuracy es ~5 puntos peor. Barato de implementar (usar la última
   posición de la temporada anterior, o un valor de liga-promedio).
2. **Estadísticas de partido** (`/fixtures/statistics`, `/fixtures/events` de
   API-Football — tiros, posesión, corners, tarjetas; Prioridad 2 de
   `CLAUDE.md` sección 7): nunca importadas. Requeriría nuevos
   `SyncStatisticsJob`/`SyncEventsJob`, features de tasa reciente (como las
   de forma/goles), y volver a pagar cuota de API-Football por partido.
3. **Altitud/clima/viajes** (`CLAUDE.md` sección 22): Liga MX tiene efectos
   de altitud documentados (México/Toluca). `Venue` ya tiene
   `AltitudeMeters`/`Latitude`/`Longitude` en el dominio pero no se usa como
   feature.
4. **Odds reales de otro proveedor** (Odds API, Betfair histórico, etc.) —
   API-Football simplemente no las tiene en este plan/estas ligas. Sería la
   ganancia potencial más grande, pero requiere una fuente de datos nueva,
   fuera del alcance de "usar lo que ya tenemos".

## Advertencia metodológica (no ignorar)

Cada ronda de "prueba una feature → mide contra el mismo test set → ajusta"
tiene un riesgo real: sobreajustar a ESE split específico por repetición,
no a los datos de entrenamiento. Si se sigue iterando, lo correcto es validar
cualquier cambio nuevo contra una ventana de tiempo que nunca se haya usado
para decidir features anteriores — no seguir mirando el mismo 20% final.
