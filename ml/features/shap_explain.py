"""
SHAP feature attribution for a single football_1x2 prediction — ported from the
standalone agent prototype (see docs/football-model.md). Stateless: operates only on an
already-loaded model artifact (training.football_1x2.load_artifact) and a features
dict, same contract as training.football_1x2.predict_single.

training.football_1x2.train_and_select can select any of 4 candidates — Logistic
Regression, XGBoost, LightGBM, or a simple averaging Ensemble of the three — and only
the tree-based ones are directly supported by shap.TreeExplainer, so this dispatches
per model type:
- XGBoost / LightGBM: shap.TreeExplainer (exact, no background data needed).
- LogisticRegression: coefficient x scaled-feature-value. This equals the SHAP value
  for a linear model when the baseline is the training mean — StandardScaler already
  centers every feature at ~0 on the training set, so no persisted background sample
  is needed.
- Ensemble: averages the per-member contributions above, mirroring how
  AveragingEnsemble.predict_proba averages predict_proba across its members.
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import shap

from training.dataset import FEATURE_COLUMNS

_tree_explainer_cache: dict[int, "shap.TreeExplainer"] = {}


def _scaled_row(artifact: dict, features: dict[str, float]) -> np.ndarray:
    row = pd.DataFrame([{name: features.get(name, 0.0) for name in FEATURE_COLUMNS}])
    return artifact["scaler"].transform(row)


def _tree_contributions(model, scaled_row: np.ndarray, predicted_label_index: int) -> np.ndarray:
    explainer = _tree_explainer_cache.get(id(model))
    if explainer is None:
        explainer = shap.TreeExplainer(model)
        _tree_explainer_cache[id(model)] = explainer

    shap_values = explainer.shap_values(scaled_row)
    if isinstance(shap_values, list):
        # Legacy shape in older shap versions: one array per class.
        return np.asarray(shap_values[predicted_label_index][0])
    values = np.asarray(shap_values)
    if values.ndim == 3:
        # Current shape: (n_samples, n_features, n_classes).
        return values[0, :, predicted_label_index]
    return values[0]


def _linear_contributions(model, scaled_row: np.ndarray, predicted_label_index: int) -> np.ndarray:
    coef = model.coef_[predicted_label_index]
    return coef * scaled_row[0]


def _member_contributions(model, scaled_row: np.ndarray, predicted_label_index: int) -> np.ndarray:
    if hasattr(model, "members"):
        contributions = [
            _member_contributions(member, scaled_row, predicted_label_index) for member in model.members
        ]
        return np.mean(contributions, axis=0)
    if hasattr(model, "coef_"):
        return _linear_contributions(model, scaled_row, predicted_label_index)
    return _tree_contributions(model, scaled_row, predicted_label_index)


def top_shap_features(
    artifact: dict, features: dict[str, float], predicted_label_index: int, top_n: int = 5
) -> list[dict]:
    """Returns the `top_n` features that most pushed the prediction toward the winning
    class, as {"feature": name, "impact": value} dicts sorted by |impact| descending."""
    scaled_row = _scaled_row(artifact, features)
    contributions = _member_contributions(artifact["model"], scaled_row, predicted_label_index)
    ranked = sorted(zip(FEATURE_COLUMNS, contributions.tolist()), key=lambda item: abs(item[1]), reverse=True)
    return [{"feature": name, "impact": round(value, 4)} for name, value in ranked[:top_n]]
