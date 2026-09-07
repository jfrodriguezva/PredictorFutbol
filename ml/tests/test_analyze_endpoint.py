from fastapi.testclient import TestClient

from app.config import settings as ml_settings
from app.main import app
from tests.test_football_1x2_training import _synthetic_csv
from training import football_1x2
from training.dataset import load_dataset

client = TestClient(app)

_FIXTURE = {
    "home_team": "Club A",
    "away_team": "Club B",
    "league_name": "Liga Test",
    "country": "Testland",
    "season": "2024",
    "status": "NS",
}


def _train_and_save(tmp_path, version):
    df = load_dataset(_synthetic_csv(60))
    summary = football_1x2.train_and_select(df)
    return football_1x2.save_artifact(summary, "football_1x2", version, str(tmp_path))


def test_analyze_football_1x2_without_odds_falls_back_to_template(tmp_path, monkeypatch):
    monkeypatch.setattr(ml_settings, "anthropic_api_key", "")
    artifact_path = _train_and_save(tmp_path, "vanalyze1")

    response = client.post(
        "/analyze/football-1x2",
        json={
            "artifact_path": artifact_path,
            "features": {"home_points_before": 40, "away_points_before": 20},
            "fixture": _FIXTURE,
        },
    )

    assert response.status_code == 200
    body = response.json()
    assert abs(body["home"] + body["draw"] + body["away"] - 1.0) < 1e-6
    assert len(body["shap_top_features"]) == 5
    assert body["stakes"] is None
    assert "Resumen del partido" in body["narrative"]


def test_analyze_football_1x2_with_odds_computes_stakes(tmp_path, monkeypatch):
    monkeypatch.setattr(ml_settings, "anthropic_api_key", "")
    artifact_path = _train_and_save(tmp_path, "vanalyze2")

    response = client.post(
        "/analyze/football-1x2",
        json={
            "artifact_path": artifact_path,
            "features": {"home_points_before": 40, "away_points_before": 20},
            "fixture": _FIXTURE,
            "odds": {"home": 1.8, "draw": 3.4, "away": 4.5},
        },
    )

    assert response.status_code == 200
    body = response.json()
    assert body["stakes"] is not None
    assert set(body["stakes"].keys()) == {"home", "draw", "away"}
    assert "Value bet y stake sugerido" in body["narrative"]


def test_analyze_football_1x2_missing_artifact_returns_404():
    response = client.post(
        "/analyze/football-1x2",
        json={"artifact_path": "models/does_not_exist.joblib", "features": {}, "fixture": _FIXTURE},
    )

    assert response.status_code == 404
