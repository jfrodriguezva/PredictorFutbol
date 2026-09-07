using SportsPredictor.Domain.Common;
using SportsPredictor.Domain.Enums;

namespace SportsPredictor.Domain.Entities;

public class Competition : Entity
{
    public required Guid SportId { get; set; }

    /// <summary>API-Football league id. Null until the API-Football provider ingests this competition (Phase 3).</summary>
    public int? ExternalApiFootballId { get; set; }

    public required string Name { get; set; }

    public string? Country { get; set; }

    public required CompetitionType CompetitionType { get; set; }

    public Sport? Sport { get; set; }
}
