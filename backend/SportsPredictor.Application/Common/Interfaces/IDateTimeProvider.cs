namespace SportsPredictor.Application.Common.Interfaces;

/// <summary>
/// Abstraction over system time so application services stay testable.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
