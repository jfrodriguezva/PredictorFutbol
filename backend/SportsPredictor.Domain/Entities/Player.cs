using SportsPredictor.Domain.Common;
using SportsPredictor.Domain.Enums;

namespace SportsPredictor.Domain.Entities;

public class Player : Entity
{
    public required Guid TeamId { get; set; }

    public int? ExternalApiFootballId { get; set; }

    public required string Name { get; set; }

    public PlayerPosition Position { get; set; } = PlayerPosition.Unknown;

    public DateOnly? BirthDate { get; set; }

    public Team? Team { get; set; }
}
