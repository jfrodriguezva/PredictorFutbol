namespace SportsPredictor.Infrastructure.Tests.ExternalProviders.ApiFootball;

/// <summary>A scriptable HttpMessageHandler for testing DelegatingHandler pipelines without real network calls.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static FakeHttpMessageHandler Sequence(params HttpResponseMessage[] responses)
    {
        var index = 0;
        return new FakeHttpMessageHandler(_ =>
        {
            var response = responses[Math.Min(index, responses.Length - 1)];
            index++;
            return response;
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
