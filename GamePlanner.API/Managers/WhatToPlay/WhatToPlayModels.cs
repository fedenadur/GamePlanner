namespace GamePlanner.API.Managers.WhatToPlay;

public record WhatToPlayCandidate(
    int GameId, string Name, string? CoverImageUrl, string Store,
    IReadOnlyList<string> PlayableDevices, IReadOnlyList<string> Genres,
    IReadOnlyList<string> Themes, IReadOnlyList<string> Perspectives,
    IReadOnlyList<string> Modes, IReadOnlyList<string> Tags,
    string Status, double TotalHours, double? LengthHours, double? CriticRating,
    bool IsFavorite, bool InNowPlaying);

public record WhatToPlayProfile(
    ISet<string> FavoriteGenres, ISet<string> AvoidGenres, int? MaxGameHours,
    string SinglePlayer, string Narrative, string Combat,
    ISet<string> LikedGenres, ISet<string> DislikedGenres,
    IReadOnlyDictionary<string, int> GenrePlayCounts);
