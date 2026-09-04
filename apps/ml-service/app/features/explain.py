from __future__ import annotations

import pandas as pd
import shap

from app.features.schema import CATEGORICAL, FEATURE_COLUMNS

_explainer_cache: dict[str, "shap.TreeExplainer"] = {}


def _get_explainer(bundle: dict) -> "shap.TreeExplainer":
    version = bundle["version"]
    if version not in _explainer_cache:
        _explainer_cache[version] = shap.TreeExplainer(bundle["base_model"])
    return _explainer_cache[version]


def explain_prediction(bundle: dict, raw_features: dict, predicted_label_index: int, top_n: int = 5) -> list[dict]:
    """Devuelve las `top_n` features que más empujaron la predicción hacia la clase
    ganadora, usando SHAP sobre el LightGBM base (antes de calibrar probabilidades)."""
    explainer = _get_explainer(bundle)

    row = {col: raw_features.get(col) for col in FEATURE_COLUMNS}
    X = pd.DataFrame([row])
    for col in CATEGORICAL:
        X[col] = X[col].astype("category")

    shap_values = explainer.shap_values(X)
    if isinstance(shap_values, list):
        # Forma legacy en versiones antiguas de shap: lista de arrays por clase.
        class_values = shap_values[predicted_label_index][0]
    else:
        # Forma actual: (n_muestras, n_features, n_clases).
        class_values = shap_values[0, :, predicted_label_index]

    contributions = sorted(
        zip(FEATURE_COLUMNS, class_values.tolist()),
        key=lambda item: abs(item[1]),
        reverse=True,
    )
    return [{"feature": name, "impact": round(value, 4)} for name, value in contributions[:top_n]]
