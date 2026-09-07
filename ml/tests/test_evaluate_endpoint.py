from fastapi.testclient import TestClient

from app.main import app
from tests.test_football_1x2_training import _synthetic_csv

client = TestClient(app)


def test_evaluate_endpoint_returns_calibration_backtest_and_benchmarks():
    response = client.post(
        "/evaluate/football-1x2",
        json={"csv_content": _synthetic_csv(120), "n_windows": 5},
    )

    assert response.status_code == 200
    body = response.json()
    assert "expected_calibration_error" in body["calibration"]
    assert len(body["backtest"]["windows"]) == 5
    assert {b["name"] for b in body["benchmarks"]} == {"always_favorite", "bookmaker_implied"}
    assert body["dataset_size"] == 120


def test_evaluate_endpoint_too_few_matches_returns_422():
    response = client.post(
        "/evaluate/football-1x2",
        json={"csv_content": _synthetic_csv(20)},
    )

    assert response.status_code == 422
