"""
Calibration (CLAUDE.md section 26): reliability diagram data + Expected Calibration
Error (ECE) for the model's top predicted class, plus (below) an isotonic-calibrated
comparison. See docs/football-model.md for scope notes.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd
from sklearn.base import clone
from sklearn.calibration import CalibratedClassifierCV

from training import football_1x2
from training.dataset import prepare_features


@dataclass
class CalibrationBin:
    bin_lower: float
    bin_upper: float
    predicted_mean: float
    actual_frequency: float
    count: int


@dataclass
class CalibrationReport:
    expected_calibration_error: float
    bins: list[CalibrationBin] = field(default_factory=list)


def compute_calibration_report(y_true_idx: np.ndarray, probabilities: np.ndarray, n_bins: int = 10) -> CalibrationReport:
    """
    Top-label calibration: for each match, take the model's own predicted (highest)
    probability and whether that prediction was actually correct, then bucket by
    confidence. A well-calibrated model's "predicted_mean" should roughly equal its
    "actual_frequency" in every bin.
    """
    confidences = np.max(probabilities, axis=1)
    predictions = np.argmax(probabilities, axis=1)
    correct = (predictions == y_true_idx).astype(float)

    bin_edges = np.linspace(0.0, 1.0, n_bins + 1)
    bins: list[CalibrationBin] = []
    weighted_error = 0.0
    total = len(confidences)

    for i in range(n_bins):
        lower, upper = bin_edges[i], bin_edges[i + 1]
        in_bin = (confidences >= lower) & (confidences < upper if i < n_bins - 1 else confidences <= upper)
        count = int(np.sum(in_bin))
        if count == 0:
            continue

        predicted_mean = float(np.mean(confidences[in_bin]))
        actual_frequency = float(np.mean(correct[in_bin]))
        bins.append(CalibrationBin(lower, upper, predicted_mean, actual_frequency, count))
        weighted_error += (count / total) * abs(predicted_mean - actual_frequency)

    return CalibrationReport(expected_calibration_error=weighted_error, bins=bins)


def fit_and_compare_isotonic(df: pd.DataFrame, summary: "football_1x2.TrainingSummary") -> CalibrationReport | None:
    """
    Fits an isotonic-calibrated version of the same algorithm class train_and_select
    selected — fresh, 3-fold cross-validated on the training fold only (never touching
    the test fold), so this stays purely diagnostic like the rest of /evaluate — and
    reports its calibration error on the same test fold compute_calibration_report
    already scores the raw model on, for a side-by-side comparison.

    Returns None when the selected algorithm is "Ensemble": AveragingEnsemble is a
    plain averaging wrapper, not a real sklearn estimator (no get_params/set_params),
    so sklearn.base.clone can't produce a fresh copy of it to calibrate.
    """
    if hasattr(summary.model, "members"):
        return None

    x, y = prepare_features(df)
    x_train, x_test, y_train, y_test = football_1x2._chronological_split(x, y)
    y_train_idx = y_train.map(football_1x2.LABEL_TO_INDEX).to_numpy()
    y_test_idx = y_test.map(football_1x2.LABEL_TO_INDEX).to_numpy()

    x_train_scaled = summary.scaler.transform(x_train)
    x_test_scaled = summary.scaler.transform(x_test)

    calibrated = CalibratedClassifierCV(clone(summary.model), method="isotonic", cv=3)
    calibrated.fit(x_train_scaled, y_train_idx)

    calibrated_probabilities = calibrated.predict_proba(x_test_scaled)
    return compute_calibration_report(y_test_idx, calibrated_probabilities)
