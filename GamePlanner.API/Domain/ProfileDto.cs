namespace GamePlanner.API.Domain;

public class ProfileDto
{
    public string? Email           { get; set; }
    public string? DisplayName     { get; set; }
    public string? ProfileImageUrl { get; set; }
    public bool    IsSteamUser     { get; set; }
}

public class UpdateProfileRequest
{
    public string? DisplayName     { get; set; }
    public string? ProfileImageUrl { get; set; }
}
