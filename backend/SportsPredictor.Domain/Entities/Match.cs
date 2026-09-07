using SportsPredictor.Domain.Common;
using SportsPredictor.Domain.Enums;

namespace SportsPredictor.Domain.Entities;

public class Match : Entity
{
    public int? ExternalApiFootballId { get; set; }

    public required Guid CompetitionId { get; set; }

    public required Guid SeasonId { get; set; }

    public required Guid HomeTeamId { get; set; }

    public required Guid AwayTeamId { get; set; }

    /// <summary>UTC kickoff date/time.</summary>
    public required DateTime MatchDate { get; set; }

    public required MatchStatus Status { get; set; }

    /// <summary>Null until the match has been played.</summary>
    public int? HomeScore { get; set; }

    /// <summary>Null until the match has been played.</summary>
    public int? AwayScore { get; set; }

    public Guid? VenueId { get; set; }

    public Competition? Competition { get; set; }
    public Season? Season { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
    public Venue? Venue { get; set; }
}
