from fastapi import APIRouter, HTTPException

from app.agent.analysis_graph import run_analysis
from app.api.predict import PredictRequest
from app.inference import FixtureNotFoundError

router = APIRouter()


@router.post("/analysis")
def analysis(payload: PredictRequest) -> dict:
    try:
        return run_analysis(payload.fixture_id)
    except FixtureNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except FileNotFoundError as exc:
        raise HTTPException(
            status_code=503,
            detail="Modelo no entrenado todavía. Corre scripts.train_model primero.",
        ) from exc
