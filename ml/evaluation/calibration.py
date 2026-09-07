"""
Calibration (CLAUDE.md section 26): reliability diagram data + Expected Calibration
Error (ECE) for the model's top predicted class. Isotonic regression / Platt scaling
(actually recalibrating the model) are not implemented yet — only measuring how
calibrated the raw model already is. See docs/football-model.md for scope notes.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np


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
