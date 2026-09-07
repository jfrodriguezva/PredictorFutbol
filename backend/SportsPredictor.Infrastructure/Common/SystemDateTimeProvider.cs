using SportsPredictor.Application.Common.Interfaces;

namespace SportsPredictor.Infrastructure.Common;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
