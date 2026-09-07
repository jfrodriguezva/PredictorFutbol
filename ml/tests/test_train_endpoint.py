from fastapi.testclient import TestClient

from app.main import app
from tests.test_football_1x2_training import _synthetic_csv

client = TestClient(app)


def test_train_endpoint_returns_comparison_and_selection(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)

    response = client.post(
        "/train/football-1x2",
        json={"csv_content": _synthetic_csv(60), "model_name": "football_1x2_test"},
    )

    assert response.status_code == 200
    body = response.json()
    assert len(body["algorithm_results"]) == 4  # LogisticRegression, XGBoost, LightGBM, Ensemble
    assert body["selected_algorithm"] in {"LogisticRegression", "XGBoost", "LightGBM"}
    assert body["version"] == "v001"
    assert body["dataset_size"] == 60


def test_train_endpoint_too_few_matches_returns_422(tmp_path, monkeypatch):
    monkeypatch.chdir(tmp_path)

    response = client.post(
        "/train/football-1x2",
        json={"csv_content": _synthetic_csv(5)},
    )

    assert response.status_code == 422
