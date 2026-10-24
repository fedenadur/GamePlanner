using GamePlanner.API.Domain;
using GamePlanner.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GamePlanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService auth, SteamAuthService steamAuth, SteamTokenStore steamTokenStore, IConfiguration config) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        if (!IsValidEmail(request.Email))
            return BadRequest("Invalid email format.");

        if (request.Password.Length < 8 || request.Password.Length > 72)
            return BadRequest("Password must be between 8 and 72 characters.");

        var result = await auth.RegisterAsync(request);
        if (result is null)
            return BadRequest("Registration failed. Please try again.");

        return Ok(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        var result = await auth.LoginAsync(request);
        if (result is null)
            return Unauthorized("Invalid email or password.");

        return Ok(result);
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new System.Net.Mail.MailAddress(email); return true; }
        catch { return false; }
    }

    private string SteamCallbackUrl => $"{config["App:BaseUrl"]}/api/auth/steam/callback";

    [HttpGet("steam")]
    public IActionResult SteamLogin()
    {
        var redirectUrl = steamAuth.BuildRedirectUrl(SteamCallbackUrl);
        return Redirect(redirectUrl);
    }

    [HttpGet("steam/callback")]
    public async Task<IActionResult> SteamCallback()
    {
        var result = await steamAuth.HandleCallbackAsync(Request.Query, SteamCallbackUrl);
        if (result is null)
            return Redirect($"{config["App:FrontendUrl"]}/login?error=steam_failed");

        // Store the token server-side; send only a short-lived opaque code in the URL
        var code = steamTokenStore.Store(result);
        return Redirect($"{config["App:FrontendUrl"]}/login?code={code}");
    }

    [HttpPost("steam/exchange")]
    public IActionResult SteamExchange([FromBody] SteamExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("Code is required.");

        var result = steamTokenStore.Redeem(request.Code);
        if (result is null)
            return BadRequest("Invalid or expired code.");

        return Ok(result);
    }
}
