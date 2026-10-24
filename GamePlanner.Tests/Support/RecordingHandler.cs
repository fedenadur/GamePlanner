namespace GamePlanner.Tests;

/// <summary>
/// Scriptable outbound HTTP stub. Records every request (method, URI, headers, content type, body)
/// and answers through <see cref="Responder"/>; an exception thrown by the responder propagates
/// unchanged, which is how tests simulate a network failure.
/// </summary>
public sealed class RecordingHandler : HttpMessageHandler
{
    public sealed record Seen(
        HttpMethod Method,
        Uri Uri,
        IReadOnlyDictionary<string, string> Headers,
        string? ContentType,
        string Body);

    private readonly object _gate = new();
    private readonly List<Seen> _requests = [];

    public IReadOnlyList<Seen> Requests
    {
        get
        {
            lock (_gate) return _requests.ToArray();
        }
    }

    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = DefaultResponder;

    public void Reset()
    {
        lock (_gate) _requests.Clear();
        Responder = DefaultResponder;
    }

    private static HttpResponseMessage DefaultResponder(HttpRequestMessage _) => new(System.Net.HttpStatusCode.OK);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Content is disposed after send, so read the body before invoking the responder.
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
        var contentType = request.Content?.Headers.ContentType?.MediaType;

        lock (_gate)
            _requests.Add(new Seen(request.Method, request.RequestUri!, headers, contentType, body));

        return Responder(request);
    }
}
