# Arquitectura — Fases 1 a 10

> Fase 2 (dominio + EF Core + SQLite) agregó persistencia real, extendida en
> Fase 6 con `InjurySnapshot`/`LineupSnapshot` y en Fase 7 con
> `ApiFootballPredictionSnapshot` — ver
> [docs/database-schema.md](database-schema.md). Fases 3-7 agregaron la
> integración con API-Football (countries/leagues, teams/venues +
> `TrackedCompetition`, fixtures/results/standings, refresco incremental +
> injuries/lineups, y odds/predictions) — ver
> [docs/api-football.md](api-football.md). Fases 8-10 agregaron el Dataset
> Builder, el primer entrenamiento real en Python (1X2 baseline), y
> calibración/backtesting/benchmarks — ver
> [docs/football-model.md](football-model.md).

## Visión general

```text
Next.js / React  --->  ASP.NET Core API  --->  SQLite (archivo local)
                              |
                              +--------------> API-FOOTBALL (countries, leagues, teams,
                              |                 fixtures, standings, injuries, lineups,
                              |                 odds, predictions)
                              v
                     Python ML Service (FastAPI)
```

Fase 1 dejó el esqueleto (servicios arrancan, exponen `/health`, el frontend
lee ese health check). Fase 2 agregó el modelo de dominio completo (17
entidades) y persistencia real vía EF Core + SQLite, con migraciones. Fase 3
agregó el cliente HTTP de API-Football (auth, retries acotados, cuota, cache)
limitado a `/countries` y `/leagues`. Fase 4 agregó `/teams` y el flujo de
"trackear una competición" (`TrackedCompetition`), que además persiste
`Competition`/`Season`/`Team`/`Venue` por primera vez con datos reales del
proveedor. Fase 5 agregó `/fixtures` (persiste `Match`) y `/standings`
(persiste `StandingSnapshot`, append-only) — cierra toda la Prioridad 1 de
la sección 7 de `CLAUDE.md`. Fase 6 agregó sincronización incremental de
fixtures (solo próximos/recientes, no la temporada entera) más `/injuries` y
`/fixtures/lineups`, con sus entidades `InjurySnapshot`/`LineupSnapshot`
(diferidas desde la Fase 2). Fase 7 agregó `/odds` (llena `OddsSnapshot`,
existente desde Fase 2 pero vacío hasta ahora) y `/predictions` (nueva
entidad `ApiFootballPredictionSnapshot`, tratada siempre como benchmark
externo, nunca como verdad). Fase 8 agregó el **Dataset Builder**: calcula
y persiste `FeatureValue` (anti-leakage garantizado — ver
docs/football-model.md) y expone `GET /api/dataset-builder/{id}/export`,
el único canal por el que el servicio Python (`ml/`) consume datos —
Python nunca abre `sportspredictor.db` directamente. Fase 9 agregó el
**primer entrenamiento real**: `ml/training/football_1x2.py` compara
Logistic Regression/XGBoost/LightGBM y selecciona por Log Loss/Brier Score
(nunca por Accuracy), expuesto vía `POST /train/football-1x2` en el
servicio Python; el backend C# (`ModelTrainingService`) orquesta el flujo
completo (exporta el dataset, llama a Python, registra `ModelVersion` +
`TrainingRun`) — Python nunca escribe en SQLite, mismo principio que en
Fase 8. El resto de endpoints de API-Football (events, statistics, players)
sigue sin implementar a propósito, por fases.

## Componentes

### backend/ (Clean Architecture, .NET 10)

- **SportsPredictor.Domain** — entidades y reglas de negocio puras: `Entity`
  (clase base), las 17 entidades de la Fase 2 (`Sport`, `Competition`,
  `Season`, `Team`, `Player`, `Venue`, `Match`, `StandingSnapshot`,
  `OddsSnapshot`, `ModelVersion`, `Prediction`, `TrainingRun`,
  `FeatureValue`, `TrackedCompetition`, `DataAvailability`,
  `ApiQuotaSnapshot`, `ApiRawSnapshot`), las 2 de Fase 6
  (`InjurySnapshot`, `LineupSnapshot`) y la de Fase 7
  (`ApiFootballPredictionSnapshot`) — 20 en total — más sus enums. Sin
  dependencias a otros proyectos.
- **SportsPredictor.Application** — casos de uso, interfaces, DTOs. Depende
  solo de Domain. Contiene `IDateTimeProvider` como ejemplo de abstracción y
  el método de extensión `AddApplication()` para el registro de DI.
- **SportsPredictor.Infrastructure** — implementaciones concretas. Contiene
  `SystemDateTimeProvider`, `SportsPredictorDbContext` + configuraciones
  Fluent API + migraciones EF Core (SQLite) (detalle en
  [docs/database-schema.md](database-schema.md)), `MemoryCacheService`
  (`ICacheService`), y en `ExternalProviders/ApiFootball/` el cliente HTTP
  completo (options, auth handler, rate-limit/retry handler, cliente
  tipado, DTOs, mapper) — detalle en
  [docs/api-football.md](api-football.md) — y en `Datasets/`
  (`DatasetBuilderService`, Fase 8), `MlService/` (`MlServiceClient`, cliente
  HTTP hacia el servicio Python) y `ModelRegistry/` (`ModelTrainingService`,
  Fase 9) — detalle en [docs/football-model.md](football-model.md). Depende
  de Application y Domain.
- **SportsPredictor.Api** — ASP.NET Core Web API con controllers, Swagger/OpenAPI,
  middleware de manejo de excepciones (respuestas `application/problem+json`),
  logging estructurado (consola, con scopes y timestamp), CORS configurado
  hacia el frontend, y el endpoint `GET /health`.

Referencias: `Api -> Application, Infrastructure, Domain`;
`Infrastructure -> Application, Domain`; `Application -> Domain`; `Domain` no depende de nada.

### frontend/ (Next.js 16 + TypeScript + Tailwind CSS)

App Router, `src/` directory. Página principal consulta `GET /health` del
backend (`src/lib/health.ts`) y muestra el estado o "No data available" si el
backend no está disponible — nunca se inventan datos deportivos ficticios.
Recharts está instalado como dependencia para las gráficas de fases futuras.

### ml/ (Python 3.12 + FastAPI)

`/health` (Fase 1) y `POST /train/football-1x2` (Fase 9): entrena y compara
Logistic Regression/XGBoost/LightGBM (`training/football_1x2.py`), lee
datasets solo vía el CSV que le entrega el backend C#
(`training/dataset.py`) — nunca consulta API-FOOTBALL ni abre
`sportspredictor.db` directamente, conforme a la regla del `CLAUDE.md` y a
la decisión de las Fases 8/9. Detalle en
[docs/football-model.md](football-model.md). `evaluation/` y
`backtesting/` siguen vacías, preparadas para la Fase 10.

### database/

`sportspredictor.db` (SQLite, gitignored) vive aquí. `migrations/` contiene
un export de solo lectura del DDL (ver [docs/database-schema.md](database-schema.md));
las migraciones EF Core reales están en
`backend/SportsPredictor.Infrastructure/Persistence/Migrations/`. `seeds/` y
`scripts/` siguen vacías, preparadas para fases futuras.

### docker-compose.yml (opcional)

No se usa en el flujo de trabajo actual (desarrollo local sin Docker ni Git).
Se deja documentado como alternativa: orquesta `api`, `ml` y `frontend`
(SQLite vive en un volumen del contenedor `api`). Ninguna imagen embebe
secretos: las credenciales se inyectan vía variables de entorno (`.env`, no
commiteado).

## Decisiones técnicas de Fase 1

1. **TFM `net10.0`** en todos los proyectos .NET — el SDK/runtime 10 (LTS)
   estaba instalado y ofrece la ventana de soporte más larga disponible
   localmente (frente a 8.0, cuyo soporte LTS termina antes).
2. **Python 3.12** como intérprete del servicio ML, aislado en su propio
   virtualenv (`ml/.venv`), aunque la máquina también tenga 3.13/3.14
   instalados — mejor compatibilidad probada con el stack
   numpy/scikit-learn/xgboost/lightgbm.
3. **Formato de solución `.sln` clásico** en vez del nuevo `.slnx` (default
   del SDK 10) para respetar exactamente `backend/SportsPredictor.sln` como
   define `CLAUDE.md`.
4. **Logging estructurado sin Serilog** — se usa el logger integrado de
   ASP.NET Core con `AddSimpleConsole` (scopes + timestamp) para mantener
   Fase 1 mínima; Serilog/sinks se puede añadir en una fase posterior si se
   necesita structured JSON logging real hacia un sink externo.
5. **`agentRules: false`** en `next.config.ts` — Next.js 16 regenera
   automáticamente `CLAUDE.md`/`AGENTS.md` dentro de `frontend/` en cada
   build/dev; se desactiva para no generar un `CLAUDE.md` duplicado que
   entre en conflicto con el `CLAUDE.md` maestro del repo.
6. **SQLite en vez de SQL Server** (decisión explícita del usuario, aplicada
   también en `CLAUDE.md`) — no se quiere depender de Docker ni de instalar
   SQL Server localmente. SQLite es un archivo (`database/sportspredictor.db`)
   sin servidor. Trade-off documentado: algunas features T-SQL-specific
   (p. ej. ciertos tipos de dato o funciones) no estarán disponibles; se
   evaluará más adelante si hace falta migrar a un motor con servidor.
7. **Ni Docker ni Git se usan en este entorno de desarrollo** — todo corre
   directo (`dotnet run`, `npm run dev`, `uvicorn`). `docker-compose.yml` se
   mantiene solo como referencia opcional para quien lo quiera usar después.

Decisiones específicas de Fase 2 (modelo de dominio, tipos, constraints, por
qué no hay capa de repositorios todavía, etc.) están en
[docs/database-schema.md](database-schema.md); las de Fase 3 (pipeline de
handlers, manejo de cuota/retries, por qué los DTOs no están verificados
contra una respuesta real todavía) están en
[docs/api-football.md](api-football.md) — no se repiten aquí.

## Pendiente para fases futuras (no implementado a propósito)

- Cache Redis para `ICacheService` (la abstracción ya existe; sigue sobre
  `IMemoryCache`).
- Prioridad 2/3 restante de la sección 7 de `CLAUDE.md` (events,
  statistics, players) y `/coachs` (necesario para una entidad `Coach`
  propia en vez del `ExternalCoachId` crudo de `LineupSnapshot`) — cada
  uno su propio job futuro, per sección 14 ("no MegaSyncJob").
- Conversión decimal↔americano de odds y remoción de vig (`OddsSnapshot`
  ya tiene los datos crudos desde Fase 7; el cálculo es Fase 12).
- Orquestación de jobs en background (start/stop/resume/progreso real,
  sección 14) — todo el sync sigue siendo on-demand por HTTP; Fase 6
  introdujo semántica incremental (qué re-sincronizar) pero no un
  scheduler que decida cuándo.
- Verificación de los DTOs de API-Football contra una respuesta real (no
  había `API_FOOTBALL_KEY` disponible en este entorno) — incluye los DTOs
  de `/teams` (Fase 4), `/fixtures`/`/standings` (Fase 5),
  `/injuries`/`/fixtures/lineups` (Fase 6) y `/odds`/`/predictions` (Fase 7).
- Resto del catálogo de features de la sección 22 de `CLAUDE.md` (Elo,
  xG/xGA, shots, possession, corners, cards, rest days, travel, clima,
  altitud, must-win, coach, formación como feature categórica real) — la
  arquitectura del Dataset Builder ya es extensible para agregarlas
  (docs/football-model.md).
- Calibración completa (reliability diagrams, isotonic regression, Platt
  scaling) y benchmarks (favorito, Elo, bookmaker, closing line) — Fase 9
  solo compara por Log Loss/Brier Score; eso es explícitamente la Fase 10.
- Activación de `ModelVersion.Active` (endpoint dedicado) — las versiones
  se crean con `Active=false`; promover una a activa queda para cuando
  exista lógica de comparación/calibración que lo justifique.
- Prueba end-to-end real del flujo Fase 9 con ambos servicios corriendo y
  una `TrackedCompetition` con partidos reales — no hay datos reales
  todavía; la orquestación está cubierta por tests con dobles de prueba.
