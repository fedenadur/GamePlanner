namespace GamePlanner.Tests;

/// <summary>
/// IHttpClientFactory that hands out a fresh HttpClient bound to the given handler on every call,
/// mirroring how IgdbService / SteamService call CreateClient() per request.
/// </summary>
public sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
