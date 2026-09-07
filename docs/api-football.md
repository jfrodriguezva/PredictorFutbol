# Integración API-Football — Fases 3 a 7

Fase 3: `GET /countries` y `GET /leagues` (solo lectura, sin persistir en el
dominio). Fase 4: `GET /teams` + gestión de `TrackedCompetition`, que sí
persiste `Competition`, `Season`, `Team` y `Venue`. Fase 5: `GET /fixtures`
(persiste `Match`) y `GET /standings` (persiste `StandingSnapshot`,
append-only) — cierra toda la Prioridad 1. Fase 6: refresco incremental de
fixtures, `GET /injuries` (persiste `InjurySnapshot`) y
`GET /fixtures/lineups` (persiste `LineupSnapshot`). Fase 7: `GET /odds`
(persiste `OddsSnapshot`, ya existía desde Fase 2 pero nunca se había
llenado) y `GET /predictions` (persiste `ApiFootballPredictionSnapshot`,
nueva entidad). Sigue sin haber events/statistics/players (Prioridad 2/3
restante de `CLAUDE.md` sección 7).

## Configuración

La API key **solo** se lee de la variable de entorno `API_FOOTBALL_KEY` —
nunca de `appsettings.json`, nunca hardcodeada
([DependencyInjection.cs](../backend/SportsPredictor.Infrastructure/DependencyInjection.cs)
usa `PostConfigure` para forzar esto). `ApiFootball:BaseUrl` sí viene de
`appsettings.json` (no es secreto).

```bash
# PowerShell, en la sesión donde corras `dotnet run` (no la pegues en ningún archivo)
$env:API_FOOTBALL_KEY = "tu_clave_real"
cd backend
dotnet run --project SportsPredictor.Api
```

Sin la variable configurada, el backend arranca y responde normalmente;
únicamente las rutas que necesitan llamar al proveedor devuelven
`503 Service Unavailable` con un mensaje claro (nunca un 500 ni un crash).
Verificado en este entorno (sin key configurada):

- `GET /api/data-sources/api-football/status` → `200`, `"connected": false`.
- `GET /api/reference/countries` → `503`, `"API-Football not configured"`.
- `POST /api/data-sources/api-football/test` → `503`, mismo mensaje.

## Endpoints propios

| Ruta | Método | Descripción |
|---|---|---|
| `/api/data-sources/api-football/status` | GET | Estado de conexión + última cuota conocida (persistida). Nunca llama al proveedor. |
| `/api/data-sources/api-football/test` | POST | Llama de verdad a `GET /countries` para probar conectividad y refrescar la cuota. |
| `/api/reference/countries` | GET | Países, cacheados 24h. |
| `/api/reference/leagues` | GET | Ligas (con su temporada actual), cacheadas 24h. |
| `/api/tracked-competitions` | GET | Lista las competiciones que se están siguiendo. |
| `/api/tracked-competitions` | POST | Da de alta una competición por `externalLeagueId` + `season` (Fase 4, ver abajo). |
| `/api/tracked-competitions/{id}/enabled` | PATCH | Habilita/deshabilita el tracking (body: `true`/`false`). |
| `/api/tracked-competitions/{id}/sync-teams` | POST | Trae equipos+venues de esa competición desde API-Football y hace upsert. |
| `/api/tracked-competitions/{id}/sync-fixtures` | POST | Trae fixtures+resultados de esa competición y hace upsert de `Match` (Fase 5). |
| `/api/tracked-competitions/{id}/sync-standings` | POST | Trae la tabla actual y **agrega** un `StandingSnapshot` nuevo por equipo conocido (nunca sobrescribe) (Fase 5). |
| `/api/tracked-competitions/{id}/refresh-fixtures?upcoming=N&recent=M` | POST | Incremental: solo los próximos N fixtures y los últimos M jugados (Fase 6). |
| `/api/tracked-competitions/{id}/sync-injuries` | POST | Trae injuries de la competición y **agrega** un `InjurySnapshot` por reporte (Fase 6). |
| `/api/matches/{id}/sync-lineups` | POST | Trae las alineaciones de ese partido y **agrega** un `LineupSnapshot` por equipo (Fase 6). |
| `/api/matches/{id}/sync-odds` | POST | Trae odds de casas de apuestas para ese partido y **agrega** un `OddsSnapshot` por cada (bookmaker, mercado, selección) (Fase 7). |
| `/api/matches/{id}/sync-prediction` | POST | Trae la predicción propia de API-Football y **agrega** un `ApiFootballPredictionSnapshot` (Fase 7, nunca tratado como verdad). |

## Pipeline HTTP (`HttpClientFactory`)

```text
HttpClient("ApiFootballClient")
  → ApiFootballAuthHandler       (agrega x-apisports-key; lanza ApiFootballNotConfiguredException si falta la key, sin llegar a red)
  → ApiFootballRateLimitHandler  (lee headers de cuota, reintenta 429/5xx con backoff acotado, persiste ApiQuotaSnapshot)
  → SocketsHttpHandler real
```

- **Reintentos**: hasta `ApiFootball:MaxRetries` (default 3) para `429` y
  `500/502/503/504`. Usa `Retry-After` si el proveedor lo manda; si no,
  backoff exponencial 1s/2s/4s/... tope 30s. **Nunca infinito** — al agotar
  los reintentos se devuelve la última respuesta fallida tal cual.
- **Cuota**: cada respuesta final (éxito o fallo tras agotar reintentos) se
  inspecciona por `x-ratelimit-requests-limit/remaining` (diario) y
  `X-RateLimit-Limit/Remaining` (por minuto) y se guarda como
  `ApiQuotaSnapshot`. Si el proveedor no manda esos headers, no se escribe
  fila (no se inventan números). Un fallo al persistir la cuota se loguea
  pero **nunca** rompe la llamada real.
- **Timeout**: `ApiFootball:TimeoutSeconds` (default 30s) por request.

## Cache

`ICacheService` (Application) / `MemoryCacheService` (Infrastructure, sobre
`IMemoryCache`) — abstracción lista para cambiar a Redis después sin tocar
consumidores. TTL de esta fase: Countries y Leagues, 24h (según sección 6 de
`CLAUDE.md`). `POST /test` **bypassa** el cache a propósito: una prueba de
conexión que devuelve un resultado cacheado no probaría nada.

## DTOs — advertencia importante

Los modelos crudos
([Models/ApiFootballEnvelope.cs](../backend/SportsPredictor.Infrastructure/ExternalProviders/ApiFootball/Models/ApiFootballEnvelope.cs),
`ApiFootballCountryModel.cs`, `ApiFootballLeagueEntryModel.cs`) se basaron en
la documentación pública de API-Football v3, **no en una respuesta real** —
no había `API_FOOTBALL_KEY` disponible en este entorno de desarrollo. La
regla de `CLAUDE.md` ("inspeccionar respuestas reales antes de finalizar los
DTOs") no se pudo cumplir al 100% en esta fase.

**Acción pendiente para el usuario**: en cuanto configures tu
`API_FOOTBALL_KEY` localmente, corre `POST /api/data-sources/api-football/test`
y `GET /api/reference/countries` / `/leagues`, y si el JSON real difiere del
esperado (nombres de campos, tipos, campos ausentes), avisa para ajustar los
DTOs — no lo demos por verificado hasta entonces.

## Pruebas

- Unitarias con `HttpMessageHandler` falso (`FakeHttpMessageHandler`):
  autenticación, reintentos acotados (429, 5xx, backoff, no-retry en 4xx no
  reintentable), persistencia de cuota, mapeo countries/leagues, cache-hit
  sin segunda llamada HTTP, y los estados sin key configurada.
- **Smoke test real**: no ejecutado en este entorno (`API_FOOTBALL_KEY` no
  configurada). Es opcional y solo aplica cuando la variable está presente,
  por diseño (`ApiFootballAuthHandler` lo impide de otra forma).

---

## Fase 4 — Reference data: teams, venues, seasons, tracked competitions

No hubo un `PHASE_4_PROMPT.txt` explícito para esta fase (a diferencia de 1-3);
el alcance se derivó directamente de `CLAUDE.md` sección 38 ("Fase 4:
teams, venues, seasons, tracked competitions") y sección 9 (entidades ya
existentes desde la Fase 2 — `TrackedCompetition`, `Team`, `Venue`, `Season`,
`Competition` no necesitaron cambios de modelo).

### Flujo de alta de una competición

`POST /api/tracked-competitions` con `{ "externalLeagueId": 39, "season": 2024 }`:

1. Si ya existe un `TrackedCompetition` con ese `(ExternalLeagueId, Season)`,
   lo devuelve tal cual (idempotente, no duplica).
2. Si no, llama a `GET /leagues?id={externalLeagueId}` en API-Football.
3. Upsert de `Competition` por `ExternalApiFootballId` (crea o actualiza
   nombre/país/tipo).
4. Upsert de cada `Season` que venga en la respuesta — **solo** las que
   traen `start`/`end` (el dominio los exige como `DateOnly` no nulos; una
   temporada sin fechas se omite en vez de inventarlas). El nombre de la
   temporada se deriva de las fechas reales (`"2024"` si `start`/`end` caen
   en el mismo año calendario, `"2024/2025"` si cruzan de año) — no se
   asume que toda temporada abarca dos años.
5. Crea la fila `TrackedCompetition` con las banderas `Sync*` del request
   (todas opcionales salvo `SyncFixtures`/`SyncStandings`, que por defecto
   vienen en `true` porque son las primeras que se van a necesitar en
   Fase 5).

### Sync de equipos

`POST /api/tracked-competitions/{id}/sync-teams` llama a
`GET /teams?league={externalLeagueId}&season={season}` y hace upsert de
`Team` (por `ExternalApiFootballId`) y, cuando el equipo trae un venue con
`id` y `name` no vacíos, de `Venue` también (por su propio
`ExternalApiFootballId`). Un venue sin id o sin nombre se **omite** — no se
inventa un nombre de venue ("Unknown" o similar) para no violar la regla
`#37` de `CLAUDE.md`. Nótese que el dominio (definido en Fase 2, sección 9
de `CLAUDE.md`) no tiene una FK directa `Team → Venue`; el venue solo queda
enlazable más adelante vía `Match.VenueId`.

Correr `sync-teams` dos veces sobre la misma competición no duplica nada
(`FootballReferenceSyncServiceTests.SyncTeamsAsync_CalledTwice_DoesNotDuplicateTeams`).

### Por qué no hay job en background todavía

`CLAUDE.md` sección 5/14 sugiere un `FootballReferenceSyncJob` con
start/stop/resume/progreso. Esta fase lo implementa como un servicio
invocado on-demand por HTTP (`FootballReferenceSyncService`,
`IFootballReferenceService`) sin esa infraestructura de jobs — no hay
todavía ningún proceso de larga duración que la necesite (eso llega con la
importación histórica de fixtures en Fase 5). Se documenta como decisión
deliberada, no como omisión accidental.

### Pruebas nuevas

`FootballReferenceSyncServiceTests` (con SQLite en memoria + HTTP simulado):
alta de competición nueva, idempotencia en alta repetida, sync de
equipos/venues, no-duplicación en sync repetido, `NotFoundException` sobre
un `TrackedCompetition` inexistente, y toggle de `Enabled`.

Mismo caveat de Fase 3: el DTO de `/teams`
(`Models/ApiFootballTeamEntryModel.cs`) está basado en la documentación
pública, no verificado contra una respuesta real.

---

## Fase 5 — Historical ingestion (alcance: Prioridad 1 completa)

`CLAUDE.md` sección 38 define la Fase 5 completa como: fixtures, results,
standings snapshots, events, statistics, players, lineups — siete tipos de
datos distintos, cada uno con su propio job según la sección 14 ("No crear
MegaSyncJob"). Implementarlos todos de una vez habría contradicho esa regla
y habría sido una sesión desproporcionadamente grande. Se le preguntó al
usuario y confirmó acotar la primera iteración a **fixtures + results**;
**standings** se agregó después en la misma fase (cierra así toda la
Prioridad 1 de la sección 7 de `CLAUDE.md`: countries, leagues, teams,
fixtures, standings). `events`, `statistics`, `players` y `lineups`
(Prioridad 2/3) quedan para iteraciones futuras, cada uno como su propio job.

### `FootballFixtureSyncService` (`IFootballFixtureSyncService`)

`POST /api/tracked-competitions/{id}/sync-fixtures`:

1. Verifica que el `TrackedCompetition` exista (`404` si no).
2. Busca localmente la `Season` cuyo `StartDate.Year` coincide con
   `TrackedCompetition.Season` (esa fila ya se creó en la Fase 4, al dar de
   alta la competición). Si por algún motivo no existe, los fixtures de esa
   pasada se **saltan** (contados en `SkippedNoSeasonMatch`) en vez de
   dejar `Match.SeasonId` vacío o inventar una temporada.
3. Llama a `GET /fixtures?league={id}&season={year}` — para una sola
   temporada de una sola liga, API-Football devuelve todos los partidos en
   una sola llamada (no pagina), así que no hizo falta lógica de
   reanudación/paginación en este alcance.
4. Para cada fixture: upsert de `Match` por `ExternalApiFootballId`
   (idempotente); si el equipo local o visitante no existe todavía
   (`ExternalApiFootballId` desconocido), se crea una versión mínima
   (id + nombre, sin país — los fixtures no traen esa info) en vez de
   fallar; si el venue trae `id` y `name`, se hace upsert igual que en
   Fase 4, si no, se omite (`Match.VenueId` queda `null`).
5. El estado (`fixture.status.short` de API-Football: `NS`, `1H`, `FT`,
   `PST`, `CANC`, etc.) se traduce a nuestro enum `MatchStatus`
   (`Scheduled`/`InProgress`/`Finished`/`Postponed`/`Cancelled`).
   `HomeScore`/`AwayScore` vienen del objeto `goals` — `null` mientras el
   partido no se ha jugado, exactamente los valores que trae el proveedor
   en ese momento (no hay una llamada separada de "solo resultados": para
   este alcance, un fixture terminado ya trae su marcador en la misma
   respuesta).

**Bug real encontrado y corregido durante el desarrollo**: al procesar
varios fixtures de la misma respuesta en un solo lote (una `SaveChangesAsync`
al final), un equipo nuevo creado por el primer fixture no se encontraba al
procesar un segundo fixture que también lo referenciaba, porque la
búsqueda solo consultaba la base de datos, no las entidades ya trackeadas
en memoria pero aún no guardadas — resultaba en `UNIQUE constraint failed`
al intentar insertar el mismo equipo dos veces. Se corrigió revisando
primero `DbSet<T>.Local` antes de ir a la base de datos, tanto para
equipos como para venues y matches. Cubierto por
`SyncFixturesAsync_UpsertsMatchesAndAutoCreatesTeamsAndVenue`.

### Por qué "results" no es un job separado en este alcance

CLAUDE.md nombra `FootballFixturesSyncJob` y `FootballResultsSyncJob` por
separado, pensados para la Fase 6 de sincronización incremental (donde
"resultados recién terminados" se refrescan con más frecuencia que fixtures
futuros). En esta Fase 5 (importación histórica, no incremental) ambos
conceptos colapsan en una sola llamada porque una única consulta a
`/fixtures?league&season` ya trae el estado y marcador actuales de todos
los partidos, jugados o no. La separación real entre "sync de fixtures
futuros" y "sync de resultados recién terminados" es una decisión de
Fase 6, no de esta.

### Pruebas nuevas

`FootballFixtureSyncServiceTests`: upsert de fixtures con auto-creación de
equipos/venues, mapeo correcto de estado y marcador (partido terminado vs.
no empezado), no-duplicación en sync repetido (ni de fixtures ni de
equipos), y `NotFoundException` sobre un `TrackedCompetition` inexistente.

Mismo caveat de Fases 3 y 4: el DTO de `/fixtures`
(`Models/ApiFootballFixtureEntryModel.cs`) está basado en la documentación
pública, no verificado contra una respuesta real.

### `FootballStandingsSyncService`

`POST /api/tracked-competitions/{id}/sync-standings` llama a
`GET /standings?league={id}&season={year}` y crea un `StandingSnapshot`
**nuevo** por cada fila de la tabla cuyo equipo ya se conoce localmente
(vía `sync-teams`). Una fila de un equipo desconocido se cuenta en
`SkippedUnknownTeam` y se omite — no se crea un equipo mínimo aquí, a
diferencia de `sync-fixtures`, porque una fila de standings no trae
suficiente contexto adicional para justificarlo (sí lo hace un fixture, que
además necesita el equipo para el partido en sí).

`standings` en la respuesta de API-Football es un arreglo de grupos (normal
tener un solo grupo para una liga simple, varios para fases de grupos); el
servicio los aplana todos en el mismo snapshot.

Llamar `sync-standings` dos veces seguidas **no** sobrescribe nada — cada
llamada agrega filas nuevas con un `CapturedAt` distinto, exactamente el
comportamiento de snapshot que pide la sección 10 de `CLAUDE.md`
("reconstruir cómo estaba la tabla antes de cada partido"). Cubierto por
`SyncStandingsAsync_CalledTwice_AppendsNewSnapshotsInsteadOfOverwriting`.

Mismo caveat de DTOs no verificados: `Models/ApiFootballStandingsResponseModel.cs`.

---

## Fase 6 — Incremental sync: fixtures, injuries, lineups

`CLAUDE.md` sección 38 define la Fase 6 como: future fixtures, completed
fixtures, injuries, lineups, standings. `standings` ya quedó cubierto en
Fase 5 (es append-only por diseño, cada llamada ya es "incremental" en el
sentido de que nunca reescribe historia). Esta fase agrega los tres
restantes: refresco incremental de fixtures, injuries y lineups. El usuario
pidió explícitamente el alcance completo (no acotado), incluyendo las
entidades `InjurySnapshot`/`LineupSnapshot` que se habían diferido desde la
Fase 2.

### Refresco incremental de fixtures (`RefreshRecentAndUpcomingFixturesAsync`)

`POST /api/tracked-competitions/{id}/refresh-fixtures?upcoming=10&recent=10`
— a diferencia de `sync-fixtures` (Fase 5, trae **toda** la temporada),
esto llama a:

- `GET /fixtures?league={id}&next={upcoming}` — los próximos N partidos
  (de cualquier temporada; el filtro `next` de API-Football no requiere
  `season`).
- `GET /fixtures?league={id}&last={recent}` — los últimos M ya jugados.

Ambas respuestas se procesan con el mismo upsert que `sync-fixtures`
(reutilizado vía un método privado compartido, `ProcessAndSaveAsync`).
`upcoming=0` o `recent=0` se saltan esa llamada por completo (no se pide
`next=0`/`last=0` a la API). Esto es lo que hace "incremental" real: fixtures
antiguos ya completados nunca se vuelven a tocar (regla de la sección 17 de
`CLAUDE.md`: "antiguos completos: no volver a consultar"), a diferencia de
`sync-fixtures` que siempre trae la temporada entera.

**Caveat documentado**: `next`/`last` no filtran por temporada, así que en
teoría un fixture muy cerca de un cambio de temporada podría no coincidir
con la `Season` local de la `TrackedCompetition` (se salta, contado en
`SkippedNoSeasonMatch`, igual que en Fase 5) — en la práctica esto solo
pasa en el borde exacto de dos temporadas.

### `FootballInjurySyncService`

`POST /api/tracked-competitions/{id}/sync-injuries` llama a
`GET /injuries?league={id}&season={year}` y **agrega** un `InjurySnapshot`
por cada reporte (nunca sobrescribe, sección 11 de `CLAUDE.md`). Si el
equipo o el jugador referenciado no existe todavía localmente, se crea un
stub mínimo (mismo patrón que los equipos en `sync-fixtures`) — un jugador
stub no tiene posición ni fecha de nacimiento hasta que `/players`
(Prioridad 3) se implemente. Si el reporte trae un `fixture.id` que
coincide con un `Match` ya sincronizado, `InjurySnapshot.MatchId` se
enlaza; si no, queda `null` (cubierto por
`SyncInjuriesAsync_LinksMatchWhenFixtureKnown_NullWhenNot`).

**Importante**: API-Football's `/injuries` no reporta fecha de inicio ni
retorno esperado — `InjurySnapshot.StartDate`/`ExpectedReturn` quedan
siempre `null` desde esta vía de ingesta (ver docs/database-schema.md).

### `FootballLineupSyncService`

`POST /api/matches/{id}/sync-lineups` opera sobre un **Match** (no una
`TrackedCompetition`) porque `/fixtures/lineups` de API-Football es
por-fixture. Llama a `GET /fixtures/lineups?fixture={externalId}` y agrega
un `LineupSnapshot` por cada equipo presente en la respuesta (normalmente
2). Un equipo de la alineación que no existe localmente se **salta**
(a diferencia de injuries/fixtures, aquí no se justifica crear un stub —
ambos equipos del partido ya deberían existir de `sync-fixtures`; si no
existen, algo más está mal). Sin entidad `Coach` local, se guarda
`ExternalCoachId`/`CoachName` crudos (ver decisión en
docs/database-schema.md).

### Pruebas nuevas

`FootballInjurySyncServiceTests`, `FootballLineupSyncServiceTests`, y dos
tests nuevos en `FootballFixtureSyncServiceTests` para el refresco
incremental (verifican que se llama `next=`/`last=` correctamente y que
`upcomingCount`/`recentCount` en 0 salta esa llamada). 50/50 tests totales
en la solución tras esta fase.

Mismo caveat de DTOs no verificados: `Models/ApiFootballInjuryEntryModel.cs`,
`Models/ApiFootballLineupEntryModel.cs`.

---

## Fase 7 — Odds snapshots + API-Football predictions

Ambos endpoints son per-fixture (igual que lineups en Fase 6), así que sus
rutas viven en `MatchesController` junto a `sync-lineups`.

### `FootballOddsSyncService`

`POST /api/matches/{id}/sync-odds` llama a `GET /odds?fixture={externalId}`.
La respuesta real de API-Football anida `bookmakers[] → bets[] → values[]`;
el servicio aplana esos tres niveles en filas `OddsSnapshot` (una por
combinación bookmaker+mercado+selección). Un valor de odd que no parsea
como decimal positivo se **salta** (no se inventa). Cada llamada agrega
snapshots nuevos — nunca sobrescribe, para poder reconstruir
opening/intermediate/closing lines (sección 24 de `CLAUDE.md`). Cubierto por
`SyncOddsAsync_CalledTwice_AppendsInsteadOfOverwriting`.

`AmericanOdds` queda `null` e `ImpliedProbability` es la probabilidad
implícita **cruda** (sin quitar vig) — ver decisiones en
docs/database-schema.md; ambas conversiones son trabajo explícito de la
Fase 12.

### `FootballPredictionSyncService`

`POST /api/matches/{id}/sync-prediction` llama a
`GET /predictions?fixture={externalId}` y guarda un
`ApiFootballPredictionSnapshot` — tratado siempre como comparación externa,
**nunca** como verdad (sección 23 de `CLAUDE.md`; esto se hará cumplir de
verdad cuando exista lógica de comparación de modelos en fases de ML). Los
tres porcentajes (`home`/`draw`/`away`, reportados como texto tipo `"45%"`)
deben parsear todos; si falta uno, no se guarda nada (cubierto por
`SyncPredictionAsync_MissingPercentages_DoesNotSaveSnapshot`). El campo
`PredictedScore` en realidad guarda el texto de `predictions.advice` —
API-Football no da un marcador exacto predicho, solo consejo textual y
porcentajes; se documenta esta discrepancia con el nombre del campo en vez
de inventar un marcador. Se guarda además el JSON crudo completo
(`RawJson`) para trazabilidad, sin necesidad de una tabla `ApiRawSnapshot`
separada para este caso específico.

### Pruebas nuevas

`FootballOddsSyncServiceTests`, `FootballPredictionSyncServiceTests`.
57/57 tests totales en la solución tras esta fase.

Mismo caveat de DTOs no verificados: `Models/ApiFootballOddsResponseModel.cs`,
`Models/ApiFootballPredictionResponseModel.cs`.
