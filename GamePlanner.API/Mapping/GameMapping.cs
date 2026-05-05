using GamePlanner.API.Domain;
using GamePlanner.API.Entities;

namespace GamePlanner.API.Mapping;

public static class GameMapping
{
    public static GameDto ToDto(this Game game) => new()
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
        Source             = game.Source
    };
}
