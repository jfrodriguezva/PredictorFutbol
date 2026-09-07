namespace SportsPredictor.Application.Common.Exceptions;

/// <summary>Thrown when a requested entity does not exist locally.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.")
    {
    }
}
