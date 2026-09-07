using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

public class Venue : Entity
{
    public int? ExternalApiFootballId { get; set; }

    public required string Name { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public int? AltitudeMeters { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}
