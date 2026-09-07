namespace SportsPredictor.Application.Common.Exceptions;

/// <summary>Thrown when a call to the Python ML service (ml/) fails.</summary>
public sealed class MlServiceException : Exception
{
    public MlServiceException(string message) : base(message)
    {
    }

    public MlServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
