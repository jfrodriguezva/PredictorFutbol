# Predictor 1X2 de Fútbol

Producto web que analiza un partido de fútbol seleccionado por el usuario y genera una predicción
de resultado (Local / Empate / Visitante) usando un modelo de Machine Learning entrenado con datos
históricos, comparándola contra cuotas reales de mercado para detectar *value bets*, y presentando
el análisis con el tono de un experto en apuestas deportivas.

**Aviso de juego responsable**: esta herramienta es informativa. No garantiza resultados. Apostar
implica riesgo financiero — juega con responsabilidad.

## Arquitectura

```
apps/
  web/         Next.js (frontend + BFF) — interfaz de usuario, consume POST /analysis
  ml-service/  FastAPI (Python) — ingesta, features, entrenamiento, inferencia y agente de análisis
```

- **Datos de partidos/equipos**: API-Football (api-sports.io directo, no RapidAPI).
- **Modelo**: LightGBM multiclase calibrado (isotonic), con explicabilidad **SHAP** por predicción.
- **Cuotas de mercado**: The Odds API (Fase 6).
- **Decisión de stake**: Kelly Criterion fraccionado sobre el edge modelo-vs-mercado.
- **Narrativa "experto en apuestas"**: agente **LangGraph** (`predict → explain → odds →
  [stake] → narrative`) que llama a Claude (`claude-opus-5`) con la tool nativa `web_search`
  para contexto reciente en partidos aún no jugados. Todo vive en `ml-service`; el frontend solo
  consume `POST /analysis`.
- **Base de datos**: **SQLite** (`apps/ml-service/data/predictor.db`) — decisión permanente del
  proyecto, sin migración a Postgres planeada.

## Setup local

### ml-service (FastAPI)
```
cd apps/ml-service
py -3.12 -m venv .venv
./.venv/Scripts/pip install -r requirements.txt
copy ..\..\.env.example .env   # completar API_FOOTBALL_KEY, ODDS_API_KEY
./.venv/Scripts/uvicorn app.main:app --reload --port 8000
```

### web (Next.js)
```
cd apps/web
npm install --ignore-scripts
copy ..\..\.env.example .env.local   # completar ANTHROPIC_API_KEY, ML_SERVICE_URL
npm run dev
```

## Fases del proyecto

- [x] **Fase 0** — Scaffolding del monorepo
- [x] **Fase 1** — Ingesta de datos (API-Football): ligas, equipos, fixtures históricos
- [x] **Fase 2** — Esquema de base de datos (leagues, teams, fixtures, stats, elo, odds, predictions)
- [x] **Fase 3** — Feature engineering + dataset histórico de entrenamiento
- [x] **Fase 4** — Entrenamiento y evaluación del modelo ML (LightGBM + calibración; ROI pendiente de odds reales)
- [x] **Fase 5** — Servicio de inferencia (`POST /predict`)
- [~] **Fase 6** — Odds + value bets: cliente y cálculo de Kelly listos y validados con datos simulados; falta `ODDS_API_KEY` real para activarlo en producción
- [x] **Fase 7** — Frontend + agente LangGraph (SHAP, staking, narrativa vía Claude con `web_search`); narrativa en modo respaldo hasta tener `ANTHROPIC_API_KEY`
- [ ] **Fase 8** — Tracking de desempeño histórico del predictor (accuracy/ROI)
- [ ] **Fase 9** — Testing, manejo de errores y deployment (Vercel + Railway/Render + Neon)

Detalle completo de cada fase (esquemas de tablas, contratos de API JSON, criterios de "hecho"):
ver el plan de arquitectura del proyecto.
