using GamePlanner.API.Domain;
using GamePlanner.API.Entities;

namespace GamePlanner.API.Mapping;

public static class GameMapping
{
    public static GameDto ToDto(this Game game) => game.ToDto(JournalStatuses.Backlog, 0, 0, null);

    public static GameDto ToDto(this Game game, string status, double loggedHours, int sessionCount, double? hltbMainStory) => new()
    {
        Id             = game.Id,
        Name           = game.Name,
        EstimatedMinutes = game.EstimatedMinutes,
        Genres             = game.Genres.Select(g => g.Name).ToList(),
        Tags               = game.Tags.Select(t => t.Name).ToList(),
        Themes             = game.Themes.Select(t => t.Name).ToList(),
        PlayerPerspectives = game.PlayerPerspectives.Select(p => p.Name).ToList(),
        GameModes          = game.GameModes.Select(m => m.Name).ToList(),
        CoverImageUrl      = game.CoverImageUrl,
        PlaytimeHours      = game.PlaytimeHours,
        Source             = game.Source,
        Store              = game.Store,
        IsFavorite         = game.IsFavorite,
        PlayableDevices    = game.PlayableDevices.Count > 0
                             ? game.PlayableDevices.Select(d => d.Device).Order().ToList()
                             : Stores.DefaultDevices(game.Store).ToList(),
        Status             = status,
        LoggedHours        = loggedHours,
        SessionCount       = sessionCount,
        TotalHours         = PlaytimeCalculator.TotalHours(game.PlaytimeHours, loggedHours),
        ReleaseYear        = game.FirstReleaseDate?.Year,
        LengthHours        = GameLength.Hours(game.SteamAppId, game.EstimatedMinutes, hltbMainStory)
    };
}
