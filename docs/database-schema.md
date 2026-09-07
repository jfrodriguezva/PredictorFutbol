# Esquema de base de datos — Fases 2, 6 y 7

Motor: **SQLite** (archivo local `database/sportspredictor.db`), vía EF Core
(`Microsoft.EntityFrameworkCore.Sqlite`). Ver [[project-stack-decisions]] /
`docs/architecture.md` para el porqué de SQLite en vez de SQL Server.

Las migraciones EF Core "reales" (compiladas, versionadas) viven en
`backend/SportsPredictor.Infrastructure/Persistence/Migrations/`. El archivo
`database/migrations/20260820220703_InitialCreate.sql` es un export de solo
lectura (`dotnet ef migrations script`) para poder revisar el DDL sin abrir
C# — no es la fuente de verdad ni se aplica manualmente.

> Fase 6 agregó `InjurySnapshot` y `LineupSnapshot` (definidas en `CLAUDE.md`
> sección 9/11 desde la Fase 2, pero deliberadamente fuera de alcance hasta
> que la sincronización incremental las necesitó). Migración
> `AddInjuryAndLineupSnapshots`. Fase 7 agregó `ApiFootballPredictionSnapshot`
> (sección 23 de `CLAUDE.md`, nunca listada en las 17 originales de Fase 2 —
> es una entidad de comparación externa, no parte del modelo base). Migración
> `AddApiFootballPredictionSnapshot`. `OddsSnapshot` ya existía desde la
> Fase 2; Fase 7 fue la primera vez que se llenó con datos reales.

## Tablas (20)

| Tabla | Relaciones (FK, `ON DELETE RESTRICT` en todas) | Índices propios |
|---|---|---|
| `Sports` | — | `Name` único |
| `Competitions` | `SportId → Sports` | `ExternalApiFootballId` único filtrado (`WHERE NOT NULL`) |
| `Seasons` | `CompetitionId → Competitions` | `(CompetitionId, Name)` único |
| `Teams` | `SportId → Sports` | `ExternalApiFootballId` único filtrado |
| `Players` | `TeamId → Teams` | `ExternalApiFootballId` único filtrado |
| `Venues` | — | `ExternalApiFootballId` único filtrado |
| `Matches` | `CompetitionId`, `SeasonId`, `HomeTeamId → Teams`, `AwayTeamId → Teams`, `VenueId?` | `ExternalApiFootballId` único filtrado; `MatchDate`; `(SeasonId, CompetitionId)`; **CHECK** `HomeTeamId <> AwayTeamId` |
| `StandingSnapshots` | `CompetitionId`, `SeasonId`, `TeamId` | `(SeasonId, TeamId, CapturedAt)` — reconstruye la tabla antes de cualquier fecha |
| `OddsSnapshots` | `MatchId → Matches` | `(MatchId, Sportsbook, Market, CapturedAt)` |
| `ModelVersions` | — | `(ModelName, Version)` único |
| `Predictions` | `MatchId`, `ModelVersionId` | `(MatchId, ModelVersionId, Market, PredictionDate)` |
| `TrainingRuns` | `ModelVersionId` | `ModelVersionId` |
| `FeatureValues` | `MatchId` | `(MatchId, FeatureName, CapturedAt)`; `AvailableAt` (guardia anti-leakage) |
| `TrackedCompetitions` | `CompetitionId` | `(CompetitionId, Season)` único; `ExternalLeagueId` |
| `DataAvailabilities` | `MatchId` | `MatchId` único (1 registro de cobertura por partido) |
| `ApiQuotaSnapshots` | — | `(Provider, CapturedAt)` |
| `ApiRawSnapshots` | — | `(Provider, Endpoint, ExternalEntityId, CapturedAt)` |
| `InjurySnapshots` | `PlayerId → Players`, `TeamId → Teams`, `MatchId?` | `(PlayerId, CapturedAt)` |
| `LineupSnapshots` | `MatchId → Matches`, `TeamId → Teams` | `(MatchId, TeamId, CapturedAt)` |
| `ApiFootballPredictionSnapshots` | `MatchId → Matches` | `(MatchId, CapturedAt)` |

Todas las PK son `Guid` (`TEXT` en SQLite), heredadas de `Entity.Id`.

## Seed data

Un único registro de referencia: `Sports` → `Football`
(`Id = 00000000-0000-0000-0000-000000000001`, expuesto como
`SportConfiguration.FootballId` para referenciarlo desde código sin
"magic strings"). No se sembraron ligas/equipos/partidos — serían datos
deportivos ficticios, prohibido por la regla `#37` de `CLAUDE.md`.

## Decisiones técnicas de Fase 2

1. **PKs `Guid`** — consistente con `Entity.Id` ya establecido en Fase 1.
2. **`DateOnly` para fechas puras** (`Season.StartDate/EndDate`,
   `Player.BirthDate`) y **`DateTime` (UTC) para instantes** (`MatchDate`,
   todos los `CapturedAt`, `PredictionDate`, `TrainedAt`, etc.) — separa
   "fecha calendario" de "momento exacto", relevante para la regla
   anti-data-leakage (`FeatureValue.AvailableAt` debe compararse como
   instante absoluto contra `Match.MatchDate`, cubierto por
   `FeatureValueTests.FeatureValue_AvailableAt_RoundTripsAsUtc`).
3. **Enums como `string` en la base** (`CompetitionType`, `PlayerPosition`,
   `MatchStatus`) vía `HasConversion<string>()` — más legible para
   inspección manual de la base SQLite que un `int`, a costa de unos bytes
   extra; irrelevante en un archivo local.
4. **`OddsSnapshot.DecimalOdds` sin `HasColumnType` explícito** — dejar el
   mapeo por defecto de EF Core Sqlite para `decimal` (columna `TEXT`).
   Declarar `decimal(10,4)` (sintaxis de SQL Server) le da afinidad
   `NUMERIC` en SQLite, que auto-convierte texto numérico a `REAL`/`INTEGER`
   y puede perder precisión — exactamente lo que `decimal` buscaba evitar.
5. **`Match` con `CHECK (HomeTeamId <> AwayTeamId)`** — único constraint de
   integridad añadido más allá de lo listado explícitamente en `CLAUDE.md`;
   se justifica como corrección de una relación explícita (regla de
   Fase 2) y se prueba en `MatchConstraintTests`.
6. **Índices únicos filtrados** (`WHERE "ExternalApiFootballId" IS NOT NULL`)
   en `Competition`, `Team`, `Player`, `Venue`, `Match` — permiten múltiples
   filas sin id externo (aún no ingeridas desde API-Football) sin romper la
   unicidad de los ids que sí existen.
7. **Sin capa de repositorios** — `SportsPredictorDbContext` se inyecta
   directamente donde se necesite. No hay todavía casos de uso reales que
   consuman el dominio (solo el health check), así que una capa de
   repositorios genérica sería abstracción prematura; se introducirá en la
   fase donde exista un consumidor concreto (sync jobs / dataset builder).
8. **`InjurySnapshot` y `LineupSnapshot` (sección 11 de `CLAUDE.md`)
   deliberadamente NO implementados en Fase 2** — no estaban en la lista
   explícita de `PHASE_2_PROMPT.txt`. Añadidos en la Fase 6 (ver abajo).

## Decisiones técnicas de Fase 6

1. **`InjurySnapshot.StartDate`/`ExpectedReturn` quedan `null`** al ingerir
   desde API-Football — el endpoint real `/injuries` no reporta esas
   fechas (solo `type`/`reason` anidados en el jugador); se dejan
   nullable en vez de inventarlas, listas para cuando exista otra fuente
   que sí las traiga.
2. **`LineupSnapshot.CoachId` (nullable) → `ExternalCoachId` (int?) +
   `CoachName`**, no una FK a un `Coach` local — no existe entidad `Coach`
   todavía (`/coachs` es Prioridad 3, sin implementar); se guarda el id/
   nombre crudo del proveedor como placeholder documentado hasta que se
   introduzca `Coach` como entidad propia.
3. **`Player` también se crea como stub mínimo** (igual que `Team` desde
   Fase 5) cuando una injury referencia un jugador no sincronizado
   todavía vía `/players` (Prioridad 3, no implementado) — id + nombre,
   sin posición ni fecha de nacimiento.

## Decisiones técnicas de Fase 7

1. **`OddsSnapshot.AmericanOdds` queda `null`** al ingerir desde
   API-Football — el proveedor solo reporta odds decimales; convertir a
   formato americano es explícitamente trabajo de la Fase 12 (Expected
   Value), no de la ingesta.
2. **`OddsSnapshot.ImpliedProbability` es la probabilidad implícita cruda**
   (`1 / odds_decimal`), **sin quitar el vig** — quitar el margen de la casa
   en un mercado mutuamente excluyente es, de nuevo, trabajo explícito de la
   Fase 12 (sección 25 de `CLAUDE.md`).
3. **`ApiFootballPredictionSnapshot.PredictedScore` guarda el texto de
   `predictions.advice`**, no un marcador exacto — el endpoint real de
   API-Football no devuelve un "marcador predicho" limpio, solo un consejo
   textual y porcentajes de resultado; guardar ese texto tal cual es más
   honesto que inventar un campo estructurado que la API no provee.
4. **Una predicción solo se guarda si los tres porcentajes
   (home/draw/away) están presentes y son parseables** — si falta alguno,
   no se crea el snapshot (no se rellenan huecos con `null` parcial en
   campos `required`).

## Cómo regenerar / inspeccionar

```bash
cd backend
dotnet ef database update --project SportsPredictor.Infrastructure --startup-project SportsPredictor.Api
dotnet ef migrations add <Nombre> --project SportsPredictor.Infrastructure --startup-project SportsPredictor.Api --output-dir Persistence/Migrations
```

El archivo `database/sportspredictor.db` está en `.gitignore` (dato local,
no código fuente).
