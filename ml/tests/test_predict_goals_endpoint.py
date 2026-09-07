from io import StringIO

from fastapi.testclient import TestClient

from app.main import app
from tests.test_goals_poisson import _synthetic_goals_df
from training.dataset import FEATURE_COLUMNS

client = TestClient(app)


def _full_schema_csv(num_matches: int) -> str:
    df = _synthetic_goals_df(num_matches)
    for column in FEATURE_COLUMNS:
        df[column] = 0.0
    buffer = StringIO()
    df.to_csv(buffer, index=False)
    return buffer.getvalue()


def test_predict_goals_endpoint_returns_expected_goals_and_markets():
    response = client.post(
        "/predict/goals",
        json={"csv_content": _full_schema_csv(150), "home_team": "Strong FC", "away_team": "Weak FC"},
    )

    assert response.status_code == 200
    body = response.json()
    assert body["home_expected_goals"] > 0
    assert body["away_expected_goals"] > 0
    assert 0.0 <= body["over_2_5"] <= 1.0
    assert 0.0 <= body["btts_yes"] <= 1.0


def test_predict_goals_endpoint_unknown_team_returns_422():
    response = client.post(
        "/predict/goals",
        json={"csv_content": _full_schema_csv(150), "home_team": "Strong FC", "away_team": "Nonexistent FC"},
    )

    assert response.status_code == 422


def test_predict_goals_endpoint_too_few_matches_returns_422():
    response = client.post(
        "/predict/goals",
        json={"csv_content": _full_schema_csv(5), "home_team": "Strong FC", "away_team": "Weak FC"},
    )

    assert response.status_code == 422
