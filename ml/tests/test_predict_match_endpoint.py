import os

from fastapi.testclient import TestClient

from app.main import app
from tests.test_football_1x2_training import _synthetic_csv
from training import football_1x2
from training.dataset import load_dataset

client = TestClient(app)


def test_predict_match_1x2_uses_saved_artifact(tmp_path):
    df = load_dataset(_synthetic_csv(60))
    summary = football_1x2.train_and_select(df)
    artifact_path = football_1x2.save_artifact(summary, "football_1x2", "vtest", str(tmp_path))

    response = client.post(
        "/predict/football-1x2",
        json={"artifact_path": artifact_path, "features": {"home_points_before": 40, "away_points_before": 20}},
    )

    assert response.status_code == 200
    body = response.json()
    total = body["home"] + body["draw"] + body["away"]
    assert abs(total - 1.0) < 1e-6


def test_predict_match_1x2_missing_artifact_returns_404():
    response = client.post(
        "/predict/football-1x2",
        json={"artifact_path": "models/does_not_exist.joblib", "features": {}},
    )

    assert response.status_code == 404


def test_predict_match_1x2_missing_features_default_to_zero(tmp_path):
    df = load_dataset(_synthetic_csv(60))
    summary = football_1x2.train_and_select(df)
    artifact_path = football_1x2.save_artifact(summary, "football_1x2", "vtest2", str(tmp_path))

    response = client.post("/predict/football-1x2", json={"artifact_path": artifact_path, "features": {}})

    assert response.status_code == 200
