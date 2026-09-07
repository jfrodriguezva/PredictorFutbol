# Sports Predictor — CLAUDE.md

## 0. Propósito

Construir una aplicación local, modular y extensible para análisis y predicción deportiva.

La primera prioridad es FÚTBOL usando API-FOOTBALL / API-SPORTS como proveedor principal de datos estructurados.

La aplicación debe ser capaz de:

- importar y conservar datos históricos;
- sincronizar datos actuales;
- guardar snapshots de standings, odds, lesiones, alineaciones y predicciones;
- construir datasets reproducibles;
- entrenar modelos estadísticos/ML;
- evaluar y calibrar probabilidades;
- comparar modelos propios contra mercado y API-FOOTBALL;
- hacer backtesting temporal;
- generar predicciones 1X2, goles, BTTS, Over/Under y marcadores;
- calcular valor esperado cuando existan momios;
- optimizar quinielas tipo Progol;
- más adelante agregar NFL y Protouch como módulos independientes.

Principio fundamental:

> No afirmar que un modelo es bueno si las métricas históricas no lo demuestran.

Nunca garantizar ganancias ni tratar una predicción como certeza.

---

# 1. Stack obligatorio

## Frontend
- Next.js
- React
- TypeScript
- Tailwind CSS
- Recharts o librería equivalente para gráficas

## Backend
- C#
- ASP.NET Core Web API
- Clean Architecture
- Entity Framework Core
- SQLite
- Swagger/OpenAPI
- Dependency Injection
- async/await
- nullable reference types
- logging estructurado
- HttpClientFactory

## Machine Learning
- Python
- FastAPI
- pandas
- numpy
- scikit-learn
- xgboost
- lightgbm
- scipy
- statsmodels
- pydantic
- joblib

No usar deep learning al inicio salvo que exista evidencia objetiva de mejora.

## Base de datos
- SQLite

---

# 2. Arquitectura objetivo

```text
Next.js / React
      |
      v
ASP.NET Core API
      |
      +------------------> SQLite
      |
      +------------------> API-FOOTBALL
      |
      v
Python ML Service (FastAPI)
```

Reglas:

- El frontend nunca llama directamente a API-FOOTBALL.
- La API key vive únicamente en backend/server-side.
- API-FOOTBALL alimenta SQLite.
- El entrenamiento de ML se hace desde datos persistidos en SQLite.
- Python no debe consultar API-FOOTBALL directamente durante entrenamiento.
- C# es el orquestador principal.
- Python se usa para feature engineering, entrenamiento, inferencia, calibración, backtesting y simulaciones.

---

# 3. Estructura del repositorio

```text
SportsPredictor/
│
├── CLAUDE.md
├── README.md
├── .env.example
├── docker-compose.yml
│
├── frontend/
│   └── Next.js app
│
├── backend/
│   ├── SportsPredictor.Api/
│   ├── SportsPredictor.Application/
│   ├── SportsPredictor.Domain/
│   ├── SportsPredictor.Infrastructure/
│   └── SportsPredictor.sln
│
├── ml/
│   ├── app/
│   ├── models/
│   ├── training/
│   ├── features/
│   ├── evaluation/
│   ├── backtesting/
│   └── requirements.txt
│
├── database/
│   ├── migrations/
│   ├── seeds/
│   └── scripts/
│
├── tests/
│
└── docs/
    ├── architecture.md
    ├── api-football.md
    ├── football-model.md
    ├── nfl-model.md
    └── api.md
```

---

# 4. API-FOOTBALL / API-SPORTS

Proveedor principal de fútbol:

```text
https://v3.football.api-sports.io
```

Autenticación por header:

```text
x-apisports-key
```

Variable de entorno:

```text
API_FOOTBALL_KEY=
API_FOOTBALL_BASE_URL=https://v3.football.api-sports.io
```

El usuario dispone de plan PRO de API-FOOTBALL.

IMPORTANTE:
- No hardcodear límites del plan.
- Leer siempre límites y cuota restante desde headers de respuesta.
- Nunca registrar ni mostrar la API key.
- Nunca exponerla al frontend.

Headers que deben inspeccionarse cuando estén disponibles:

```text
x-ratelimit-requests-limit
x-ratelimit-requests-remaining
X-RateLimit-Limit
X-RateLimit-Remaining
```

---

# 5. Cliente API-Football

Ubicación sugerida:

```text
backend/
  SportsPredictor.Infrastructure/
    ExternalProviders/
      ApiFootball/
        ApiFootballClient.cs
        ApiFootballOptions.cs
        ApiFootballAuthHandler.cs
        ApiFootballRateLimitHandler.cs
        ApiFootballSyncService.cs
        Models/
        Mappings/
```

Requisitos:

- HttpClientFactory
- timeout
- CancellationToken
- retry limitado
- exponential backoff
- manejo de HTTP 429
- manejo de HTTP 5xx
- logging estructurado
- captura de headers de cuota
- pruebas unitarias con HttpMessageHandler falso/mocks
- smoke test real solo si API_FOOTBALL_KEY está configurada

Nunca hacer reintentos infinitos.

---

# 6. Cache

Implementar abstracción de cache.

Inicialmente:
- IMemoryCache

Preparar para:
- Redis

TTL sugeridos:

- Countries: 24h
- Leagues: 24h
- Teams: 24h
- Standings activas: 15m
- Standings históricas: 6h o permanente
- Fixtures futuros: 15-60m
- Fixtures terminados: persistencia permanente salvo correcciones
- Injuries: 30-60m
- Lineups cerca del kickoff: 5m
- Odds: 5-15m
- Predictions API-Football: 6h

---

# 7. Endpoints API-FOOTBALL por prioridad

## Prioridad 1
- /countries
- /leagues
- /teams
- /fixtures
- /standings

## Prioridad 2
- /fixtures/events
- /fixtures/statistics
- /fixtures/players
- /fixtures/lineups

## Prioridad 3
- /players
- /players/squads
- /injuries
- /coachs
- /transfers

## Prioridad 4
- /fixtures/headtohead
- /predictions
- /odds

No implementar todo de golpe.

---

# 8. Coverage-aware ingestion

La cobertura varía por liga/temporada/fixture.

Antes de solicitar datos avanzados:
- revisar coverage disponible;
- no asumir que todas las ligas tienen lineups, injuries, odds o player stats;
- registrar disponibilidad.

Entidad sugerida:

```text
DataAvailability
- Id
- FixtureId
- HasEvents
- HasLineups
- HasStatistics
- HasPlayerStatistics
- HasInjuries
- HasOdds
- HasPredictions
- CapturedAt
```

La ausencia de cobertura NO es un error.

---

# 9. Modelo de dominio mínimo

## Sports
- Id
- Name

## Competitions
- Id
- SportId
- ExternalApiFootballId
- Name
- Country
- CompetitionType

## Seasons
- Id
- CompetitionId
- Name
- StartDate
- EndDate

## Teams
- Id
- SportId
- ExternalApiFootballId
- Name
- Country

## Players
- Id
- TeamId
- ExternalApiFootballId
- Name
- Position
- BirthDate

## Venues
- Id
- ExternalApiFootballId
- Name
- City
- Country
- AltitudeMeters
- Latitude
- Longitude

## Matches
- Id
- ExternalApiFootballId
- CompetitionId
- SeasonId
- HomeTeamId
- AwayTeamId
- MatchDate
- Status
- HomeScore
- AwayScore
- VenueId

## OddsSnapshot
- Id
- MatchId
- Sportsbook
- Market
- Selection
- AmericanOdds
- DecimalOdds
- ImpliedProbability
- CapturedAt

## Predictions
- Id
- MatchId
- ModelVersionId
- PredictionDate
- Market
- Selection
- Probability
- ExpectedValue
- Recommended
- ActualOutcome
- IsCorrect

## ModelVersions
- Id
- ModelName
- Sport
- Version
- Algorithm
- TrainedAt
- TrainingStartDate
- TrainingEndDate
- BrierScore
- LogLoss
- Accuracy
- Roi
- ArtifactPath
- Active

## TrainingRuns
- Id
- ModelVersionId
- StartedAt
- FinishedAt
- DatasetSize
- TrainingParametersJson
- MetricsJson

## FeatureValue
- Id
- MatchId
- FeatureName
- NumericValue
- AvailableAt
- CapturedAt

Crear índices y claves únicas por ExternalApiFootballId cuando corresponda.

Nunca usar nombres de equipos como clave.

---

# 10. Standings snapshots

No guardar únicamente la tabla actual.

Entidad:

```text
StandingSnapshot
- Id
- CompetitionId
- SeasonId
- TeamId
- CapturedAt
- Rank
- Points
- Played
- Wins
- Draws
- Losses
- GoalsFor
- GoalsAgainst
- GoalDifference
- Form
```

Esto permite reconstruir cómo estaba la tabla antes de cada partido.

---

# 11. Lesiones y alineaciones como snapshots

## InjurySnapshot
- Id
- PlayerId
- TeamId
- MatchId nullable
- Type
- Reason
- StartDate
- ExpectedReturn
- CapturedAt
- Source

## LineupSnapshot
- Id
- MatchId
- TeamId
- Formation
- CoachId nullable
- CapturedAt

No sobrescribir historia; crear snapshots.

---

# 12. Raw responses y trazabilidad

Para endpoints críticos permitir almacenamiento opcional de payload crudo:

```text
ApiRawSnapshot
- Id
- Provider
- Endpoint
- ExternalEntityId
- CapturedAt
- PayloadJson
```

Todo dato importado debe conocer:
- SourceProvider
- ExternalId
- CapturedAt

---

# 13. Cuota y salud del proveedor

Entidad:

```text
ApiQuotaSnapshot
- Id
- Provider
- CapturedAt
- DailyLimit
- DailyRemaining
- MinuteLimit
- MinuteRemaining
```

Crear página:

```text
/settings/data-sources
```

Mostrar:
- API-Football Connected / Disconnected
- Daily requests remaining
- Minute requests remaining
- Last successful request
- Last error
- Last sync

Nunca mostrar la API key completa.

---

# 14. Sincronización

Jobs separados:

```text
FootballReferenceSyncJob
FootballFixturesSyncJob
FootballStandingsSyncJob
FootballResultsSyncJob
FootballStatisticsSyncJob
FootballLineupsSyncJob
FootballInjuriesSyncJob
FootballOddsSyncJob
FootballPredictionsSyncJob
```

Cada job debe:
- iniciar;
- detener;
- reanudar;
- registrar progreso;
- registrar requests consumidos;
- registrar errores.

No crear MegaSyncJob.

---

# 15. TrackedCompetition

Tabla:

```text
TrackedCompetition
- Id
- CompetitionId
- ExternalLeagueId
- Season
- Enabled
- Priority
- HistoricalSeasonsToImport
- SyncFixtures
- SyncStandings
- SyncPlayers
- SyncStatistics
- SyncInjuries
- SyncOdds
- SyncPredictions
```

Sirve para decidir en qué gastar cuota.

---

# 16. Importación histórica

Endpoint sugerido:

```text
POST /api/import/football/history
```

Body:

```json
{
  "leagueId": 39,
  "season": 2025
}
```

Flujo:
1. obtener liga;
2. obtener equipos;
3. obtener fixtures;
4. upsert idempotente;
5. para fixtures terminados, cargar extras según coverage;
6. guardar progreso;
7. poder reanudar;
8. no duplicar.

---

# 17. Sincronización incremental

Nunca volver a descargar toda la historia diariamente.

Reglas:
- histórico completo: no consultar salvo corrección;
- fixtures futuros: refrescar periódicamente;
- fixtures del día: mayor frecuencia;
- finalizados recientes: una última actualización;
- antiguos completos: no volver a consultar.

---

# 18. Dataset Builder

Flujo obligatorio:

```text
API-FOOTBALL
   |
   v
C# Ingestion
   |
   v
SQLite
   |
   v
Dataset Builder
   |
   v
Python ML
```

El entrenamiento debe ser reproducible únicamente desde SQLite.

---

# 19. Regla anti data-leakage

NUNCA usar información futura para predecir el pasado.

Incluye:
- estadísticas posteriores;
- standings calculadas después;
- odds capturadas después;
- lesiones conocidas después;
- alineaciones no disponibles todavía;
- features con AvailableAt posterior al kickoff.

Toda feature debe tener `AvailableAt` o ser calculable con datos disponibles antes del evento.

---

# 20. Football 1X2

Targets:
- Home
- Draw
- Away

Salida:

```json
{
  "home": 0.45,
  "draw": 0.29,
  "away": 0.26
}
```

Comparar:
- Logistic Regression
- XGBoost
- LightGBM

Seleccionar usando principalmente:
- Log Loss
- Brier Score
- Calibration

No seleccionar únicamente por Accuracy.

---

# 21. Expected Goals

Modelo independiente.

Inicialmente:
- Poisson

Después:
- Dixon-Coles

Generar:
- HomeExpectedGoals
- AwayExpectedGoals
- distribución de marcadores
- Over/Under
- BTTS
- score probabilities

---

# 22. Features de fútbol

Preparar arquitectura extensible para:

- Elo
- forma últimos 5/10
- goles
- xG/xGA cuando exista
- shots
- shots on target
- possession
- corners
- cards
- home/away performance
- injuries
- suspensions
- expected starting XI
- rest days
- travel distance
- altitude
- temperature
- humidity
- tournament stage
- must-win condition
- table position
- points needed
- goal-difference requirements
- coach
- formation
- bookmaker odds

Documentar cada feature.

---

# 23. API-Football predictions

Integrar `/predictions?fixture={fixtureId}` cuando la fase correspondiente llegue.

Tratarlo como modelo externo.

Guardar snapshot:

```text
ApiFootballPredictionSnapshot
- Id
- MatchId
- CapturedAt
- HomeProbability
- DrawProbability
- AwayProbability
- PredictedWinner
- PredictedScore
- RawJson
```

Comparar:
- Nuestro modelo
- API-Football
- Bookmaker sin vig

No usarlo como verdad.

---

# 24. Odds

Guardar snapshots; nunca sobrescribir.

Objetivo:
- Opening
- Intermediate
- Closing

Cuando sea posible capturar:
- T-72h
- T-24h
- T-6h
- T-1h
- T-15m

Si API-Football no permite recuperar historia antigua de odds, nuestra base local será el histórico permanente.

---

# 25. Expected Value

Conversión:
- American odds
- Decimal odds
- Implied probability

Eliminar vig cuando el mercado sea mutuamente excluyente.

EV:

```text
EV = model_probability * decimal_odds - 1
```

Categorías:
- Strong Value
- Value
- Neutral
- Negative Value

No recomendar solo porque un equipo sea favorito.

---

# 26. Calibration y backtesting

Implementar:
- reliability diagrams
- calibration curves
- isotonic regression
- Platt scaling cuando aplique

Backtesting:
- walk-forward
- rolling windows
- NUNCA random split para series temporales

Guardar:
- Accuracy
- Precision
- Recall
- F1
- Log Loss
- Brier Score
- Calibration Error
- ROI
- Yield
- Win Rate
- Max Drawdown
- Total Bets
- Average Odds

---

# 27. Benchmarks obligatorios

Comparar contra:
1. elegir siempre favorito;
2. Elo;
3. probabilidades implícitas del bookmaker;
4. closing line cuando exista;
5. API-Football predictions cuando exista.

Si el modelo no supera benchmarks, no afirmar que tiene ventaja.

---

# 28. Model Registry

Cada entrenamiento crea una nueva versión.

Ejemplos:
- football_1x2_v001
- football_1x2_v002
- football_xg_v001

Nunca sobrescribir versiones anteriores.

---

# 29. Prediction snapshots

Toda predicción se guarda antes del partido.

Una nueva ejecución crea un nuevo snapshot.

Nunca modificar retroactivamente una predicción después de conocer el resultado.

---

# 30. Progol optimizer

Entrada:
- partidos
- probabilidades L/E/V
- presupuesto
- dobles permitidos
- triples permitidos

Salida:
- combinación optimizada
- probabilidad estimada de aciertos
- escenarios Safe / Balanced / Aggressive / Contrarian

---

# 31. NFL / Protouch

No implementar todavía.

Arquitectura separada.

NFL requerirá:
- ExpectedPoints
- ExpectedMargin
- TotalPoints

Protouch:
- L = local gana por más de 6
- D = diferencia máxima de 6
- V = visitante gana por más de 6

No mezclar modelos NFL con fútbol.

---

# 32. Dashboard

Páginas objetivo:

```text
/dashboard
/matches
/matches/[id]
/predictions
/models
/models/[id]
/training
/backtesting
/value-bets
/progol
/nfl
/protouch
/settings
/settings/data-sources
/data/import
```

---

# 33. Calidad de código

Obligatorio:
- SOLID
- Clean Architecture
- strongly typed code
- DTOs
- validation
- exception middleware
- structured logging
- unit tests
- integration tests
- dependency injection
- async APIs
- CancellationToken

No God classes.
No controllers gigantes.
No pseudocódigo como implementación final.

---

# 34. Testing mínimo

Tests para:
- odds conversion
- probability normalization
- EV
- Poisson
- Progol optimizer
- API-Football mapping
- API quota parsing
- retry 429
- cache
- idempotent imports
- model serialization
- feature availability / anti leakage

---

# 35. Seguridad

Aplicación local inicialmente.

No autenticación compleja todavía.

Secretos solo en environment variables / user secrets.

Archivos:
- .env.example
- appsettings.example.json

Nunca commitear secrets.

---

# 36. Local-first

Flujo principal, sin Docker:

```text
dotnet run
npm run dev
uvicorn app.main:app
```

SQLite es un archivo local (p. ej. `database/sportspredictor.db`); no requiere
un servidor de base de datos independiente ni Docker.

Opcionalmente, si más adelante se desea, también debe poder ejecutarse con:

```text
docker compose up
```

pero esto no es un requisito para el desarrollo local.

---

# 37. No inventar datos

Nunca introducir estadísticas deportivas ficticias para llenar UI.

Si falta información:
- mostrar "No data available"

Test data solo en tests y claramente marcada.

---

# 38. Fases

## Fase 1
Skeleton:
- Clean Architecture C#
- Next.js
- FastAPI
- SQLite (archivo local, sin servidor de base de datos)
- health endpoints
- configuración
- tests mínimos
- docs architecture

## Fase 2
Modelo de dominio + EF Core + migraciones.

## Fase 3
API-Football base:
- options
- typed client
- auth
- quota parsing
- retry
- cache
- health
- /countries
- /leagues

## Fase 4
Reference data:
- teams
- venues
- seasons
- tracked competitions

## Fase 5
Historical ingestion:
- fixtures
- results
- standings snapshots
- events
- statistics
- players
- lineups

## Fase 6
Incremental sync:
- future fixtures
- completed fixtures
- injuries
- lineups
- standings

## Fase 7
Odds snapshots + API-Football predictions.

## Fase 8
Dataset Builder.

## Fase 9
Football 1X2 baseline.

## Fase 10
Calibration + backtesting + benchmarks.

## Fase 11
Goals / Poisson / Dixon-Coles.

## Fase 12
Expected Value.

## Fase 13
Football dashboard.

## Fase 14
Progol optimizer.

## Fase 15
NFL.

## Fase 16
Protouch.

## Fase 17
News/context/weather.

## Fase 18
Automated retraining/model registry.

---

# 39. Regla para Claude Code

Antes de cada fase:
1. leer CLAUDE.md;
2. inspeccionar repositorio;
3. explicar brevemente plan;
4. identificar conflictos;
5. implementar solo esa fase;
6. compilar;
7. ejecutar tests;
8. corregir;
9. documentar;
10. detenerse.

No avanzar de fase sin instrucción explícita.

---

# 40. Primera tarea

Implementar SOLO Fase 1.

No implementar todavía API-Football.

La arquitectura sí debe quedar lista para que Fase 3 agregue el proveedor.

Al terminar:
- mostrar estructura final;
- comandos de ejecución;
- tests ejecutados;
- decisiones arquitectónicas;
- detenerse y esperar Fase 2.

---

# 41. Adición fuera del plan original: análisis "experto" (SHAP + Kelly + narrativa)

No formaba parte de las 18 fases originales de este documento. Se agregó
portando el agente de análisis de un proyecto Python/Next.js paralelo
(predicción 1X2 + LangGraph + Claude) sobre esta base, respetando las reglas
de arquitectura ya establecidas arriba (secciones 1, 8-9, 29):

- `ml/features/shap_explain.py`, `ml/decision/staking.py`,
  `ml/narrative/generate.py` + `POST /analyze/football-1x2` en `ml/app/main.py`
  — igual que `/predict/football-1x2`, **enteramente stateless**: nunca toca
  `sportspredictor.db` ni API-FOOTBALL; recibe features/odds/contexto del
  partido ya resueltos por C#.
- `PredictionAnalysisService` (Infrastructure) orquesta: Dataset Builder →
  ML service → persiste `PredictionExplanation` (nueva entidad, snapshot
  inmutable igual que `Prediction` — sección 29). Expuesto en
  `PredictionsController` como `POST /api/predictions/{matchId}/analyze` y
  `GET /api/predictions/{matchId}/analysis`.
- **Cuotas de mercado**: se reutiliza `OddsSnapshot` (ya sincronizada desde
  API-FOOTBALL, Fase 7) en vez de integrar un segundo proveedor de cuotas
  (el proyecto origen usaba The Odds API) — cero credenciales nuevas, una
  sola fuente de verdad para odds.
- **Narrativa**: usa `ANTHROPIC_API_KEY` (nueva variable de entorno, opcional
  — sin ella cae a una narrativa por plantilla). Nunca se usa para llamar a
  API-FOOTBALL ni ningún otro proveedor de datos deportivos — solo para
  redactar el análisis a partir de los datos que el backend ya calculó.

Ver el README ("Análisis 'experto'") para el detalle de uso.
