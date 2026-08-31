using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GamePlanner.API.Services;

public class AuthService(GamePlannerDbContext db, IConfiguration config, ILogger<AuthService> logger)
{
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        if (await db.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            return null; // email already taken

        var user = new User
        {
            Email        = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Username     = request.Username?.Trim()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new AuthResponse
        {
            Token    = GenerateToken(user),
            Email    = user.Email,
            Username = user.Username
        };
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive);

        if (user is null || user.PasswordHash is null)
        {
            logger.LogWarning("Failed login attempt for {Email}: {Reason}", request.Email, "user not found or inactive");
            return null;
        }
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}: {Reason}", request.Email, "invalid password");
            return null;
        }

        return new AuthResponse
        {
            Token    = GenerateToken(user),
            Email    = user.Email,
            Username = user.Username
        };
    }

    public string GenerateToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };

        if (!string.IsNullOrEmpty(user.SteamId))
            claims.Add(new Claim("steamId", user.SteamId));

        var token = new JwtSecurityToken(
            issuer:   config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims:   claims.ToArray(),
            expires:  DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
