using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using GamePlanner.API.Entities;

namespace GamePlanner.Tests;

public class AdminAuthorizationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const int ProbeUserId = 2147483000;

    private HttpClient ClientFor(bool isAdmin)
    {
        var token = factory.IssueToken(new User { Id = ProbeUserId, Email = "admin-probe@test.local", IsAdmin = isAdmin });
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Workers_NonAdminToken_Returns403()
    {
        var response = await ClientFor(isAdmin: false).GetAsync("/api/worker");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sync_NonAdminToken_Returns403()
    {
        var response = await ClientFor(isAdmin: false).PostAsync("/api/sync/run", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Workers_AdminToken_Returns200()
    {
        var response = await ClientFor(isAdmin: true).GetAsync("/api/worker");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void GenerateToken_AdminUser_HasRoleClaim()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            factory.IssueToken(new User { Id = 1, Email = "a@test.local", IsAdmin = true }));
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_NonAdminUser_HasNoRoleClaim()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            factory.IssueToken(new User { Id = 1, Email = "a@test.local" }));
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Role);
    }
}
