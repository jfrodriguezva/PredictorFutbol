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


def _load_model() -> dict:
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


def predict_fixture(fixture_id: int) -> dict:
    bundle = _load_model()
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

    prediction = Prediction(
        fixture_id=fixture.id,
        model_version=bundle["version"],
        prob_home=prob_by_label["home"],
        prob_draw=prob_by_label["draw"],
        prob_away=prob_by_label["away"],
    )
    session.add(prediction)
    session.commit()

    def _round(value: float | None, ndigits: int = 2) -> float | None:
        return round(value, ndigits) if value is not None else None

    return {
        "fixture_id": fixture.id,
        "model_version": bundle["version"],
        "probabilities": {
            "home": round(prob_by_label["home"], 4),
            "draw": round(prob_by_label["draw"], 4),
            "away": round(prob_by_label["away"], 4),
        },
        "features_summary": {
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
        },
    }
