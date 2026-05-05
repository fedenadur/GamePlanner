USE GamePlannerDB;
GO

-- The original index enforces SteamAppId uniqueness globally across all users,
-- which blocks two different users from having the same Steam game.
-- Replace it with a per-user composite index.

DROP INDEX IF EXISTS UX_Games_SteamAppId ON Games;
GO

CREATE UNIQUE NONCLUSTERED INDEX UX_Games_SteamAppId_PerUser
    ON Games (SteamAppId, UserId)
    WHERE SteamAppId IS NOT NULL AND UserId IS NOT NULL;
GO
