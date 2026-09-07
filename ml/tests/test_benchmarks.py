from evaluation import benchmarks
from tests.test_football_1x2_training import _synthetic_csv
from training.dataset import load_dataset


def test_always_favorite_benchmark_returns_metrics():
    df = load_dataset(_synthetic_csv(60))

    result = benchmarks.always_favorite_benchmark(df)

    assert result.name == "always_favorite"
    assert 0.0 <= result.accuracy <= 1.0
    assert result.log_loss >= 0.0
    assert result.brier_score >= 0.0


def test_bookmaker_implied_benchmark_returns_metrics():
    df = load_dataset(_synthetic_csv(60))

    result = benchmarks.bookmaker_implied_benchmark(df)

    assert result.name == "bookmaker_implied"
    assert 0.0 <= result.accuracy <= 1.0


def test_market_probabilities_normalize_to_one_per_match():
    df = load_dataset(_synthetic_csv(20))

    probs = benchmarks._market_probabilities(df)

    row_sums = probs.sum(axis=1)
    assert all(abs(s - 1.0) < 1e-6 for s in row_sums)
