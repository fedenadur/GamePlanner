-- =============================================
-- 34 - Fix sp_UpsertIGDBGame to scope by UserId
-- =============================================

USE GamePlannerDB;
GO

-- UPDATE-only procedure (D-02): scoped by (IGDBId, UserId). The drafted row-creating
-- ELSE/INSERT branch is removed entirely -- a game must only ever be created through
-- sp_InsertGame, never through this orphaned sync-only procedure. With no matching row
-- this is now a no-op, which is the intended behaviour. Still writes the dormant
-- Games.UpdatedAt column (D-04): that column's fate is out of scope for this fix.
--
-- ANSI_NULLS/QUOTED_IDENTIFIER must be explicitly ON at CREATE time: Games carries
-- filtered indexes (IX_Games_IGDBId, UX_Games_SteamAppId_PerUser), and SQL Server checks
-- the SET options baked into the procedure at creation, not the caller's session, before
-- allowing any UPDATE against a table with a filtered index. sqlcmd defaults to
-- QUOTED_IDENTIFIER OFF, unlike SSMS (same fix as 36_AddAuditColumns_ModifiedAt_DeletedAt.sql).
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE sp_UpsertIGDBGame
    @IGDBId             BIGINT,
    @Name               NVARCHAR(100),
    @IGDBGenres         NVARCHAR(300),
    @FirstReleaseDate   DATETIME2,
    @UpdatedAt          DATETIME2,
    @UserId             INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Games
    SET Name             = @Name,
        IGDBGenres       = @IGDBGenres,
        FirstReleaseDate = @FirstReleaseDate,
        UpdatedAt        = @UpdatedAt
    WHERE IGDBId = @IGDBId AND UserId = @UserId;
END
GO
