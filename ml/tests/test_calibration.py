import numpy as np
import pytest

from evaluation.calibration import compute_calibration_report, fit_and_compare_isotonic
from tests.test_football_1x2_training import _synthetic_csv
from training import football_1x2
from training.dataset import load_dataset


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


def test_fit_and_compare_isotonic_returns_a_report_for_a_calibratable_algorithm():
    df = load_dataset(_synthetic_csv(80))
    summary = football_1x2.train_and_select(df)
    if hasattr(summary.model, "members"):
        pytest.skip("Ensemble was selected for this seed — not calibratable, see next test.")

    report = fit_and_compare_isotonic(df, summary)

    assert report is not None
    assert report.expected_calibration_error >= 0.0


def test_fit_and_compare_isotonic_returns_none_for_ensemble():
    df = load_dataset(_synthetic_csv(80))
    summary = football_1x2.train_and_select(df)
    # Force the Ensemble path regardless of which algorithm actually won on this seed.
    summary.model = football_1x2.AveragingEnsemble([summary.model])

    assert fit_and_compare_isotonic(df, summary) is None
