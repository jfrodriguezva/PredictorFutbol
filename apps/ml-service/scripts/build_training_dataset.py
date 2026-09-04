"""Construye el dataset de entrenamiento a partir de los fixtures sincronizados.

Procesa todos los fixtures con estado FT en orden cronológico, calculando
features (Elo, forma reciente, H2H, descanso, tabla de posiciones) usando
únicamente información anterior al kickoff de cada partido — sin fuga
temporal — y guarda el resultado en data/training_dataset.csv. También
persiste el Elo final de cada equipo en la tabla `elo_ratings` para su uso
en inferencia (Fase 5).

Uso (desde apps/ml-service, con el venv activo):
    python -m scripts.build_training_dataset
"""

from __future__ import annotations

import pandas as pd

import app  # noqa: F401
from app.db.models import EloRating, Fixture
from app.db.session import get_session
from app.features.engineering import FeatureBuilder


def main() -> None:
    session = get_session()
    fixtures = (
        session.query(Fixture)
        .filter(Fixture.status == "FT")
        .order_by(Fixture.kickoff_at.asc())
        .all()
    )
    if not fixtures:
        raise SystemExit("No hay fixtures con estado FT en la base de datos. Corre sync_fixtures.py primero.")

    builder = FeatureBuilder()
    rows = []
    for fixture in fixtures:
        rows.append(builder.features_for(fixture))
        builder.update(fixture)

    df = pd.DataFrame(rows)
    df.to_csv("data/training_dataset.csv", index=False)
    print(f"Dataset generado: {len(df)} filas -> data/training_dataset.csv")
    print(df["result_1x2"].value_counts(normalize=True))

    latest_date = max(f.kickoff_at for f in fixtures).date()
    for team_id, rating in builder.elo.items():
        existing = (
            session.query(EloRating)
            .filter_by(team_id=team_id, as_of_date=latest_date)
            .one_or_none()
        )
        if existing is None:
            session.add(EloRating(team_id=team_id, as_of_date=latest_date, rating=rating))
        else:
            existing.rating = rating
    session.commit()
    print(f"Elo final persistido para {len(builder.elo)} equipos (as_of_date={latest_date})")


if __name__ == "__main__":
    main()
