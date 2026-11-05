using System.Security.Claims;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProfileController(GamePlannerDbContext db, UserSettingsManager settings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user is null) return NotFound();

        return Ok(new ProfileDto
        {
            Email           = user.Email,
            DisplayName     = user.DisplayName ?? user.Username,
            ProfileImageUrl = user.ProfileImageUrl,
            IsSteamUser     = user.SteamId is not null
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync(ct);
        if (user is null) return NotFound();

        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length == 0 || name.Length > 100)
                return BadRequest("DisplayName must be between 1 and 100 characters.");
            user.DisplayName = name;
        }

        if (request.ProfileImageUrl is not null)
        {
            var url = request.ProfileImageUrl.Trim();
            if (url.Length > 0 && !Uri.TryCreate(url, UriKind.Absolute, out _))
                return BadRequest("ProfileImageUrl must be a valid URL.");
            user.ProfileImageUrl = url.Length > 0 ? url : null;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new ProfileDto
        {
            Email           = user.Email,
            DisplayName     = user.DisplayName ?? user.Username,
            ProfileImageUrl = user.ProfileImageUrl,
            IsSteamUser     = user.SteamId is not null
        });
    }

    // ── Devices & preferences ────────────────────────────────
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(new SaveDevicesRequest { Devices = await settings.GetDevicesAsync(userId) });
    }

    [HttpPut("devices")]
    public async Task<IActionResult> SaveDevices([FromBody] SaveDevicesRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await settings.SaveDevicesAsync(userId, request.Devices) switch
        {
            SaveDevicesResult.Ok          => Ok(new SaveDevicesRequest { Devices = request.Devices }),
            SaveDevicesResult.DeviceInUse => Conflict(new { code = "device_in_use" }),
            _                             => BadRequest("Invalid or duplicate device.")
        };
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return Ok(await settings.GetPreferencesAsync(userId));
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> SavePreferences([FromBody] UserPreferencesDto request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await settings.SavePreferencesAsync(userId, request))
            return BadRequest("Invalid preferences.");
        return Ok(await settings.GetPreferencesAsync(userId));
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private async Task<Entities.User?> GetCurrentUserAsync(CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var userId)) return null;
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
    }
}
