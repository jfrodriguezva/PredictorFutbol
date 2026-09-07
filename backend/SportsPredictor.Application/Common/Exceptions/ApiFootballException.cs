using System.Net;

namespace SportsPredictor.Application.Common.Exceptions;

/// <summary>Base exception for any failure talking to API-Football. Never carries the API key.</summary>
public class ApiFootballException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public ApiFootballException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public ApiFootballException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
