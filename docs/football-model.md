# Modelo de fútbol — Dataset Builder (Fase 8), 1X2 baseline (Fase 9), calibración/backtesting/benchmarks (Fase 10)

Implementa `CLAUDE.md` sección 18 (Dataset Builder) y arranca el catálogo de
features de la sección 22. Cierra el flujo obligatorio:

```text
API-FOOTBALL → C# Ingestion → SQLite → Dataset Builder → Python ML
```

## Decisión de arquitectura: Python nunca toca SQLite directamente

Se le preguntó al usuario cómo debía llegar el dataset a Python. Eligió que
**C# exponga un endpoint de export** (`GET /api/dataset-builder/{id}/export`)
en vez de que el servicio `ml/` abra `sportspredictor.db` directamente con
un driver Python. Razón: mantiene un solo dueño del esquema (el backend
.NET, que ya corre las migraciones EF Core) y es consistente con la regla
"C# es el orquestador principal" — el mismo principio por el que el
frontend nunca llama a API-FOOTBALL directamente.

## Flujo

1. `POST /api/dataset-builder/{trackedCompetitionId}/build-features` —
   calcula y persiste `FeatureValue` para cada partido **terminado** de esa
   competición/temporada que aún no las tenga. Cada llamada crea snapshots
   nuevos (no sobrescribe — mismo patrón append-only del resto del dominio).
2. `GET /api/dataset-builder/{trackedCompetitionId}/export` — lee
   `FeatureValue` (la más reciente por `(MatchId, FeatureName)`) + `Match`,
   las pivotea en columnas, y devuelve un CSV: una fila por partido
   terminado, con el resultado real (`H`/`D`/`A`) como target para la
   Fase 9.

## Regla anti-data-leakage (sección 19 de `CLAUDE.md`) — cómo se garantiza

Cada feature se calcula usando **exclusivamente** datos con marca de tiempo
anterior al kickoff del partido en cuestión:

- `StandingSnapshot`: se toma el más reciente con `CapturedAt < MatchDate`
  del partido — nunca el snapshot más reciente en general (que podría
  haberse capturado después). Verificado explícitamente por
  `BuildFeaturesAsync_UsesOnlyStandingCapturedBeforeKickoff_NotAfter`.
- Forma reciente: solo partidos **terminados** con `MatchDate` estrictamente
  anterior.
- Injuries: ventana de 14 días antes del kickoff (ver caveat abajo).
- Odds: solo snapshots con `CapturedAt <= MatchDate`.
- `FeatureValue.AvailableAt` se fija al `MatchDate` del partido objetivo —
  un límite conservador correcto, ya que ninguna feature usa información
  posterior a ese instante.

## Catálogo de features (esta iteración)

Documentado en
[`Application/Datasets/FootballFeatureNames.cs`](../backend/SportsPredictor.Application/Datasets/FootballFeatureNames.cs).
El usuario pidió el set "más amplio" (no solo standings/forma, también
injuries/lineups/odds ya que esas snapshots existen desde Fases 6-7):

| Feature | Fuente | Notas |
|---|---|---|
| `home_points_before` / `away_points_before` | `StandingSnapshot.Points` | Snapshot más reciente antes del kickoff |
| `home_goal_diff_before` / `away_goal_diff_before` | `StandingSnapshot.GoalDifference` | ídem |
| `home_rank_before` / `away_rank_before` | `StandingSnapshot.Rank` | ídem |
| `home_form_points_last5` / `away_form_points_last5` | Últimos 5 partidos terminados (calculado, no del campo `Form` de texto) | 3/1/0 por victoria/empate/derrota |
| `home_injuries_count` / `away_injuries_count` | `InjurySnapshot` en ventana de 14 días antes del kickoff | **Aproximación**, ver caveat |
| `home_lineup_announced` / `away_lineup_announced` | Existe `LineupSnapshot` para ese partido+equipo (1/0) | No codifica la formación en sí (ver caveat) |
| `market_implied_home_prob` / `..._draw_prob` / `..._away_prob` | Promedio de `OddsSnapshot.ImpliedProbability` (mercado "Match Winner") entre casas, antes del kickoff | Probabilidad cruda, sin quitar vig (Fase 12) |

**No implementadas todavía** (sección 22 de `CLAUDE.md`, arquitectura queda
extensible para agregarlas): Elo, xG/xGA, shots, shots on target,
possession, corners, cards, rest days, travel distance, altitude,
temperature/humedad, tournament stage, must-win condition, points needed,
coach, formation (como feature categórica real, no solo el flag actual).

### Caveats documentados (no ocultos)

- **`home_injuries_count`/`away_injuries_count` es una aproximación**: cuenta
  reportes de lesión capturados en los 14 días previos al kickoff, sin
  saber si el jugador realmente sigue lesionado en el momento del partido
  (no tenemos fecha de resolución — `InjurySnapshot.ExpectedReturn` queda
  `null` desde la Fase 6, ver docs/database-schema.md). Es una señal
  razonable, no un conteo exacto.
- **`home_lineup_announced`/`away_lineup_announced` es binario**, no
  codifica la formación (`"4-3-3"` etc.) como número — `FeatureValue.NumericValue`
  es estrictamente numérico; la formación como feature categórica
  (one-hot) es trabajo del pipeline de Python en una fase futura, no de
  este Dataset Builder.
- **Las probabilidades de mercado son crudas** (con vig incluido) — ver
  arriba.

## Pruebas

`DatasetBuilderServiceTests`: anti-leakage explícito (standing antes vs.
después del kickoff), cálculo de forma, ventana de injuries, promedio de
odds, contenido del CSV exportado (columnas + resultado), y
`NotFoundException` sobre una competición no rastreada. 63/63 tests totales
en la solución tras esta fase.

---

## Fase 9 — Football 1X2 baseline

Implementa `CLAUDE.md` sección 20: comparar Logistic Regression, XGBoost y
LightGBM para el target Home/Draw/Away, seleccionando **principalmente por
Log Loss y Brier Score, nunca solo por Accuracy**.

### Dos decisiones de arquitectura resueltas con el usuario antes de programar

1. **¿Quién registra `ModelVersion`/`TrainingRun`?** Python entrena
   (`ml/training/football_1x2.py`, expuesto vía `POST /train/football-1x2`
   en `ml/app/main.py`) y devuelve **solo metadatos** (métricas, versión,
   ruta del artefacto) — nunca escribe en `sportspredictor.db`. El backend
   C# (`ModelTrainingService`) es quien crea las filas `ModelVersion` y
   `TrainingRun`. Mantiene la regla de la Fase 8: **solo C# escribe en
   SQLite**.
2. **¿Con qué datos se prueba el pipeline?** No hay partidos reales
   todavía (sin `API_FOOTBALL_KEY`). El pipeline se probó con datos
   sintéticos generados en los propios tests
   (`ml/tests/test_football_1x2_training.py`), **nunca presentados como
   partidos reales** — solo para verificar que el split cronológico, el
   cálculo de métricas y la selección de modelo funcionan correctamente.
   Cuando haya datos reales, el mismo pipeline corre sin cambios de código.

### Flujo end-to-end

```text
POST /api/model-versions/train-football-1x2/{trackedCompetitionId}
  │
  ├─ 1. IDatasetBuilderService.ExportDatasetCsvAsync (Fase 8, sin cambios)
  ├─ 2. IMlServiceClient.TrainFootball1X2Async — POST al servicio Python
  │      con el CSV como body; timeout largo (300s por defecto,
  │      MlService:TimeoutSeconds) porque entrenar puede tardar
  ├─ 3. Python: carga el CSV, split cronológico (¡nunca aleatorio! —
  │      sección 26 de CLAUDE.md, "NUNCA random split para series
  │      temporales"), entrena los 3 algoritmos, calcula Log Loss +
  │      Brier Score multiclase + Accuracy (referencia, no criterio),
  │      selecciona por menor Log Loss, guarda el artefacto (.joblib) en
  │      ml/models/ versionado (v001, v002... nunca sobrescribe)
  └─ 4. C# crea ModelVersion (Algorithm/LogLoss/BrierScore = los del
         ganador) + TrainingRun (MetricsJson = los 3 candidatos completos,
         para poder auditar qué se comparó) — Active queda en false por
         defecto (promoción manual es una decisión de fase futura)
```

### Por qué no hay calibración completa todavía

`CLAUDE.md` sección 26 pide reliability diagrams, isotonic regression y
Platt scaling — eso es explícitamente el trabajo de la **Fase 10**
("Calibration + backtesting + benchmarks"). Esta fase solo reporta Log Loss
y Brier Score como criterio de selección; no genera curvas de calibración
ni compara contra benchmarks (favorito, Elo, bookmaker, closing line) —
eso también es Fase 10 (sección 27).

### Requisito mínimo de datos

`train_and_select` exige al menos 20 partidos terminados
(`MIN_MATCHES_REQUIRED`); menos que eso y el pipeline rechaza el
entrenamiento (`ValueError` → HTTP 422) en vez de entrenar sobre una
muestra demasiado pequeña para decir algo significativo.

### Pruebas

- **Python** (`ml/tests/test_football_1x2_training.py`,
  `test_train_endpoint.py`): parseo del CSV, orden cronológico, rechazo por
  pocos partidos, comparación de los 3 algoritmos, selección por Log Loss
  (no Accuracy) verificada explícitamente, split cronológico (no aleatorio)
  verificado explícitamente, Brier score multiclase, versionado de
  artefactos sin sobrescribir. 13/13 tests Python.
- **C#** (`MlServiceClientTests`, `ModelTrainingServiceTests`): parseo de
  la respuesta del servicio ML, envío correcto del CSV, `MlServiceException`
  en error, registro de `ModelVersion`/`TrainingRun` con las métricas del
  algoritmo ganador, dos entrenamientos crean dos versiones separadas
  (nunca se sobrescribe), orden por más reciente. 71/71 tests totales en
  la solución .NET tras esta fase.

### Verificado en runtime real

Se corrió el flujo Python completo de punta a punta (servidor FastAPI real
+ dataset sintético vía `curl`): los 3 algoritmos se entrenaron, XGBoost
fue seleccionado por menor Log Loss, y el artefacto `.joblib` se guardó
correctamente en `ml/models/`. El lado C# se verificó con la API corriendo
de verdad (`GET /api/model-versions` → `200` lista vacía;
`POST .../train-football-1x2/{id-inexistente}` → `404` limpio, sin llegar
a llamar a Python). No se hizo una prueba end-to-end completa con ambos
servicios y una `TrackedCompetition` real con partidos (requeriría datos
reales de API-Football); la orquestación completa está cubierta por
`ModelTrainingServiceTests` con dobles de prueba.

---

## Fase 10 — Calibración, backtesting y benchmarks

Implementa `CLAUDE.md` secciones 26-27, con alcance acotado a lo
alcanzable con los datos actuales (documentado explícitamente, no oculto).

### `ml/evaluation/calibration.py`

Calibración *top-label*: para cada partido, la confianza (probabilidad más
alta que dio el modelo) y si acertó o no, agrupadas en bins de confianza.
El **Expected Calibration Error (ECE)** es el promedio ponderado de
`|confianza_promedio − frecuencia_real|` por bin. Isotonic regression /
Platt scaling (recalibrar el modelo de verdad, no solo medir qué tan
calibrado está) **no** están implementados todavía.

### `ml/evaluation/benchmarks.py`

Dos benchmarks alcanzables con los datos que ya tenemos:
1. **Siempre el favorito** (mayor probabilidad implícita de mercado).
2. **Las probabilidades del bookmaker usadas directamente como "modelo"**
   (normalizadas para sumar 1 — una normalización simple para poder medir
   Log Loss/Brier, **no** el mismo cálculo de remoción de vig con
   principios de arbitraje que hará la Fase 12 para Expected Value).

**No implementados, documentado por qué**: Elo (no existe todavía un
rating Elo — sería una feature nueva de la sección 22 con su propio
cálculo histórico) y comparación real contra la *closing line* (el
Dataset Builder promedia todas las odds antes del kickoff; distinguir
"apertura" de "cierre" necesitaría una feature dedicada). La comparación
contra las predicciones de API-Football también queda pendiente de que
existan filas `ApiFootballPredictionSnapshot` reales para unir.

### `ml/backtesting/walk_forward.py`

Walk-forward real: N ventanas móviles, cada una entrena con todo lo
anterior y evalúa en la siguiente porción cronológica — **nunca** un split
aleatorio (verificado con un test explícito: el tamaño de entrenamiento
debe crecer estrictamente ventana a ventana). Reporta por ventana:
Accuracy, Precision/Recall/F1 (macro), Log Loss, Brier Score,
Calibration Error, y métricas de apuestas (Total Bets, Win Rate, ROI,
Average Odds) más Max Drawdown agregado sobre toda la curva de ganancias.

**Aproximación documentada**: ROI/Yield/Win Rate/Average Odds usan cuotas
decimales **aproximadas** (`1 / probabilidad_implícita_de_mercado`) porque
el CSV del Dataset Builder solo trae la probabilidad promedio, no la cuota
decimal cruda por bookmaker — no son precios reales de casas de apuestas.

### Endpoint nuevo

`POST /evaluate/football-1x2` (Python) — recibe el mismo CSV que
`/train`, no persiste nada, y devuelve calibración + backtest + benchmarks.
`POST /api/model-versions/evaluate-football-1x2/{trackedCompetitionId}`
(C#) — pasa el CSV exportado a Python y reenvía el resultado tal cual;
puramente diagnóstico, no crea `ModelVersion` ni `TrainingRun`.

### Pruebas

Python (25/25 tras esta fase): calibración (perfecta → error 0, sobre-
confiada → error alto, bins solo donde hay datos), benchmarks (métricas
válidas, normalización a 1), walk-forward (ventanas crecientes = prueba de
que no es aleatorio, agregación de apuestas/drawdown), endpoint `/evaluate`.
C# (74/74 tras esta fase): parseo de la respuesta de evaluación,
`MlServiceException` en error, y que `EvaluateFootball1X2Async` no persiste
nada en la base (a diferencia de `TrainFootball1X2Async`).

Verificado en runtime real con un dataset sintético vía `curl`: ECE, 5
ventanas de backtest, y los 2 benchmarks, todos devueltos correctamente.

---

## Fase 11 — Expected Goals (Poisson / Dixon-Coles)

`ml/training/goals_poisson.py`: modelo de goles independiente del 1X2
(`CLAUDE.md` sección 21). Ajusta un GLM de Poisson (`statsmodels`) sobre un
dataset "doblado" (una fila por equipo por partido: goles propios, rival,
si jugó de local), obteniendo fuerza de ataque/defensa por equipo +
ventaja de local. De ahí se derivan `HomeExpectedGoals`/`AwayExpectedGoals`,
la matriz de probabilidad de marcador (Poisson independiente, o con el
ajuste de correlación de bajo marcador de Dixon-Coles si se pasa `rho`), y
los mercados Over/Under 2.5 y BTTS.

Endpoint Python `POST /predict/goals` (recibe el mismo CSV que `/train`, más
`home_team`/`away_team`) y su passthrough C#
`POST /api/model-versions/predict-goals/{trackedCompetitionId}` — ambos
diagnósticos, no persisten nada.

## Fase 9 (pieza faltante) — predicción real para un partido puntual

Durante la Fase 12 se detectó que faltaba la pieza central: **nunca existió
un endpoint que prediga un partido real específico** usando un modelo ya
entrenado (`/train` solo compara algoritmos y guarda el artefacto). Se
agregó:

- `ml/training/football_1x2.py`: `load_artifact()` + `predict_single()` —
  carga el `.joblib` guardado y predice un solo partido a partir de un
  diccionario de features (rellena las que falten con 0, igual que en
  entrenamiento).
- Endpoint Python `POST /predict/football-1x2` (`artifact_path` + `features`).
- `DatasetBuilderService.ComputeFeaturesForPredictionAsync` (C#): calcula el
  mismo catálogo de features de la Fase 8 pero para **un solo partido**,
  sin importar su estado (a diferencia del builder de datasets, que solo
  procesa partidos terminados) — así se puede predecir un partido que
  todavía no se jugó.
- `IPredictionGenerationService` / `PredictionGenerationService`: junta
  todo — busca el partido, el `ModelVersion` (el más reciente si no se
  especifica), calcula features, llama a Python, calcula EV contra las
  odds conocidas (Fase 12), y persiste una fila `Prediction` por
  selección (Home/Draw/Away) — igual que siempre, nunca se sobrescribe
  después.
- Endpoints: `POST /api/predictions/generate/{matchId}`,
  `GET /api/predictions/match/{matchId}`, `GET /api/predictions/recommended`.

Esta es la funcionalidad que expone el botón **"Generate Prediction"** en el
dashboard (Fase 13).

## Ciclo de evaluación y "aprendizaje" por partido

`Prediction.ActualOutcome`/`IsCorrect` existían desde la Fase 2 pero nunca se
llenaban. Se cierra el ciclo con:

- `IPredictionGenerationService.EvaluateMatchAsync(matchId)`: dado un partido
  ya `Finished`, calcula el resultado real (Home/Draw/Away por marcador) y
  actualiza **solo** `ActualOutcome`/`IsCorrect` de cada `Prediction` ya
  guardada para ese partido — `Probability`, `ExpectedValue`, `Selection` y
  `PredictionDate` nunca se tocan (`CLAUDE.md` sección 29). Lanza si el
  partido todavía no terminó — no hay resultado real contra qué comparar.
- `GetAccuracySummaryAsync`: aciertos totales y de las apuestas
  `Recommended` específicamente, más la lista de fallos recientes entre las
  recomendadas (lo más accionable para revisar).
- `POST /api/predictions/learn-from-match/{matchId}?trackedCompetitionId=...`:
  el ciclo completo en una llamada — evalúa el resultado, reconstruye
  features de toda la competición (ahora incluyendo este partido, vía
  `DatasetBuilderService.BuildFeaturesAsync`, que **no** se llamaba
  automáticamente antes de entrenar — se detectó como un hueco real:
  `TrainFootball1X2Async` exporta el CSV directo desde `FeatureValues` sin
  recalcularlas primero) y reentrena, devolviendo la nueva `ModelVersion`.

**Importante, para que quede claro qué es y qué no es**: esto **no es
aprendizaje en línea**. Es un reentrenamiento completo disparado por
partido en vez de por calendario — el modelo mejora porque el dataset
crece con cada resultado real, no porque ajuste sus pesos incrementalmente.
Automatizarlo con un scheduler es exactamente la Fase 18 (no implementada).

Frontend: en `/matches/[id]`, botones **"Evaluate Result"** (marca
✓/✗ en cada predicción) y **"Learn from this match"** (visible solo si se
llegó desde `/tracked-competitions/[id]`, que ahora pasa el
`trackedCompetitionId` por query string). Página nueva `/accuracy`:
precisión global + de recomendadas + tabla de fallos recientes.

Cubierto por 6 tests nuevos en `PredictionGenerationServiceTests` (evaluar
antes de tiempo lanza, marca correcto/incorrecto sin tocar `Probability`,
partido desconocido lanza `NotFoundException`, resumen de precisión
correcto incluyendo fallos recomendados, resumen vacío cuando no hay nada
evaluado todavía).

## Fase 12 — Expected Value

`backend/SportsPredictor.Application/Betting/`: `OddsConversion`
(americano ↔ decimal ↔ probabilidad implícita), `VigRemoval` (normaliza un
mercado mutuamente excluyente para que sume 1), `ExpectedValueCalculator`
(`EV = prob_modelo * odds_decimales - 1`, categorizado en
Strong Value / Value / Neutral / Negative Value). Pura lógica de dominio,
sin infraestructura — igual que el resto de `Application`. Una selección
solo se marca `Recommended` si su EV cae en Value o Strong Value — nunca
por ser favorito (verificado explícitamente en
`Categorize_NeverRecommendsSolelyBecauseFavorite`).

## Fase 13 — Dashboard (Next.js)

Páginas reales conectadas a la API (antes solo existía el health-check de
la Fase 1): `/tracked-competitions` (alta + sync), `/tracked-competitions/[id]`
(lista de partidos), `/matches/[id]` (botón **Generate Prediction** +
historial de predicciones), `/models` (entrenar + listar), `/value-bets`
(predicciones recomendadas), `/progol` (optimizador). Verificado en
runtime real (navegador embebido) contra el backend real corriendo en el
puerto `20050` — sin errores de consola.

## Fase 14 — Optimizador de Progol

`backend/SportsPredictor.Application/Progol/ProgolOptimizer.cs`:
heurística documentada (**no** un solver de programación entera completo).
Ordena los partidos por incertidumbre (margen entre las dos probabilidades
más altas) y gasta ahí los dobles/triples permitidos, respetando el
presupuesto (`2^dobles * 3^triples <= budget`, reducido greedily si se
excede). Cuatro escenarios: Safe (todo sencillo), Balanced (los
dobles/triples pedidos), Aggressive (más de los pedidos, si el presupuesto
alcanza), Contrarian (busca la sorpresa: parte de la probabilidad más
baja). La "probabilidad estimada de pleno" es el producto de las
probabilidades de acierto por partido — asumiendo independencia entre
partidos, una simplificación estándar y documentada, no estrictamente
cierta. Sin persistencia — es un cálculo puro. Endpoint
`POST /api/progol/optimize`.

