namespace SportsPredictor.Domain.Common;

/// <summary>
/// Base class for all domain entities. Concrete entities are introduced in Phase 2.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
