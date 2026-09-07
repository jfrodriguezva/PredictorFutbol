"""
Football 1X2 baseline (CLAUDE.md section 20).

Compares Logistic Regression, XGBoost and LightGBM on the Home/Draw/Away target and
selects the best one primarily by Log Loss and Brier Score — never by Accuracy alone.
Full calibration curves / isotonic regression are explicitly Phase 10's job
(CLAUDE.md section 26); this phase only reports Log Loss and Brier Score.

The split is chronological (train = earliest matches, test = most recent), never a
random shuffle — CLAUDE.md section 26: "NUNCA random split para series temporales".
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime

import joblib
import numpy as np
import pandas as pd
from lightgbm import LGBMClassifier
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import log_loss
from sklearn.preprocessing import StandardScaler
from xgboost import XGBClassifier

from training.dataset import RESULT_CLASSES, prepare_features

LABEL_TO_INDEX = {label: i for i, label in enumerate(RESULT_CLASSES)}
MIN_MATCHES_REQUIRED = 20
TEST_FRACTION = 0.2


@dataclass
class AlgorithmResult:
    algorithm: str
    log_loss: float
    brier_score: float
    accuracy: float


class AveragingEnsemble:
    """
    Soft-voting ensemble: averages the predict_proba output of already-fitted diverse
    models. A standard, low-risk technique — averaging several models' probabilities
    usually reduces variance a little even when no single model changes, since each
    model's errors are at least partly independent. Picklable (plain class, module
    level) so it saves via joblib exactly like any other candidate's artifact.
    """

    def __init__(self, members: list[object]):
        self.members = members

    def predict_proba(self, x):
        probas = [member.predict_proba(x) for member in self.members]
        return np.mean(probas, axis=0)


@dataclass
class TrainingSummary:
    algorithm_results: list[AlgorithmResult]
    selected_algorithm: str
    dataset_size: int
    training_start_date: datetime
    training_end_date: datetime
    model: object = field(repr=False)
    scaler: StandardScaler = field(repr=False)


def multiclass_brier_score(y_true_idx: np.ndarray, probabilities: np.ndarray) -> float:
    """Mean squared error between predicted probabilities and one-hot actual outcomes, summed over classes."""
    one_hot = np.zeros_like(probabilities)
    one_hot[np.arange(len(y_true_idx)), y_true_idx] = 1.0
    return float(np.mean(np.sum((probabilities - one_hot) ** 2, axis=1)))


def _chronological_split(x: pd.DataFrame, y: pd.Series) -> tuple[pd.DataFrame, pd.DataFrame, pd.Series, pd.Series]:
    split_index = int(len(x) * (1 - TEST_FRACTION))
    split_index = max(1, min(split_index, len(x) - 1))
    return x.iloc[:split_index], x.iloc[split_index:], y.iloc[:split_index], y.iloc[split_index:]


def train_and_select(df: pd.DataFrame) -> TrainingSummary:
    if len(df) < MIN_MATCHES_REQUIRED:
        raise ValueError(
            f"Need at least {MIN_MATCHES_REQUIRED} finished matches to train a baseline; got {len(df)}."
        )

    x, y = prepare_features(df)
    x_train, x_test, y_train, y_test = _chronological_split(x, y)
    y_train_idx = y_train.map(LABEL_TO_INDEX).to_numpy()
    y_test_idx = y_test.map(LABEL_TO_INDEX).to_numpy()

    scaler = StandardScaler()
    x_train_scaled = scaler.fit_transform(x_train)
    x_test_scaled = scaler.transform(x_test)

    candidates: dict[str, object] = {
        "LogisticRegression": LogisticRegression(max_iter=1000),
        # Library defaults (100 shallow-ish trees, lr=0.3 for XGBoost) are tuned for
        # small/quick datasets. With 26k+ pooled matches there's room for more, slower
        # trees with standard regularization (subsample/colsample) to actually pay off —
        # these are common, principled settings for tabular data this size, not values
        # fit to this specific test split (which would just be a subtler leakage).
        "XGBoost": XGBClassifier(
            objective="multi:softprob", num_class=3, eval_metric="mlogloss", verbosity=0,
            n_estimators=400, max_depth=4, learning_rate=0.03, subsample=0.8, colsample_bytree=0.8,
            reg_lambda=1.0,
        ),
        "LightGBM": LGBMClassifier(
            objective="multiclass", num_class=3, verbose=-1,
            n_estimators=400, num_leaves=31, learning_rate=0.03, subsample=0.8, colsample_bytree=0.8,
            reg_lambda=1.0,
        ),
    }

    results: list[AlgorithmResult] = []
    fitted_models: dict[str, object] = {}

    for name, model in candidates.items():
        model.fit(x_train_scaled, y_train_idx)
        probabilities = model.predict_proba(x_test_scaled)
        predicted_idx = np.argmax(probabilities, axis=1)

        results.append(
            AlgorithmResult(
                algorithm=name,
                log_loss=float(log_loss(y_test_idx, probabilities, labels=[0, 1, 2])),
                brier_score=multiclass_brier_score(y_test_idx, probabilities),
                accuracy=float(np.mean(predicted_idx == y_test_idx)),
            )
        )
        fitted_models[name] = model

    # A 4th "candidate": simple probability averaging across all three already-fitted
    # models. Only kept if it actually wins on log loss, same as any other candidate —
    # never forced in.
    ensemble = AveragingEnsemble([fitted_models[name] for name in candidates])
    ensemble_probabilities = ensemble.predict_proba(x_test_scaled)
    ensemble_predicted_idx = np.argmax(ensemble_probabilities, axis=1)
    results.append(
        AlgorithmResult(
            algorithm="Ensemble",
            log_loss=float(log_loss(y_test_idx, ensemble_probabilities, labels=[0, 1, 2])),
            brier_score=multiclass_brier_score(y_test_idx, ensemble_probabilities),
            accuracy=float(np.mean(ensemble_predicted_idx == y_test_idx)),
        )
    )
    fitted_models["Ensemble"] = ensemble

    # Primary selection criterion is Log Loss (lower is better) — Accuracy is
    # reported for reference only, never used to pick the winner (CLAUDE.md section 20).
    best = min(results, key=lambda r: r.log_loss)

    return TrainingSummary(
        algorithm_results=results,
        selected_algorithm=best.algorithm,
        dataset_size=len(df),
        training_start_date=df["match_date_utc"].min().to_pydatetime(),
        training_end_date=df["match_date_utc"].max().to_pydatetime(),
        model=fitted_models[best.algorithm],
        scaler=scaler,
    )


def save_artifact(summary: TrainingSummary, model_name: str, version: str, models_dir: str) -> str:
    """Persists the selected model + its feature scaler together, versioned by filename — never overwritten."""
    import os

    os.makedirs(models_dir, exist_ok=True)
    artifact_path = os.path.join(models_dir, f"{model_name}_{version}.joblib")
    joblib.dump({"model": summary.model, "scaler": summary.scaler, "algorithm": summary.selected_algorithm}, artifact_path)
    return artifact_path


def load_artifact(artifact_path: str) -> dict:
    return joblib.load(artifact_path)


def predict_single(artifact: dict, features: dict[str, float]) -> dict[str, float]:
    """Predicts Home/Draw/Away probabilities for one match from a saved artifact.
    Missing feature keys default to 0.0, same convention as training (prepare_features)."""
    from training.dataset import FEATURE_COLUMNS

    row = pd.DataFrame([{name: features.get(name, 0.0) for name in FEATURE_COLUMNS}])
    scaled = artifact["scaler"].transform(row)
    probabilities = artifact["model"].predict_proba(scaled)[0]
    return {label: float(probabilities[i]) for i, label in enumerate(RESULT_CLASSES)}


def next_version(model_name: str, models_dir: str) -> str:
    """Finds existing {model_name}_vNNN.joblib artifacts and returns the next version — never reuses one."""
    import os
    import re

    if not os.path.isdir(models_dir):
        return "v001"

    pattern = re.compile(rf"^{re.escape(model_name)}_v(\d+)\.joblib$")
    existing = [int(m.group(1)) for f in os.listdir(models_dir) if (m := pattern.match(f))]
    return f"v{(max(existing) + 1) if existing else 1:03d}"
