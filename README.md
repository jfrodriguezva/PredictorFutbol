# SportsPredictor

Plataforma local de análisis y predicción deportiva (fútbol primero), según
la especificación en [CLAUDE.md](CLAUDE.md). Arquitectura:
**WPF (lanzador) → Next.js (dashboard) → API C# → Servicio ML Python**, con
SQLite como única base de datos local.

## Contenido

- `CLAUDE.md` — especificación maestra.
- `.env.example` — variables de entorno sin secretos.
- `PHASE_1_PROMPT.txt` / `PHASE_2_PROMPT.txt` / `PHASE_3_API_FOOTBALL_PROMPT.txt` — prompts históricos de las primeras 3 fases.
- `backend/` — API C# (Clean Architecture), `frontend/` — dashboard Next.js, `ml/` — servicio Python, `desktop/` — lanzador WPF, `installer/` — empaquetado.

## API key

No colocar la API key real en ningún archivo. Configúrala como variable de
entorno `API_FOOTBALL_KEY` en tu sesión de terminal (o de usuario en Windows
si usas el lanzador WPF/instalador) — nunca en un archivo commiteado. Tu plan
es PRO, pero el software lee los límites reales desde los headers de
API-Football, nunca los hardcodea.

### Validación de los DTOs contra una respuesta real — hecho

Corrido contra API-Football real (plan PRO) sobre las 15 competiciones con
temporada activa. Resultado por endpoint:

- `GET /api/reference/countries`, `GET /api/reference/leagues` — OK, datos
  reales, sin discrepancias.
- `POST /api/tracked-competitions/{id}/sync-teams` (vía `sync-fixtures`,
  que crea equipos nuevos al vuelo) — OK.
- `POST /api/tracked-competitions/{id}/refresh-fixtures` — OK, ~421
  fixtures reales sincronizados sin error en 15 competiciones.
- `POST /api/matches/{id}/sync-odds` — OK, probado en 2,995 partidos reales
  (285 Scheduled + 2,710 Finished) sin un solo error de parseo.
- `POST /api/matches/{id}/sync-prediction` — OK, 285 partidos reales.
- `POST /api/tracked-competitions/{id}/sync-standings` — OK.
- `POST /api/matches/{id}/sync-lineups` — OK (devuelve 0 snapshots para
  partidos lejanos en el tiempo, como se espera — las alineaciones recién
  se publican cerca del kickoff).
- **`POST /api/tracked-competitions/{id}/sync-injuries` — bug real
  encontrado**: funciona con ligas chicas/sin lesiones activas (ej. Liga MX,
  0 resultados), pero falla con `500`/`TaskCanceledException` en ligas
  grandes con volumen real de lesiones (ej. Premier League) — el DTO en sí
  parsea bien (no es un problema de forma de los datos), es
  `FootballInjurySyncService.GetOrCreateMinimalPlayerAsync` haciendo un
  lookup EF individual por jugador/equipo en un loop secuencial (patrón
  N+1) que no escala al volumen real de una liga grande. **No arreglado
  todavía** — requiere batchear los lookups (ej. cargar todos los
  equipos/jugadores relevantes en un diccionario antes del loop, en vez de
  una query por item).

## Estado del proyecto

**Fases 1-14 completadas** (de las 18 de `CLAUDE.md`; ver excepciones abajo):
skeleton → dominio/persistencia SQLite → integración API-Football completa
(countries/leagues/teams/fixtures/standings/injuries/lineups/odds/predictions,
con sync incremental) → Dataset Builder → modelo 1X2 (Logistic
Regression/XGBoost/LightGBM) → calibración/backtesting/benchmarks → Expected
Goals (Poisson/Dixon-Coles) → Expected Value → dashboard Next.js →
optimizador de Progol.

**Fases 15-16 (NFL, Protouch) NO implementadas a propósito** — `CLAUDE.md`
sección 31 dice explícitamente "No implementar todavía", y esa regla se
respeta aunque se haya pedido terminar el proyecto completo.

**Fases 17-18 (news/weather/contexto, reentrenamiento automatizado) fuera de
alcance de esta pasada** — son mejoras incrementales de largo plazo, no
bloquean el flujo principal de "generar una predicción real", que es lo que
sí quedó funcionando de punta a punta.

Además del plan de `CLAUDE.md`, se agregó (a pedido explícito, fuera de la
especificación original):
- **Lanzador de escritorio WPF** (`desktop/`) que arranca los tres servicios
  en orden y muestra el dashboard embebido — ver [desktop/README.md](desktop/README.md).
- **Instalador** (`installer/`) para empaquetar todo el stack en cualquier
  equipo Windows — ver [installer/README.md](installer/README.md).
- Puertos fijos: API C# `20050`, Next.js `20051`, servicio ML `8001`.
- **Endpoint de análisis "experto"** (`POST /api/predictions/{matchId}/analyze`,
  `GET /api/predictions/{matchId}/analysis`) — ver la sección siguiente.
- **Activar una versión de modelo** (`POST /api/model-versions/{id}/activate`) —
  fuerza qué `ModelVersion` usan `/generate` y `/analyze`; sin activar
  ninguna, se sigue usando "la más reciente entrenada" (comportamiento
  anterior, sin cambios).
- **Scheduler mínimo + notificaciones in-app**: `ValueBetWatcherBackgroundService`
  corre cada 15 min, genera predicciones para partidos `Scheduled` (próximos
  7 días) de competiciones trackeadas que aún no tengan una, y publica un
  value bet detectado en `GET /api/notifications/value-bets` (campana 🔔 en
  el navbar del dashboard). **No sincroniza fixtures/odds automáticamente**
  — solo reacciona a partidos ya presentes en SQLite; el "MegaSyncJob"
  completo (sección 14 de `CLAUDE.md`) sigue fuera de alcance a propósito.

Ver [docs/architecture.md](docs/architecture.md),
[docs/database-schema.md](docs/database-schema.md),
[docs/api-football.md](docs/api-football.md) y
[docs/football-model.md](docs/football-model.md) para el detalle fase por fase.

**El modelo de fútbol 1X2 se siguió desarrollando más allá de la Fase 10**
(multi-liga, Elo, más features, ensemble de algoritmos) en sesiones
posteriores. Ver [docs/model-journey.md](docs/model-journey.md) para el
historial completo de qué se probó, qué mejoró la accuracy y qué no, y qué
falta — léelo primero si estás retomando el proyecto en una sesión nueva
(de Claude o de quien sea).

## Análisis "experto" (SHAP + Kelly + narrativa)

Además de la predicción 1X2 cruda (`POST /api/predictions/generate/{matchId}`),
el backend expone un endpoint de análisis más completo:

- `POST /api/predictions/{matchId}/analyze` — genera y persiste un nuevo
  snapshot de análisis.
- `GET /api/predictions/{matchId}/analysis` — devuelve el análisis más
  reciente ya generado (404 si nunca se llamó a `analyze`).

Cada análisis incluye:
- **Explicabilidad SHAP**: las 5 features que más empujaron la predicción
  hacia la clase ganadora (`ml/features/shap_explain.py`).
- **Stake sugerido (Kelly fraccionado)**: cuando hay cuotas de mercado
  disponibles para el partido (`ml/decision/staking.py`), tope del 5% del
  bankroll por apuesta.
- **Narrativa en tono de experto**: generada con Claude (`claude-opus-5`,
  con la tool `web_search` para contexto reciente en partidos aún no
  jugados) — o una narrativa por plantilla si no hay `ANTHROPIC_API_KEY`
  configurado (`ml/narrative/generate.py`).

Este endpoint respeta las reglas de arquitectura de `CLAUDE.md`: el servicio
Python (`ml/`) sigue siendo enteramente **stateless** — nunca toca
`sportspredictor.db` ni API-FOOTBALL directamente. El backend C#
(`PredictionAnalysisService`) es quien calcula las features (Dataset
Builder), resuelve las cuotas ya sincronizadas (`OddsSnapshot`, vía
`/api/matches/{id}/sync-odds` — se reutiliza la integración con
API-Football existente en vez de añadir un segundo proveedor de cuotas) y
persiste el resultado (`PredictionExplanation`).

## Cómo ejecutar

### Opción 1 — todo junto, vía el lanzador WPF (recomendado)

Ver [desktop/README.md](desktop/README.md): copia
`launcher.settings.local.json.example` a `launcher.settings.local.json`,
ajusta las rutas a tu checkout, y corre `SportsPredictor.Desktop`. Arranca
Python, la API y Next.js en orden y abre el dashboard embebido.

### Opción 2 — cada servicio por separado

#### Backend (.NET API)

```bash
cd backend
dotnet build SportsPredictor.sln
dotnet test SportsPredictor.sln
dotnet ef database update --project SportsPredictor.Infrastructure --startup-project SportsPredictor.Api
dotnet run --project SportsPredictor.Api
```

`dotnet ef database update` ya no es estrictamente necesario para primer uso —
la API aplica migraciones pendientes automáticamente al arrancar — pero sigue
siendo útil para aplicar migraciones sin levantar la API. La API expone
`GET /health` en `http://localhost:20050`.

#### Frontend (Next.js)

```bash
cd frontend
npm install
cp .env.example .env.local
npm run dev
```

Corre en `http://localhost:20051` (puerto fijado en `package.json`).

#### Servicio ML (FastAPI)

```bash
cd ml
py -3.12 -m venv .venv
./.venv/Scripts/pip install -r requirements.txt
./.venv/Scripts/python -m pytest
./.venv/Scripts/python -m uvicorn app.main:app --port 8001
```

Para que el endpoint de análisis genere narrativa con Claude (en vez de la
plantilla de respaldo), configura `ANTHROPIC_API_KEY` como variable de
entorno antes de arrancar `ml`.

No se necesita Docker ni un servidor de base de datos: SQLite es un archivo
local.

### Instalador para otro equipo

Ver [installer/README.md](installer/README.md) — `installer/build-release.ps1`
genera los binarios publicados, y `installer/SportsPredictor.iss` (Inno
Setup) produce el `.exe` instalable.

### Docker Compose (opcional, no usado en este entorno)

```bash
docker compose up --build
```

Queda documentado como alternativa para quien prefiera containerizar los
servicios más adelante, pero no es parte del flujo de trabajo actual.
