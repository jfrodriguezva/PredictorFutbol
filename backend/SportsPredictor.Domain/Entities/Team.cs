using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

public class Team : Entity
{
    public required Guid SportId { get; set; }

    public int? ExternalApiFootballId { get; set; }

    public required string Name { get; set; }

    public string? Country { get; set; }

    public Sport? Sport { get; set; }
}
