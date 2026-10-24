using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace GamePlanner.Tests;

/// <summary>
/// Hermetic in-process API host: environment "Testing" (user-secrets never load), test-only secrets,
/// no GamePlanner.API.Workers hosted service, and every IHttpClientFactory client answered by <see cref="Http"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // Test value, not a real secret. 64+ ASCII chars so one key can sign both HS256 and HS512 tokens.
    public const string Secret = "unit-test-only-hs-secret-0123456789abcdefghijklmnopqrstuvwxyzABCDEF";
    public const string Issuer = "GamePlannerAPI";
    public const string Audience = "GamePlannerClient";
    public const string TestSteamKey = "TEST-STEAM-KEY-DO-NOT-LOG";
    public const string TestIgdbClientId = "test-igdb-id";
    public const string TestIgdbSecret = "TEST-IGDB-SECRET-DO-NOT-LOG";

    public RecordingHandler Http { get; } = new();
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("Jwt:Secret", Secret);
        builder.UseSetting("Steam:ApiKey", TestSteamKey);
        builder.UseSetting("IGDB:ClientId", TestIgdbClientId);
        builder.UseSetting("IGDB:ClientSecret", TestIgdbSecret);

        builder.ConfigureLogging(l => l.AddProvider(Logs));

        builder.ConfigureTestServices(services =>
        {
            // Only the app's workers go; the web host's own hosted service starts the TestServer.
            var workers = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                         && d.ImplementationType?.Namespace == "GamePlanner.API.Workers")
                .ToList();
            foreach (var d in workers)
                services.Remove(d);

            services.ConfigureHttpClientDefaults(b => b.ConfigurePrimaryHttpMessageHandler(() => Http));
        });
    }

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    public string IssueToken(User user)
    {
        using var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AuthService>().GenerateToken(user);
    }
}
