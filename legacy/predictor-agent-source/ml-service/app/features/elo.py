DEFAULT_RATING = 1500.0
K_FACTOR = 20.0
HOME_ADVANTAGE = 60.0


def expected_score(rating_a: float, rating_b: float) -> float:
    return 1.0 / (1.0 + 10 ** ((rating_b - rating_a) / 400.0))


def update_elo(home_rating: float, away_rating: float, result: str) -> tuple[float, float]:
    """result: 'H' | 'D' | 'A', desde la perspectiva del equipo local."""
    exp_home = expected_score(home_rating + HOME_ADVANTAGE, away_rating)
    score_home = {"H": 1.0, "D": 0.5, "A": 0.0}[result]
    score_away = 1.0 - score_home
    new_home = home_rating + K_FACTOR * (score_home - exp_home)
    new_away = away_rating + K_FACTOR * (score_away - (1.0 - exp_home))
    return new_home, new_away
