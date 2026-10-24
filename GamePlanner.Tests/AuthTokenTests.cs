using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GamePlanner.API.Entities;
using GamePlanner.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace GamePlanner.Tests;

/// <summary>
/// Phase 3 JWT contract: tokens live 24 hours, and the real JwtBearer middleware of the in-process host
/// rejects expired, mis-signed, wrong-issuer, wrong-audience, wrong-algorithm, unsigned and exp-less tokens.
/// The valid-token case runs one read-only query against the dev SQL Server.
/// </summary>
public class AuthTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // A user id that does not exist, so the authorized request returns an empty list.
    private const int ProbeUserId = 2147483000;

    private const string OtherSecret = "a-completely-different-test-secret-that-is-also-longer-than-64-chars!!";

    private static string Token(string secret, string issuer, string audience, DateTime? expires, string? algorithm)
    {
        var signing = algorithm is null
            ? null
            : new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), algorithm);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, ProbeUserId.ToString())],
            notBefore: expires is null ? null : expires.Value.AddHours(-24),
            expires: expires,
            signingCredentials: signing);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<HttpResponseMessage> GetGamesAsync(string? token)
    {
        using var client = factory.CreateApiClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/games");
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public void GenerateToken_IssuesTokenValidFor24Hours()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = ApiFactory.Secret,
                ["Jwt:Issuer"] = ApiFactory.Issuer,
                ["Jwt:Audience"] = ApiFactory.Audience
            })
            .Build();

        var service = new AuthService(db: null!, config, NullLogger<AuthService>.Instance);
        var token = service.GenerateToken(new User { Id = ProbeUserId, Email = "probe@gsd.local" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.True(Math.Abs((jwt.ValidTo - DateTime.UtcNow.AddHours(24)).TotalSeconds) < 60);
        Assert.Equal(ApiFactory.Issuer, jwt.Issuer);
        Assert.Contains(ApiFactory.Audience, jwt.Audiences);
    }

    [Fact]
    public async Task ValidToken_Returns200()
    {
        var token = factory.IssueToken(new User { Id = ProbeUserId, Email = "probe@gsd.local" });

        using var response = await GetGamesAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingToken_Returns401()
    {
        using var response = await GetGamesAsync(null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static TheoryData<string> RejectedCases =>
    [
        "expired",
        "wrong-secret",
        "wrong-issuer",
        "wrong-audience",
        "hs512",
        "alg-none",
        "no-exp"
    ];

    private static string BuildRejectedToken(string caseName) => caseName switch
    {
        // Expired by 1 hour: well past the 5-minute default clock tolerance (Phase 3 D-03).
        "expired" => Token(ApiFactory.Secret, ApiFactory.Issuer, ApiFactory.Audience,
            DateTime.UtcNow.AddHours(-1), SecurityAlgorithms.HmacSha256),
        "wrong-secret" => Token(OtherSecret, ApiFactory.Issuer, ApiFactory.Audience,
            DateTime.UtcNow.AddHours(1), SecurityAlgorithms.HmacSha256),
        "wrong-issuer" => Token(ApiFactory.Secret, "EvilIssuer", ApiFactory.Audience,
            DateTime.UtcNow.AddHours(1), SecurityAlgorithms.HmacSha256),
        "wrong-audience" => Token(ApiFactory.Secret, ApiFactory.Issuer, "EvilAudience",
            DateTime.UtcNow.AddHours(1), SecurityAlgorithms.HmacSha256),
        // HS256 is pinned (Phase 3 D-04); a correctly keyed HS512 token must not pass.
        "hs512" => Token(ApiFactory.Secret, ApiFactory.Issuer, ApiFactory.Audience,
            DateTime.UtcNow.AddHours(1), SecurityAlgorithms.HmacSha512),
        "alg-none" => Token(ApiFactory.Secret, ApiFactory.Issuer, ApiFactory.Audience,
            DateTime.UtcNow.AddHours(1), null),
        "no-exp" => Token(ApiFactory.Secret, ApiFactory.Issuer, ApiFactory.Audience,
            null, SecurityAlgorithms.HmacSha256),
        _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null)
    };

    [Theory]
    [MemberData(nameof(RejectedCases))]
    public async Task RejectedToken_Returns401_WithInvalidToken(string caseName)
    {
        using var response = await GetGamesAsync(BuildRejectedToken(caseName));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // invalid_token proves the middleware rejected the token rather than seeing none.
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.ToString().Contains("invalid_token"));
    }

    [Fact]
    public void TestHost_IsHermetic_NoWorkersNoUserSecrets()
    {
        var hosted = factory.Services.GetServices<IHostedService>().ToList();

        Assert.DoesNotContain(hosted, s => s.GetType().Namespace == "GamePlanner.API.Workers");
        Assert.NotEmpty(hosted);
        Assert.Equal("Testing", factory.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);
        Assert.Equal(ApiFactory.Secret, factory.Services.GetRequiredService<IConfiguration>()["Jwt:Secret"]);
    }
}
