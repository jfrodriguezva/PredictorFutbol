from fastapi import APIRouter, HTTPException
from pydantic import BaseModel

from app.inference import FixtureNotFoundError, predict_fixture

router = APIRouter()


class PredictRequest(BaseModel):
    fixture_id: int


@router.post("/predict")
def predict(payload: PredictRequest) -> dict:
    try:
        return predict_fixture(payload.fixture_id)
    except FixtureNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except FileNotFoundError as exc:
        raise HTTPException(
            status_code=503,
            detail="Modelo no entrenado todavía. Corre scripts.train_model primero.",
        ) from exc
