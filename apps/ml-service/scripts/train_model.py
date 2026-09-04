"""Entrena el modelo de predicción 1X2 (LightGBM multiclase + calibración isotónica).

Split TEMPORAL (no aleatorio): entrena con los partidos más antiguos, calibra
con el siguiente tramo y evalúa con los más recientes — así se simula el
escenario real de predecir partidos futuros sin fuga de información.

Nota sobre ROI: el backtesting de ROI contra cuotas de mercado requiere odds
históricas (tabla `odds_snapshots`), que se poblarán en la Fase 6. Por ahora
se reportan accuracy, log-loss y Brier score contra un baseline naive.

Uso (desde apps/ml-service, con el venv activo):
    python -m scripts.train_model
"""

from __future__ import annotations

import joblib
import numpy as np
import pandas as pd
from lightgbm import LGBMClassifier
from sklearn.calibration import CalibratedClassifierCV
from sklearn.frozen import FrozenEstimator
from sklearn.metrics import accuracy_score, confusion_matrix, log_loss

import app  # noqa: F401
from app.features.schema import CATEGORICAL, FEATURE_COLUMNS, LABEL_MAP, LABEL_NAMES

MODEL_VERSION = "v1"


def brier_score_multiclass(y_true: np.ndarray, probs: np.ndarray, n_classes: int) -> float:
    one_hot = np.eye(n_classes)[y_true]
    return float(np.mean(np.sum((probs - one_hot) ** 2, axis=1)))


def evaluate(name: str, model, X: pd.DataFrame, y: pd.Series) -> None:
    probs = model.predict_proba(X)
    preds = probs.argmax(axis=1)
    acc = accuracy_score(y, preds)
    ll = log_loss(y, probs, labels=[0, 1, 2])
    brier = brier_score_multiclass(y.to_numpy(), probs, 3)
    print(f"[{name}] accuracy={acc:.3f} log_loss={ll:.3f} brier={brier:.3f} n={len(y)}")


def main() -> None:
    df = pd.read_csv("data/training_dataset.csv", parse_dates=["kickoff_at"])
    df = df.sort_values("kickoff_at").reset_index(drop=True)
    df["y"] = df["result_1x2"].map(LABEL_MAP)

    for col in CATEGORICAL:
        df[col] = df[col].astype("category")

    n = len(df)
    train_end = int(n * 0.70)
    calib_end = int(n * 0.85)
    train_df, calib_df, test_df = df.iloc[:train_end], df.iloc[train_end:calib_end], df.iloc[calib_end:]
    print(f"train={len(train_df)} calib={len(calib_df)} test={len(test_df)}")

    X_train, y_train = train_df[FEATURE_COLUMNS], train_df["y"]
    X_calib, y_calib = calib_df[FEATURE_COLUMNS], calib_df["y"]
    X_test, y_test = test_df[FEATURE_COLUMNS], test_df["y"]

    base_model = LGBMClassifier(
        objective="multiclass",
        num_class=3,
        n_estimators=300,
        learning_rate=0.03,
        max_depth=5,
        num_leaves=15,
        min_child_samples=20,
        subsample=0.8,
        colsample_bytree=0.8,
        random_state=42,
        verbosity=-1,
    )
    base_model.fit(X_train, y_train, categorical_feature=CATEGORICAL)

    calibrated_model = CalibratedClassifierCV(FrozenEstimator(base_model), method="isotonic")
    calibrated_model.fit(X_calib, y_calib)

    print("--- Test set ---")
    evaluate("base (sin calibrar)", base_model, X_test, y_test)
    evaluate("calibrado (isotonic)", calibrated_model, X_test, y_test)

    naive_preds = np.full(len(y_test), LABEL_MAP["H"])
    print(f"[baseline naive 'siempre local'] accuracy={accuracy_score(y_test, naive_preds):.3f}")

    probs_test = calibrated_model.predict_proba(X_test)
    cm = confusion_matrix(y_test, probs_test.argmax(axis=1), labels=[0, 1, 2])
    print("Matriz de confusión test (filas=real, cols=predicho) [A, D, H]:")
    print(cm)

    joblib.dump(
        {
            "model": calibrated_model,
            "features": FEATURE_COLUMNS,
            "categorical": CATEGORICAL,
            "label_names": LABEL_NAMES,
            "version": MODEL_VERSION,
        },
        f"data/model_{MODEL_VERSION}.joblib",
    )
    print(f"Modelo guardado en data/model_{MODEL_VERSION}.joblib")


if __name__ == "__main__":
    main()
