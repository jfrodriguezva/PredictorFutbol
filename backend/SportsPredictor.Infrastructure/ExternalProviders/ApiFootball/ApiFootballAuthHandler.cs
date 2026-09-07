using Microsoft.Extensions.Options;
using SportsPredictor.Application.Common.Exceptions;

namespace SportsPredictor.Infrastructure.ExternalProviders.ApiFootball;

/// <summary>Adds the x-apisports-key header. Fails fast, before any network call, if no key is configured.</summary>
public sealed class ApiFootballAuthHandler : DelegatingHandler
{
    private const string ApiKeyHeaderName = "x-apisports-key";

    private readonly IOptionsMonitor<ApiFootballOptions> _options;

    public ApiFootballAuthHandler(IOptionsMonitor<ApiFootballOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _options.CurrentValue.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ApiFootballNotConfiguredException();
        }

        request.Headers.Remove(ApiKeyHeaderName);
        request.Headers.Add(ApiKeyHeaderName, apiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
