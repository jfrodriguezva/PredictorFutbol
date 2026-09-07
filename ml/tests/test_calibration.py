import numpy as np
import pytest

from evaluation.calibration import compute_calibration_report


def test_perfectly_calibrated_predictions_have_zero_error():
    # 100 predictions all at confidence 0.7, and exactly 70 are correct.
    n = 100
    probabilities = np.zeros((n, 3))
    probabilities[:, 0] = 0.7
    probabilities[:, 1] = 0.15
    probabilities[:, 2] = 0.15
    y_true_idx = np.array([0] * 70 + [1] * 30)

    report = compute_calibration_report(y_true_idx, probabilities, n_bins=10)

    assert report.expected_calibration_error == pytest.approx(0.0, abs=1e-9)


def test_overconfident_predictions_have_high_error():
    n = 100
    probabilities = np.zeros((n, 3))
    probabilities[:, 0] = 0.95  # very confident...
    probabilities[:, 1] = 0.025
    probabilities[:, 2] = 0.025
    y_true_idx = np.array([0] * 50 + [1] * 50)  # ...but only right half the time

    report = compute_calibration_report(y_true_idx, probabilities, n_bins=10)

    assert report.expected_calibration_error > 0.3


def test_bins_cover_only_populated_ranges():
    probabilities = np.array([[0.5, 0.3, 0.2]] * 10)
    y_true_idx = np.zeros(10, dtype=int)

    report = compute_calibration_report(y_true_idx, probabilities, n_bins=10)

    assert len(report.bins) == 1
    assert report.bins[0].count == 10
