from __future__ import annotations

import joblib
import pandas as pd
from sqlalchemy.orm import Session

from app.db.models import Fixture, Prediction
from app.db.session import get_session
from app.features.engineering import FeatureBuilder
from app.features.schema import CATEGORICAL, FEATURE_COLUMNS

MODEL_PATH = "data/model_v1.joblib"

_model_bundle: dict | None = None


class FixtureNotFoundError(ValueError):
    pass


def load_model() -> dict:
    global _model_bundle
    if _model_bundle is None:
        _model_bundle = joblib.load(MODEL_PATH)
    return _model_bundle


def _features_up_to(session: Session, target_fixture: Fixture) -> dict:
    """Reconstruye el estado (Elo, forma, H2H, tabla) usando solo partidos FT
    anteriores al kickoff del fixture objetivo, y calcula sus features."""
    history = (
        session.query(Fixture)
        .filter(Fixture.status == "FT", Fixture.kickoff_at < target_fixture.kickoff_at)
        .order_by(Fixture.kickoff_at.asc())
        .all()
    )
    builder = FeatureBuilder()
    for fx in history:
        builder.update(fx)
    return builder.features_for(target_fixture)


def _features_summary(features: dict) -> dict:
    def _round(value: float | None, ndigits: int = 2) -> float | None:
        return round(value, ndigits) if value is not None else None

    return {
        "elo_home": _round(features["elo_home"], 1),
        "elo_away": _round(features["elo_away"], 1),
        "form_pts_home_last5": features["form_pts_home"],
        "form_pts_away_last5": features["form_pts_away"],
        "avg_goals_scored_home": _round(features["avg_goals_scored_home"]),
        "avg_goals_scored_away": _round(features["avg_goals_scored_away"]),
        "h2h_home_wins": features["h2h_home_wins"],
        "h2h_draws": features["h2h_draws"],
        "h2h_away_wins": features["h2h_away_wins"],
        "rest_days_home": features["rest_days_home"],
        "rest_days_away": features["rest_days_away"],
        "ppg_home": _round(features["ppg_home"]),
        "ppg_away": _round(features["ppg_away"]),
    }


def compute_prediction(fixture_id: int) -> dict:
    """Corre el modelo para un fixture y devuelve todo lo que necesitan tanto el
    endpoint /predict (solo `response`) como el agente de análisis (`fixture` ORM,
    `raw_features` para SHAP, `predicted_label_index`)."""
    bundle = load_model()
    session = get_session()
    fixture = session.get(Fixture, fixture_id)
    if fixture is None:
        raise FixtureNotFoundError(f"Fixture {fixture_id} no existe en la base de datos")

    features = _features_up_to(session, fixture)

    row = {col: features.get(col) for col in FEATURE_COLUMNS}
    X = pd.DataFrame([row])
    for col in CATEGORICAL:
        X[col] = X[col].astype("category")

    probs = bundle["model"].predict_proba(X)[0]
    prob_by_label = dict(zip(bundle["label_names"], probs.tolist()))
    predicted_label_index = int(probs.argmax())

    prediction_row = Prediction(
        fixture_id=fixture.id,
        model_version=bundle["version"],
        prob_home=prob_by_label["home"],
        prob_draw=prob_by_label["draw"],
        prob_away=prob_by_label["away"],
    )
    session.add(prediction_row)
    session.commit()

    response = {
        "fixture_id": fixture.id,
        "model_version": bundle["version"],
        "probabilities": {
            "home": round(prob_by_label["home"], 4),
            "draw": round(prob_by_label["draw"], 4),
            "away": round(prob_by_label["away"], 4),
        },
        "features_summary": _features_summary(features),
    }

    # Se arma el resumen del fixture (incluyendo relaciones) mientras la sesión
    # sigue activa, para no arrastrar el objeto ORM fuera de este scope: acceder a
    # `fixture.league` después de que esta función retorne puede fallar con
    # DetachedInstanceError si la sesión ya fue recolectada por el GC.
    fixture_summary = {
        "fixture_id": fixture.id,
        "league_id": fixture.league.api_football_id,
        "league_name": fixture.league.name,
        "country": fixture.league.country,
        "season": fixture.league.season,
        "home_team": fixture.home_team.name,
        "away_team": fixture.away_team.name,
        "kickoff_at": fixture.kickoff_at.isoformat(),
        "status": fixture.status,
        "venue": fixture.venue,
    }

    return {
        "fixture_summary": fixture_summary,
        "response": response,
        "raw_features": features,
        "predicted_label_index": predicted_label_index,
    }


def predict_fixture(fixture_id: int) -> dict:
    return compute_prediction(fixture_id)["response"]
