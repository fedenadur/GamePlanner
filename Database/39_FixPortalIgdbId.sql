-- ============================================================================
-- 39_FixPortalIgdbId.sql
-- Phase 06 / plan 06-02 / D-05, D-06: correct the wrong IGDBId on "Portal"
-- (Games.Id 107693, UserId 2, SteamAppId 400): 14546 -> 71.
-- Authored 2026-11-05. Applied MANUALLY in SSMS by the owner (project convention).
--
-- Lookup evidence (direct read-only IGDB v4 queries, 2026-11-05):
--   Steam AppId 400 -> external_games (uid "400", external_game_source = 1) -> game 71
--     [{"id":15156,"game":71,"name":"Portal","uid":"400","external_game_source":1}]
--   71    = "Portal", first_release_date 2007-10-10, developer Valve (publisher Electronic Arts)
--   14546 = "Portal", first_release_date 1986-11-01, developer Nexa Corporation, publisher Activision
--           (the 1986 text adventure currently stored on the row)
--   The app's SearchGameAsync name search cannot surface 71 for "Portal" (it ranks 8th),
--   which is how the wrong first hit was stored in the first place.
--   The stored cover "coaanx" belongs to neither game (71 uses coay61, 14546 uses co4i78).
--
-- CONVENTION EXEMPTION: this one-off data correction uses raw IF EXISTS-guarded UPDATE/DELETE
-- statements instead of a stored procedure. No stored procedure is added because a single-use SP
-- is an unnecessary abstraction, and no existing SP performs this change (sp_UpsertIGDBGame
-- cannot change IGDBId; sp_UpdateGameCover stamps LastImageSyncDate rather than clearing it).
-- Approved by the owner at the 06-02 decision checkpoint before apply.
--
-- Effect: IGDBId set to 71; IGDB-derived scalars cleared (FirstReleaseDate, AggregatedRating,
-- AggregatedRatingCount, CoverImageUrl, IGDBUpdatedAt, LastIGDBSyncAt, LastImageSyncDate);
-- IGDB-derived junction rows (GameGenres, GameThemes, GamePlayerPerspectives,
-- GameModesRelation) and GameImages rows deleted. GameTags is kept per D-06 (TagId 15 "Action"),
-- so after the re-sync Tags still reads only "Action". That is expected, not a bug.
-- After apply, the plan 06-05 restart runs GameSyncWorker and GameImageSyncWorker, which
-- re-derive the row from IGDB 71. Screenshots are fetched before the cover in the same run.
-- Idempotent / guarded: the script changes nothing unless the row is exactly
-- (Id 107693, UserId 2, IGDBId 14546), so a second run, or a run on a changed row, is a no-op.
--
-- REVERSAL: (run manually if ever needed; values captured live 2026-11-05 before the fix)
-- Removes the re-derived rows first, then restores the captured values. GameTags is untouched.
-- Restored GameImages rows get NEW identity Ids (4464 and 4467 are not reused).
--
-- REVERSAL-BEGIN
--   DELETE FROM GameGenres WHERE GameId = 107693;
--   DELETE FROM GameThemes WHERE GameId = 107693;
--   DELETE FROM GamePlayerPerspectives WHERE GameId = 107693;
--   DELETE FROM GameModesRelation WHERE GameId = 107693;
--   DELETE FROM GameImages WHERE GameId = 107693;
--   UPDATE Games SET IGDBId = 14546, FirstReleaseDate = '1986-11-01T00:00:00', AggregatedRating = 80.0, AggregatedRatingCount = 0, CoverImageUrl = 'https://images.igdb.com/igdb/image/upload/t_cover_big/coaanx.jpg', IGDBUpdatedAt = '2026-08-28T02:26:32', LastIGDBSyncAt = '2026-08-30T00:58:28.0118486', LastImageSyncDate = '2026-11-05T16:09:38.143', ModifiedAt = NULL WHERE Id = 107693 AND UserId = 2;
--   INSERT INTO GameGenres (GameId, GenreId) VALUES (107693, 1), (107693, 3), (107693, 7);
--   INSERT INTO GameThemes (GameId, ThemeId) VALUES (107693, 1);
--   INSERT INTO GamePlayerPerspectives (GameId, PlayerPerspectiveId) VALUES (107693, 1);
--   INSERT INTO GameModesRelation (GameId, GameModeId) VALUES (107693, 3);
--   INSERT INTO GameImages (GameId, IgdbImageId, ImageType, Url, Width, Height, SyncedAt) VALUES (107693, 'h1sxo1adkfjzwpksozfy', 'screenshot', 'https://images.igdb.com/igdb/image/upload/t_screenshot_big/h1sxo1adkfjzwpksozfy.jpg', 320, 256, '2026-11-05T16:09:37.5600000'), (107693, 'xx33ndsquzvhr2i0quao', 'screenshot', 'https://images.igdb.com/igdb/image/upload/t_screenshot_big/xx33ndsquzvhr2i0quao.jpg', 320, 200, '2026-11-05T16:09:37.8700000');
-- REVERSAL-END
-- ============================================================================
USE GamePlannerDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

-- Portal (107693): 14546 -> 71
IF EXISTS (SELECT 1 FROM Games WHERE Id = 107693 AND UserId = 2 AND IGDBId = 14546)
BEGIN
    DELETE FROM GameGenres             WHERE GameId = 107693;
    DELETE FROM GameThemes             WHERE GameId = 107693;
    DELETE FROM GamePlayerPerspectives WHERE GameId = 107693;
    DELETE FROM GameModesRelation      WHERE GameId = 107693;
    DELETE FROM GameImages             WHERE GameId = 107693;

    UPDATE Games
    SET IGDBId = 71,
        FirstReleaseDate = NULL,
        AggregatedRating = NULL,
        AggregatedRatingCount = NULL,
        CoverImageUrl = NULL,
        IGDBUpdatedAt = NULL,
        LastIGDBSyncAt = NULL,
        LastImageSyncDate = NULL,
        ModifiedAt = GETUTCDATE()
    WHERE Id = 107693 AND UserId = 2 AND IGDBId = 14546;
END

COMMIT TRANSACTION;
GO
