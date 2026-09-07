using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SportsPredictor.Application.Common.Exceptions;
using SportsPredictor.Application.Datasets;
using SportsPredictor.Domain.Entities;
using SportsPredictor.Domain.Enums;
using SportsPredictor.Infrastructure.Persistence;

namespace SportsPredictor.Infrastructure.Datasets;

/// <summary>
/// CLAUDE.md section 18's "Dataset Builder". Computes anti-leakage-safe features
/// (section 19/22) for finished matches and exports a flat, reproducible CSV — the
/// only channel through which the Python ML service ever sees this data.
/// </summary>
public sealed class DatasetBuilderService : IDatasetBuilderService
{
    private const string MatchWinnerMarket = "Match Winner";
    private static readonly TimeSpan InjuryLookback = TimeSpan.FromDays(14);
    private const int FormWindowMatches = 5;

    private readonly SportsPredictorDbContext _dbContext;

    public DatasetBuilderService(SportsPredictorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BuildFeaturesResultDto> BuildFeaturesAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var (tracked, season) = await GetTrackedCompetitionAndSeasonAsync(trackedCompetitionId, cancellationToken);
        var capturedAt = DateTime.UtcNow;

        if (season is null)
        {
            return new BuildFeaturesResultDto(0, 0, capturedAt);
        }

        var matches = await _dbContext.Matches
            .Where(m => m.CompetitionId == tracked.CompetitionId && m.SeasonId == season.Id && m.Status == MatchStatus.Finished)
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);

        var competition = await _dbContext.Competitions.FindAsync([tracked.CompetitionId], cancellationToken);
        var competitionType = competition?.CompetitionType ?? CompetitionType.League;

        // Elo is a single running rating per team across its ENTIRE history (every
        // competition/season, never reset) — computing it fresh per match would replay
        // the whole matches table once per match. One global pass up front, keyed by
        // matchId, gives every match in this batch its "Elo before kickoff" in O(all
        // finished matches) total instead of O(matches in this batch x all matches).
        var eloBeforeByMatch = await ComputeEloBeforeEachMatchAsync(cancellationToken);

        var featureValuesCreated = 0;

        foreach (var match in matches)
        {
            var features = await ComputeFeaturesForMatchAsync(match, season.Id, competitionType, eloBeforeByMatch, cancellationToken);
            foreach (var (name, value) in features)
            {
                _dbContext.FeatureValues.Add(new FeatureValue
                {
                    MatchId = match.Id,
                    FeatureName = name,
                    NumericValue = value,
                    // Conservative anti-leakage bound: these features only use data
                    // strictly before kickoff, so they are "available" no later than kickoff.
                    AvailableAt = match.MatchDate,
                    CapturedAt = capturedAt,
                });
                featureValuesCreated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new BuildFeaturesResultDto(matches.Count, featureValuesCreated, capturedAt);
    }

    public async Task<string> ExportDatasetCsvAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var (tracked, _) = await GetTrackedCompetitionAndSeasonAsync(trackedCompetitionId, cancellationToken);

        // A model trained on one season alone (often just a few dozen finished matches)
        // is statistically unreliable. Export every finished match for this competition
        // across all tracked seasons instead — each match's features were already
        // computed using only data strictly before its own kickoff (BuildFeaturesAsync
        // is called per season), so combining seasons adds no leakage, only sample size.
        var matches = await _dbContext.Matches
            .Where(m => m.CompetitionId == tracked.CompetitionId && m.Status == MatchStatus.Finished)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);

        return await BuildCsvAsync(matches, cancellationToken);
    }

    public async Task<string> ExportGlobalDatasetCsvAsync(CancellationToken cancellationToken)
    {
        var matches = await _dbContext.Matches
            .Where(m => m.Status == MatchStatus.Finished)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .OrderBy(m => m.MatchDate)
            .ToListAsync(cancellationToken);

        return await BuildCsvAsync(matches, cancellationToken);
    }

    private async Task<string> BuildCsvAsync(List<Match> matches, CancellationToken cancellationToken)
    {
        var matchIds = matches.Select(m => m.Id).ToList();
        var latestFeatureByMatchAndName = (await _dbContext.FeatureValues
                .Where(f => matchIds.Contains(f.MatchId))
                .ToListAsync(cancellationToken))
            .GroupBy(f => (f.MatchId, f.FeatureName))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.CapturedAt).First().NumericValue);

        var builder = new StringBuilder();
        var headers = new List<string> { "match_id", "match_date_utc", "home_team", "away_team", "home_score", "away_score", "result" };
        headers.AddRange(FootballFeatureNames.All);
        builder.AppendLine(string.Join(",", headers));

        foreach (var match in matches)
        {
            var result = match.HomeScore == match.AwayScore ? "D" : match.HomeScore > match.AwayScore ? "H" : "A";

            var row = new List<string>
            {
                match.Id.ToString(),
                match.MatchDate.ToString("o", CultureInfo.InvariantCulture),
                CsvEscape(match.HomeTeam?.Name ?? string.Empty),
                CsvEscape(match.AwayTeam?.Name ?? string.Empty),
                match.HomeScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                match.AwayScore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result,
            };

            foreach (var featureName in FootballFeatureNames.All)
            {
                row.Add(latestFeatureByMatchAndName.TryGetValue((match.Id, featureName), out var value)
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
            }

            builder.AppendLine(string.Join(",", row));
        }

        return builder.ToString();
    }

    public async Task<IReadOnlyDictionary<string, double>> ComputeFeaturesForPredictionAsync(Guid matchId, CancellationToken cancellationToken)
    {
        var match = await _dbContext.Matches.FindAsync([matchId], cancellationToken)
            ?? throw new NotFoundException(nameof(Match), matchId);

        var competition = await _dbContext.Competitions.FindAsync([match.CompetitionId], cancellationToken);
        var competitionType = competition?.CompetitionType ?? CompetitionType.League;

        var features = await ComputeFeaturesForMatchAsync(match, match.SeasonId, competitionType, eloBeforeByMatch: null, cancellationToken);
        return features.ToDictionary(f => f.Name, f => f.Value);
    }

    private async Task<(TrackedCompetition tracked, Season? season)> GetTrackedCompetitionAndSeasonAsync(Guid trackedCompetitionId, CancellationToken cancellationToken)
    {
        var tracked = await _dbContext.TrackedCompetitions.FindAsync([trackedCompetitionId], cancellationToken)
            ?? throw new NotFoundException(nameof(TrackedCompetition), trackedCompetitionId);

        var season = await _dbContext.Seasons.FirstOrDefaultAsync(
            s => s.CompetitionId == tracked.CompetitionId && s.StartDate.Year == tracked.Season,
            cancellationToken);

        return (tracked, season);
    }

    private async Task<List<(string Name, double Value)>> ComputeFeaturesForMatchAsync(
        Match match,
        Guid seasonId,
        CompetitionType competitionType,
        IReadOnlyDictionary<Guid, (double HomeEloBefore, double AwayEloBefore)>? eloBeforeByMatch,
        CancellationToken cancellationToken)
    {
        var features = new List<(string, double)>();

        // API-Football's /standings only exposes the CURRENT table, so a snapshot's
        // CapturedAt is always after every already-played match this season — a
        // snapshot-based lookup can never satisfy "before kickoff" for historical
        // matches. Compute the table as-of that date directly from match results
        // already in SQLite instead (still zero leakage: only matches strictly
        // before beforeUtc are read), which is available for every match, not just
        // the ones ahead of the one snapshot we happen to have captured today.
        var standingsToDate = await ComputeStandingsToDateAsync(seasonId, match.MatchDate, cancellationToken);

        var hasHomeStanding = standingsToDate.TryGetValue(match.HomeTeamId, out var homeStanding);
        if (hasHomeStanding)
        {
            features.Add((FootballFeatureNames.HomePointsBefore, homeStanding.Points));
            features.Add((FootballFeatureNames.HomeGoalDiffBefore, homeStanding.GoalDifference));
            features.Add((FootballFeatureNames.HomeRankBefore, homeStanding.Rank));
        }

        var hasAwayStanding = standingsToDate.TryGetValue(match.AwayTeamId, out var awayStanding);
        if (hasAwayStanding)
        {
            features.Add((FootballFeatureNames.AwayPointsBefore, awayStanding.Points));
            features.Add((FootballFeatureNames.AwayGoalDiffBefore, awayStanding.GoalDifference));
            features.Add((FootballFeatureNames.AwayRankBefore, awayStanding.Rank));
        }

        var homeFormPoints = await ComputeFormPointsAsync(match.HomeTeamId, match.MatchDate, cancellationToken);
        var awayFormPoints = await ComputeFormPointsAsync(match.AwayTeamId, match.MatchDate, cancellationToken);
        features.Add((FootballFeatureNames.HomeFormPointsLast5, homeFormPoints));
        features.Add((FootballFeatureNames.AwayFormPointsLast5, awayFormPoints));

        features.Add((FootballFeatureNames.HomeFormAtHomeLast5,
            await ComputeVenueSpecificFormPointsAsync(match.HomeTeamId, asHomeTeam: true, match.MatchDate, cancellationToken)));
        features.Add((FootballFeatureNames.AwayFormAwayLast5,
            await ComputeVenueSpecificFormPointsAsync(match.AwayTeamId, asHomeTeam: false, match.MatchDate, cancellationToken)));

        features.Add((FootballFeatureNames.PoissonDrawProbability,
            await ComputePoissonDrawProbabilityAsync(match.HomeTeamId, match.AwayTeamId, match.MatchDate, cancellationToken)));

        var injuryWindowStart = match.MatchDate - InjuryLookback;
        features.Add((FootballFeatureNames.HomeInjuriesCount, await _dbContext.InjurySnapshots.CountAsync(
            i => i.TeamId == match.HomeTeamId && i.CapturedAt >= injuryWindowStart && i.CapturedAt <= match.MatchDate, cancellationToken)));
        features.Add((FootballFeatureNames.AwayInjuriesCount, await _dbContext.InjurySnapshots.CountAsync(
            i => i.TeamId == match.AwayTeamId && i.CapturedAt >= injuryWindowStart && i.CapturedAt <= match.MatchDate, cancellationToken)));

        features.Add((FootballFeatureNames.HomeLineupAnnounced,
            await _dbContext.LineupSnapshots.AnyAsync(l => l.MatchId == match.Id && l.TeamId == match.HomeTeamId, cancellationToken) ? 1 : 0));
        features.Add((FootballFeatureNames.AwayLineupAnnounced,
            await _dbContext.LineupSnapshots.AnyAsync(l => l.MatchId == match.Id && l.TeamId == match.AwayTeamId, cancellationToken) ? 1 : 0));

        var oddsBeforeKickoff = await _dbContext.OddsSnapshots
            .Where(o => o.MatchId == match.Id && o.Market == MatchWinnerMarket && o.CapturedAt <= match.MatchDate)
            .ToListAsync(cancellationToken);
        AddAverageIfAny(features, FootballFeatureNames.MarketImpliedHomeProbability, oddsBeforeKickoff.Where(o => o.Selection == "Home"));
        AddAverageIfAny(features, FootballFeatureNames.MarketImpliedDrawProbability, oddsBeforeKickoff.Where(o => o.Selection == "Draw"));
        AddAverageIfAny(features, FootballFeatureNames.MarketImpliedAwayProbability, oddsBeforeKickoff.Where(o => o.Selection == "Away"));

        double homeElo, awayElo;
        if (eloBeforeByMatch is not null && eloBeforeByMatch.TryGetValue(match.Id, out var eloBefore))
        {
            (homeElo, awayElo) = eloBefore;
        }
        else
        {
            var ratingsAsOf = await ComputeEloRatingsAsOfAsync(match.MatchDate, cancellationToken);
            homeElo = ratingsAsOf.GetValueOrDefault(match.HomeTeamId, InitialElo);
            awayElo = ratingsAsOf.GetValueOrDefault(match.AwayTeamId, InitialElo);
        }
        features.Add((FootballFeatureNames.HomeEloBefore, homeElo));
        features.Add((FootballFeatureNames.AwayEloBefore, awayElo));

        features.Add((FootballFeatureNames.HeadToHeadHomeWinRate,
            await ComputeHeadToHeadHomeWinRateAsync(match.HomeTeamId, match.AwayTeamId, match.MatchDate, cancellationToken)));

        var homeRestDays = await ComputeRestDaysAsync(match.HomeTeamId, match.MatchDate, cancellationToken);
        var awayRestDays = await ComputeRestDaysAsync(match.AwayTeamId, match.MatchDate, cancellationToken);
        features.Add((FootballFeatureNames.HomeRestDays, homeRestDays));
        features.Add((FootballFeatureNames.AwayRestDays, awayRestDays));

        features.Add((FootballFeatureNames.IsCupCompetition, competitionType == CompetitionType.League ? 0 : 1));

        // Explicit home-minus-away differentials — see FootballFeatureNames for why:
        // a linear model benefits from being handed the matchup advantage directly.
        features.Add((FootballFeatureNames.EloDiff, homeElo - awayElo));
        if (hasHomeStanding && hasAwayStanding)
        {
            features.Add((FootballFeatureNames.PointsDiff, homeStanding.Points - awayStanding.Points));
            features.Add((FootballFeatureNames.RankDiff, awayStanding.Rank - homeStanding.Rank));
            features.Add((FootballFeatureNames.GoalDiffDiff, homeStanding.GoalDifference - awayStanding.GoalDifference));
        }
        features.Add((FootballFeatureNames.FormDiff, homeFormPoints - awayFormPoints));
        features.Add((FootballFeatureNames.RestDiff, homeRestDays - awayRestDays));

        return features;
    }

    private const double InitialElo = 1500.0;
    private const double EloKFactor = 20.0;
    private const double EloHomeAdvantage = 60.0;

    /// <summary>Global Elo replay across every finished match ever recorded (any competition), returning the rating each team held immediately before each match's own kickoff.</summary>
    private async Task<Dictionary<Guid, (double HomeEloBefore, double AwayEloBefore)>> ComputeEloBeforeEachMatchAsync(CancellationToken cancellationToken)
    {
        var (eloBeforeByMatch, _) = await ReplayEloAsync(strictlyBeforeUtc: null, cancellationToken);
        return eloBeforeByMatch;
    }

    /// <summary>Same Elo replay, but stopped strictly before <paramref name="beforeUtc"/> and returning the resulting per-team ratings snapshot — for a single ad-hoc prediction rather than a whole batch.</summary>
    private async Task<Dictionary<Guid, double>> ComputeEloRatingsAsOfAsync(DateTime beforeUtc, CancellationToken cancellationToken)
    {
        var (_, finalRatings) = await ReplayEloAsync(beforeUtc, cancellationToken);
        return finalRatings;
    }

    private async Task<(Dictionary<Guid, (double, double)> EloBeforeByMatch, Dictionary<Guid, double> FinalRatings)> ReplayEloAsync(
        DateTime? strictlyBeforeUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.Matches.Where(m => m.Status == MatchStatus.Finished && m.HomeScore != null && m.AwayScore != null);
        if (strictlyBeforeUtc is { } cutoff)
        {
            query = query.Where(m => m.MatchDate < cutoff);
        }

        var orderedMatches = await query
            .OrderBy(m => m.MatchDate)
            .Select(m => new { m.Id, m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore })
            .ToListAsync(cancellationToken);

        var elo = new Dictionary<Guid, double>();
        var eloBeforeByMatch = new Dictionary<Guid, (double, double)>();

        foreach (var m in orderedMatches)
        {
            var homeElo = elo.GetValueOrDefault(m.HomeTeamId, InitialElo);
            var awayElo = elo.GetValueOrDefault(m.AwayTeamId, InitialElo);
            eloBeforeByMatch[m.Id] = (homeElo, awayElo);

            var expectedHome = 1.0 / (1.0 + Math.Pow(10, (awayElo - (homeElo + EloHomeAdvantage)) / 400.0));
            var actualHome = m.HomeScore > m.AwayScore ? 1.0 : m.HomeScore == m.AwayScore ? 0.5 : 0.0;

            elo[m.HomeTeamId] = homeElo + EloKFactor * (actualHome - expectedHome);
            elo[m.AwayTeamId] = awayElo + EloKFactor * ((1.0 - actualHome) - (1.0 - expectedHome));
        }

        return (eloBeforeByMatch, elo);
    }

    /// <summary>Fraction of the last 5 head-to-head meetings (any competition, either venue) this match's home team won. 0.5 when there is no prior meeting.</summary>
    private async Task<double> ComputeHeadToHeadHomeWinRateAsync(Guid homeTeamId, Guid awayTeamId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        const int HeadToHeadWindow = 5;

        var meetings = await _dbContext.Matches
            .Where(m => m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc && m.HomeScore != null && m.AwayScore != null &&
                ((m.HomeTeamId == homeTeamId && m.AwayTeamId == awayTeamId) || (m.HomeTeamId == awayTeamId && m.AwayTeamId == homeTeamId)))
            .OrderByDescending(m => m.MatchDate)
            .Take(HeadToHeadWindow)
            .Select(m => new { m.HomeTeamId, m.HomeScore, m.AwayScore })
            .ToListAsync(cancellationToken);

        if (meetings.Count == 0)
        {
            return 0.5;
        }

        var homeTeamWins = meetings.Count(m =>
            (m.HomeTeamId == homeTeamId && m.HomeScore > m.AwayScore) ||
            (m.HomeTeamId != homeTeamId && m.AwayScore > m.HomeScore));

        return (double)homeTeamWins / meetings.Count;
    }

    /// <summary>Days since the team's most recent finished match (any competition) before this kickoff. Defaults to 7 (a typical rest week) with no prior match on record.</summary>
    private async Task<double> ComputeRestDaysAsync(Guid teamId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        const double DefaultRestDays = 7.0;

        var lastMatchDate = await _dbContext.Matches
            .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc)
            .OrderByDescending(m => m.MatchDate)
            .Select(m => (DateTime?)m.MatchDate)
            .FirstOrDefaultAsync(cancellationToken);

        return lastMatchDate is { } last ? (beforeUtc - last).TotalDays : DefaultRestDays;
    }

    private async Task<Dictionary<Guid, (int Rank, int Points, int GoalDifference)>> ComputeStandingsToDateAsync(
        Guid seasonId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        var priorMatches = await _dbContext.Matches
            .Where(m => m.SeasonId == seasonId && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc
                && m.HomeScore != null && m.AwayScore != null)
            .Select(m => new { m.HomeTeamId, m.AwayTeamId, m.HomeScore, m.AwayScore })
            .ToListAsync(cancellationToken);

        var table = new Dictionary<Guid, (int Points, int GoalsFor, int GoalsAgainst)>();

        void Accumulate(Guid teamId, int goalsFor, int goalsAgainst, int points)
        {
            var current = table.TryGetValue(teamId, out var existing) ? existing : (Points: 0, GoalsFor: 0, GoalsAgainst: 0);
            table[teamId] = (current.Points + points, current.GoalsFor + goalsFor, current.GoalsAgainst + goalsAgainst);
        }

        foreach (var m in priorMatches)
        {
            var homeGoals = m.HomeScore!.Value;
            var awayGoals = m.AwayScore!.Value;
            var homePoints = homeGoals > awayGoals ? 3 : homeGoals == awayGoals ? 1 : 0;
            var awayPoints = awayGoals > homeGoals ? 3 : awayGoals == homeGoals ? 1 : 0;

            Accumulate(m.HomeTeamId, homeGoals, awayGoals, homePoints);
            Accumulate(m.AwayTeamId, awayGoals, homeGoals, awayPoints);
        }

        var ranked = table
            .Select(kv => new { TeamId = kv.Key, kv.Value.Points, GoalDifference = kv.Value.GoalsFor - kv.Value.GoalsAgainst })
            .OrderByDescending(t => t.Points)
            .ThenByDescending(t => t.GoalDifference)
            .ToList();

        var result = new Dictionary<Guid, (int Rank, int Points, int GoalDifference)>();
        for (var i = 0; i < ranked.Count; i++)
        {
            result[ranked[i].TeamId] = (i + 1, ranked[i].Points, ranked[i].GoalDifference);
        }

        return result;
    }

    private async Task<double> ComputeFormPointsAsync(Guid teamId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        var recentMatches = await _dbContext.Matches
            .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc)
            .OrderByDescending(m => m.MatchDate)
            .Take(FormWindowMatches)
            .ToListAsync(cancellationToken);

        double points = 0;
        foreach (var match in recentMatches)
        {
            var isHome = match.HomeTeamId == teamId;
            var teamScore = isHome ? match.HomeScore : match.AwayScore;
            var otherScore = isHome ? match.AwayScore : match.HomeScore;
            if (teamScore is null || otherScore is null)
            {
                continue;
            }

            if (teamScore > otherScore)
            {
                points += 3;
            }
            else if (teamScore == otherScore)
            {
                points += 1;
            }
        }

        return points;
    }

    /// <summary>Points earned (3/1/0) across a team's last 5 finished matches played specifically at the given venue side (home-only or away-only) — a team's home and away strength often differ meaningfully from its overall form.</summary>
    private async Task<double> ComputeVenueSpecificFormPointsAsync(Guid teamId, bool asHomeTeam, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        var recentMatches = asHomeTeam
            ? await _dbContext.Matches
                .Where(m => m.HomeTeamId == teamId && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc)
                .OrderByDescending(m => m.MatchDate)
                .Take(FormWindowMatches)
                .Select(m => new { m.HomeScore, m.AwayScore })
                .ToListAsync(cancellationToken)
            : await _dbContext.Matches
                .Where(m => m.AwayTeamId == teamId && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc)
                .OrderByDescending(m => m.MatchDate)
                .Take(FormWindowMatches)
                .Select(m => new { m.HomeScore, m.AwayScore })
                .ToListAsync(cancellationToken);

        double points = 0;
        foreach (var m in recentMatches)
        {
            var teamScore = asHomeTeam ? m.HomeScore : m.AwayScore;
            var otherScore = asHomeTeam ? m.AwayScore : m.HomeScore;
            if (teamScore is null || otherScore is null)
            {
                continue;
            }

            if (teamScore > otherScore)
            {
                points += 3;
            }
            else if (teamScore == otherScore)
            {
                points += 1;
            }
        }

        return points;
    }

    /// <summary>
    /// P(home goals == away goals) from an independent-Poisson approximation built from
    /// each team's average goals scored/conceded across its last 5 matches (any venue,
    /// any competition) before kickoff. Deliberately a lightweight rolling-rate estimate
    /// rather than a fitted Poisson GLM (training/goals_poisson.py's team-effects model
    /// is fit once over the WHOLE dataset for hypothetical-fixture prediction — reusing
    /// it here would leak future match results into a historical match's features).
    /// </summary>
    private async Task<double> ComputePoissonDrawProbabilityAsync(Guid homeTeamId, Guid awayTeamId, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        const int MaxGoalsConsidered = 8;

        var homeAttack = await ComputeAverageGoalsAsync(homeTeamId, scored: true, beforeUtc, cancellationToken);
        var homeDefense = await ComputeAverageGoalsAsync(homeTeamId, scored: false, beforeUtc, cancellationToken);
        var awayAttack = await ComputeAverageGoalsAsync(awayTeamId, scored: true, beforeUtc, cancellationToken);
        var awayDefense = await ComputeAverageGoalsAsync(awayTeamId, scored: false, beforeUtc, cancellationToken);

        var expectedHomeGoals = Math.Max(0.1, (homeAttack + awayDefense) / 2.0);
        var expectedAwayGoals = Math.Max(0.1, (awayAttack + homeDefense) / 2.0);

        var drawProbability = 0.0;
        for (var goals = 0; goals <= MaxGoalsConsidered; goals++)
        {
            drawProbability += PoissonPmf(goals, expectedHomeGoals) * PoissonPmf(goals, expectedAwayGoals);
        }

        return drawProbability;
    }

    private static double PoissonPmf(int k, double lambda) => Math.Exp(-lambda) * Math.Pow(lambda, k) / Factorial(k);

    private static double Factorial(int n)
    {
        double result = 1;
        for (var i = 2; i <= n; i++)
        {
            result *= i;
        }

        return result;
    }

    /// <summary>Average goals scored (or conceded) by a team across its last 5 finished matches (any venue) before kickoff. Defaults to 1.3 (a typical league-average goals rate) with no prior match on record.</summary>
    private async Task<double> ComputeAverageGoalsAsync(Guid teamId, bool scored, DateTime beforeUtc, CancellationToken cancellationToken)
    {
        const double DefaultGoalsRate = 1.3;

        var recentMatches = await _dbContext.Matches
            .Where(m => (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && m.Status == MatchStatus.Finished && m.MatchDate < beforeUtc
                && m.HomeScore != null && m.AwayScore != null)
            .OrderByDescending(m => m.MatchDate)
            .Take(FormWindowMatches)
            .Select(m => new { m.HomeTeamId, m.HomeScore, m.AwayScore })
            .ToListAsync(cancellationToken);

        if (recentMatches.Count == 0)
        {
            return DefaultGoalsRate;
        }

        var total = 0;
        foreach (var m in recentMatches)
        {
            var isHome = m.HomeTeamId == teamId;
            var goalsFor = isHome ? m.HomeScore!.Value : m.AwayScore!.Value;
            var goalsAgainst = isHome ? m.AwayScore!.Value : m.HomeScore!.Value;
            total += scored ? goalsFor : goalsAgainst;
        }

        return (double)total / recentMatches.Count;
    }

    private static void AddAverageIfAny(List<(string, double)> features, string name, IEnumerable<OddsSnapshot> snapshots)
    {
        var list = snapshots.ToList();
        if (list.Count > 0)
        {
            features.Add((name, list.Average(s => s.ImpliedProbability)));
        }
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
