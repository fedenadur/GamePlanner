-- ============================================================================
-- 38_FixHalfLifeIgdbId.sql
-- Phase 05 / plan 05-02 / D-08: correct the same-user IGDBId collision between
-- "Half-Life" (Games.Id 107683) and "Half-Life 2" (Games.Id 107687), UserId 2.
-- Authored 2026-10-24. Applied MANUALLY in SSMS by the user (project convention).
--
-- Duplicate scan (active rows, GROUP BY UserId, IGDBId HAVING COUNT(DISTINCT Name) > 1),
-- run for UserId 2 and for all users immediately before authoring -> exactly one group:
--   UserId 2, IGDBId 267639 -> 107683:Half-Life ; 107687:Half-Life 2
-- Both rows carry IGDB 267639 = "Half-Life 2: MMod - Half-Life 2: Update" (2018-12-25),
-- which matches neither title, so BOTH rows are corrected.
--
-- Lookup evidence (exact title + original release year):
--   Half-Life 2 -> 233 "Half-Life 2", 2004-11-16
--     (app GET /api/games/igdb/search?name=Half-Life 2, as the UAT user)
--   Half-Life   -> 231 "Half-Life", 1998-11-19
--     (NOT returned by the app's search, which is capped at 5 results and only
--      surfaced Half-Life 401474/2001 and 228891/no date; confirmed by a direct IGDB
--      id query, id = 231 -> name "Half-Life", first_release_date 1998-11-19)
--
-- CONVENTION EXEMPTION: this one-off data correction uses raw IF EXISTS-guarded UPDATE/DELETE
-- statements instead of a stored procedure. No stored procedure is added because a single-use SP
-- is an unnecessary abstraction, and no existing SP performs this change (sp_UpsertIGDBGame
-- cannot change IGDBId; sp_UpdateGameCover stamps LastImageSyncDate rather than clearing it).
-- Approved by the user at the 05-02 decision checkpoint before apply.
--
-- Effect per corrected row: IGDBId set to the correct value; IGDB-derived scalars cleared
-- (FirstReleaseDate, AggregatedRating*, CoverImageUrl, IGDBUpdatedAt, LastIGDBSyncAt,
-- LastImageSyncDate); IGDB-derived junction rows (GameGenres, GameThemes,
-- GamePlayerPerspectives, GameModesRelation) and GameImages rows deleted. GameTags is left
-- alone (also written by user edits; both rows hold zero tags). A normal non-force run of
-- GameSyncWorker and GameImageSyncWorker re-derives everything from the correct IGDB entry.
-- Idempotent: each block is guarded on the row's pre-fix (Id, UserId, IGDBId).
--
-- REVERSAL: (run manually if ever needed; values captured 2026-10-24 before the fix)
-- Games had no GameTags, no GameThemes and no GameImages rows for either game.
-- After a re-sync the re-derived junction rows must be removed first, then restore:
--
--   DELETE FROM GameGenres             WHERE GameId IN (107683,107687);
--   DELETE FROM GamePlayerPerspectives WHERE GameId IN (107683,107687);
--   DELETE FROM GameModesRelation      WHERE GameId IN (107683,107687);
--   DELETE FROM GameThemes             WHERE GameId IN (107683,107687);
--   DELETE FROM GameImages             WHERE GameId IN (107683,107687);
--   UPDATE Games SET IGDBId = 267639, FirstReleaseDate = '2018-12-25T00:00:00',
--       CoverImageUrl = 'https://images.igdb.com/igdb/image/upload/t_cover_big/co779u.jpg',
--       AggregatedRating = NULL, AggregatedRatingCount = NULL,
--       IGDBUpdatedAt = '2026-09-28T15:56:43',
--       LastIGDBSyncAt = '2026-10-24T15:54:45.6978118',
--       LastImageSyncDate = '2026-10-24T15:55:00.760', ModifiedAt = NULL
--     WHERE Id = 107683 AND UserId = 2;
--   UPDATE Games SET IGDBId = 267639, FirstReleaseDate = '2018-12-25T00:00:00',
--       CoverImageUrl = 'https://images.igdb.com/igdb/image/upload/t_cover_big/co779u.jpg',
--       AggregatedRating = NULL, AggregatedRatingCount = NULL,
--       IGDBUpdatedAt = '2026-09-28T15:56:43',
--       LastIGDBSyncAt = '2026-10-24T15:54:48.1586132',
--       LastImageSyncDate = '2026-10-24T15:55:00.760', ModifiedAt = NULL
--     WHERE Id = 107687 AND UserId = 2;
--   INSERT INTO GameGenres (GameId, GenreId) VALUES (107683, 3), (107687, 3);
--   INSERT INTO GamePlayerPerspectives (GameId, PlayerPerspectiveId) VALUES (107683, 1), (107687, 1);
--   INSERT INTO GameModesRelation (GameId, GameModeId) VALUES (107683, 3), (107687, 3);
-- ============================================================================
USE GamePlannerDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- Half-Life (107683): 267639 -> 231
IF EXISTS (SELECT 1 FROM Games WHERE Id = 107683 AND UserId = 2 AND IGDBId = 267639)
BEGIN
    DELETE FROM GameGenres             WHERE GameId = 107683;
    DELETE FROM GameThemes             WHERE GameId = 107683;
    DELETE FROM GamePlayerPerspectives WHERE GameId = 107683;
    DELETE FROM GameModesRelation      WHERE GameId = 107683;
    DELETE FROM GameImages             WHERE GameId = 107683;

    UPDATE Games
    SET IGDBId = 231,
        FirstReleaseDate = NULL,
        AggregatedRating = NULL,
        AggregatedRatingCount = NULL,
        CoverImageUrl = NULL,
        IGDBUpdatedAt = NULL,
        LastIGDBSyncAt = NULL,
        LastImageSyncDate = NULL,
        ModifiedAt = GETUTCDATE()
    WHERE Id = 107683 AND UserId = 2 AND IGDBId = 267639;
END

-- Half-Life 2 (107687): 267639 -> 233
IF EXISTS (SELECT 1 FROM Games WHERE Id = 107687 AND UserId = 2 AND IGDBId = 267639)
BEGIN
    DELETE FROM GameGenres             WHERE GameId = 107687;
    DELETE FROM GameThemes             WHERE GameId = 107687;
    DELETE FROM GamePlayerPerspectives WHERE GameId = 107687;
    DELETE FROM GameModesRelation      WHERE GameId = 107687;
    DELETE FROM GameImages             WHERE GameId = 107687;

    UPDATE Games
    SET IGDBId = 233,
        FirstReleaseDate = NULL,
        AggregatedRating = NULL,
        AggregatedRatingCount = NULL,
        CoverImageUrl = NULL,
        IGDBUpdatedAt = NULL,
        LastIGDBSyncAt = NULL,
        LastImageSyncDate = NULL,
        ModifiedAt = GETUTCDATE()
    WHERE Id = 107687 AND UserId = 2 AND IGDBId = 267639;
END

COMMIT TRANSACTION;
GO
