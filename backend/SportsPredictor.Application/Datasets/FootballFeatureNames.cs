namespace SportsPredictor.Application.Datasets;

/// <summary>
/// Names of the features the Dataset Builder computes (CLAUDE.md section 22 asks for
/// an extensible, documented feature catalog). Each is computed strictly from data
/// timestamped before the target match's kickoff (CLAUDE.md section 19, anti-leakage).
/// </summary>
public static class FootballFeatureNames
{
    /// <summary>League points from the most recent StandingSnapshot captured before kickoff.</summary>
    public const string HomePointsBefore = "home_points_before";
    public const string AwayPointsBefore = "away_points_before";

    /// <summary>Goal difference from the same snapshot.</summary>
    public const string HomeGoalDiffBefore = "home_goal_diff_before";
    public const string AwayGoalDiffBefore = "away_goal_diff_before";

    /// <summary>League rank from the same snapshot.</summary>
    public const string HomeRankBefore = "home_rank_before";
    public const string AwayRankBefore = "away_rank_before";

    /// <summary>Points earned (3/1/0) across the team's last 5 finished matches before kickoff.</summary>
    public const string HomeFormPointsLast5 = "home_form_points_last5";
    public const string AwayFormPointsLast5 = "away_form_points_last5";

    /// <summary>Count of InjurySnapshot rows for the team captured in the 14 days before kickoff (approximation — see docs).</summary>
    public const string HomeInjuriesCount = "home_injuries_count";
    public const string AwayInjuriesCount = "away_injuries_count";

    /// <summary>1 if a LineupSnapshot exists for this team+match, 0 otherwise.</summary>
    public const string HomeLineupAnnounced = "home_lineup_announced";
    public const string AwayLineupAnnounced = "away_lineup_announced";

    /// <summary>Average bookmaker implied probability (raw, vig included) across OddsSnapshot rows captured before kickoff.</summary>
    public const string MarketImpliedHomeProbability = "market_implied_home_prob";
    public const string MarketImpliedDrawProbability = "market_implied_draw_prob";
    public const string MarketImpliedAwayProbability = "market_implied_away_prob";

    /// <summary>
    /// Elo rating carried across the team's entire match history (every competition/season,
    /// not reset per season) — updated incrementally match-by-match with a home-advantage
    /// term. More cross-competition-comparable than the season-scoped points/rank above,
    /// which is exactly what a pooled multi-league dataset needs (CLAUDE.md section 22/27).
    /// </summary>
    public const string HomeEloBefore = "home_elo_before";
    public const string AwayEloBefore = "away_elo_before";

    /// <summary>Fraction of the last 5 head-to-head meetings (any competition) this match's home team won. 0.5 (neutral) when the two teams have no prior meeting on record.</summary>
    public const string HeadToHeadHomeWinRate = "h2h_home_win_rate";

    /// <summary>Days since the team's most recent finished match (any competition) before this kickoff. Defaults to 7 (a typical rest week) when there is no prior match on record.</summary>
    public const string HomeRestDays = "home_rest_days";
    public const string AwayRestDays = "away_rest_days";

    /// <summary>1 if the competition is a Cup/knockout format, 0 if it's a League — cup ties can behave structurally differently (e.g. fewer draws once extra time applies).</summary>
    public const string IsCupCompetition = "is_cup_competition";

    /// <summary>
    /// Explicit home-minus-away differentials. A linear model has to learn the "matchup
    /// advantage" itself from two separate scaled features; handing it the difference
    /// directly is a standard, well-known technique for linear classifiers and costs
    /// nothing extra to compute since both sides are already on hand.
    /// </summary>
    public const string EloDiff = "elo_diff";
    public const string PointsDiff = "points_diff";
    public const string RankDiff = "rank_diff";
    public const string GoalDiffDiff = "goal_diff_diff";
    public const string FormDiff = "form_diff";
    public const string RestDiff = "rest_diff";

    /// <summary>Points earned (3/1/0) across the home team's last 5 finished matches played AT HOME, and the away team's last 5 played AWAY — venue-specific form, since a team's home and away strength often differ.</summary>
    public const string HomeFormAtHomeLast5 = "home_form_at_home_last5";
    public const string AwayFormAwayLast5 = "away_form_away_last5";

    /// <summary>
    /// P(home goals == away goals) from an independent-Poisson approximation using each
    /// team's average goals scored/conceded across its last 5 matches (any venue) —
    /// this targets a gap none of the strength-based features above cover: a draw
    /// correlates with both teams tending to be LOW-SCORING, not with the two teams
    /// being closely matched in overall strength (Elo/points/rank measure the latter,
    /// not the former).
    /// </summary>
    public const string PoissonDrawProbability = "poisson_draw_probability";

    public static IReadOnlyList<string> All { get; } =
    [
        HomePointsBefore, AwayPointsBefore,
        HomeGoalDiffBefore, AwayGoalDiffBefore,
        HomeRankBefore, AwayRankBefore,
        HomeFormPointsLast5, AwayFormPointsLast5,
        HomeInjuriesCount, AwayInjuriesCount,
        HomeLineupAnnounced, AwayLineupAnnounced,
        MarketImpliedHomeProbability, MarketImpliedDrawProbability, MarketImpliedAwayProbability,
        HomeEloBefore, AwayEloBefore,
        HeadToHeadHomeWinRate,
        HomeRestDays, AwayRestDays,
        IsCupCompetition,
        EloDiff, PointsDiff, RankDiff, GoalDiffDiff, FormDiff, RestDiff,
        HomeFormAtHomeLast5, AwayFormAwayLast5,
        PoissonDrawProbability,
    ];
}
