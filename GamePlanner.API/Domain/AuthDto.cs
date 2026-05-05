namespace GamePlanner.API.Domain;

public class RegisterRequest
{
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Username { get; set; }
}

public class LoginRequest
{
    public string Email    { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string  Token    { get; set; } = string.Empty;
    public string? Email    { get; set; }
    public string? Username { get; set; }
}

public class SteamExchangeRequest
{
    public string Code { get; set; } = string.Empty;
}
