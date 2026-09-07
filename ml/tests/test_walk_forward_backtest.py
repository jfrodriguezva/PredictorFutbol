import pytest

from backtesting.walk_forward import walk_forward_backtest
from tests.test_football_1x2_training import _synthetic_csv
from training.dataset import load_dataset


def test_walk_forward_backtest_too_few_matches_raises():
    df = load_dataset(_synthetic_csv(30))

    with pytest.raises(ValueError):
        walk_forward_backtest(df, n_windows=5)


def test_walk_forward_backtest_produces_one_result_per_window():
    df = load_dataset(_synthetic_csv(120))

    summary = walk_forward_backtest(df, n_windows=5)

    assert len(summary.windows) == 5
    for window in summary.windows:
        assert window.train_size > 0
        assert window.test_size > 0


def test_walk_forward_backtest_windows_are_chronological_not_random():
    df = load_dataset(_synthetic_csv(120))

    summary = walk_forward_backtest(df, n_windows=5)

    # Train size must strictly grow window-over-window — proof the split walks
    # forward through time rather than reshuffling randomly each time.
    train_sizes = [w.train_size for w in summary.windows]
    assert train_sizes == sorted(train_sizes)
    assert len(set(train_sizes)) == len(train_sizes)


def test_walk_forward_backtest_aggregates_bets_and_drawdown():
    df = load_dataset(_synthetic_csv(120))

    summary = walk_forward_backtest(df, n_windows=5)

    assert summary.total_bets == sum(w.total_bets for w in summary.windows)
    assert summary.max_drawdown >= 0.0
