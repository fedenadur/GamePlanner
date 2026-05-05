using GamePlanner.API.Context;
using GamePlanner.API.Domain;
using GamePlanner.API.Mapping;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Managers;

public class SessionManager(GamePlannerDbContext db)
{
    public async Task<SessionRecommendation> GetRecommendationAsync(SessionRequest request)
    {
        var mood = request.Mood.Trim().ToLower();

        var candidates = await db.Games
            .Where(g => g.IsActive && g.EstimatedMinutes <= request.AvailableMinutes)
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .ToListAsync();

        var moodMatches = candidates
            .Where(g => g.Tags.Any(t => t.Name.ToLower() == mood))
            .ToList();

        if (moodMatches.Count > 0)
        {
            var pick = moodMatches[Random.Shared.Next(moodMatches.Count)];
            return new SessionRecommendation
            {
                Game = pick.ToDto(),
                IsExactMatch = true,
                Message = $"Perfect match for your {mood} mood!"
            };
        }

        if (candidates.Count > 0)
        {
            var pick = candidates[Random.Shared.Next(candidates.Count)];
            return new SessionRecommendation
            {
                Game = pick.ToDto(),
                IsExactMatch = false,
                Message = $"No exact match for \"{mood}\", but this fits your available time."
            };
        }

        return new SessionRecommendation
        {
            Game = null,
            IsExactMatch = false,
            Message = "No games found for your selection. Try adding more games!"
        };
    }
}
