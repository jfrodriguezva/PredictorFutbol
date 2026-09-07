using SportsPredictor.Domain.Common;

namespace SportsPredictor.Domain.Entities;

/// <summary>
/// Optional raw payload capture for critical endpoints, kept for traceability
/// (auditing what the provider actually returned at a point in time).
/// </summary>
public class ApiRawSnapshot : Entity
{
    public required string Provider { get; set; }

    public required string Endpoint { get; set; }

    public string? ExternalEntityId { get; set; }

    public required DateTime CapturedAt { get; set; }

    public required string PayloadJson { get; set; }
}
