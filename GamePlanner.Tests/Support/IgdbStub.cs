using System.Net;
using System.Text;
using GamePlanner.API.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GamePlanner.Tests;

/// <summary>
/// IgdbService wired to a RecordingHandler. The token host answers a fixed token; every api.igdb.com request
/// is answered by <c>route(path, body)</c> as JSON. A null route result answers HTTP 500, which makes
/// IgdbService throw (simulates a failed IGDB call).
/// </summary>
public static class IgdbStub
{
    public static (IgdbService Service, RecordingHandler Handler) Create(Func<string, string, string?> route)
    {
        var handler = new RecordingHandler();
        handler.Responder = request =>
        {
            if (request.RequestUri!.Host == "id.twitch.tv")
                return Json("""{"access_token":"tok","expires_in":3600}""", HttpStatusCode.OK);

            var body = handler.Requests[^1].Body;
            var json = route(request.RequestUri.AbsolutePath, body);
            return json is null
                ? Json("""{"message":"stub failure"}""", HttpStatusCode.InternalServerError)
                : Json(json, HttpStatusCode.OK);
        };

        var service = new IgdbService(
            new StubHttpClientFactory(handler),
            TestDatabase.CreateConfig(),
            NullLogger<IgdbService>.Instance);
        return (service, handler);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
