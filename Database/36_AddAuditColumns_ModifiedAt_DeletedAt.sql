USE GamePlannerDB;
GO

-- Games.ModifiedAt: SQL-side "last modified" audit stamp, distinct from the
-- dormant IGDB-sync-era Games.UpdatedAt column, which is left untouched (D-04/D-09).
IF COL_LENGTH('Games', 'ModifiedAt') IS NULL
BEGIN
    ALTER TABLE Games ADD ModifiedAt DATETIME2 NULL;
END
GO

-- Games.DeletedAt: soft-delete audit stamp (AUDIT-02).
IF COL_LENGTH('Games', 'DeletedAt') IS NULL
BEGIN
    ALTER TABLE Games ADD DeletedAt DATETIME2 NULL;
END
GO

-- NowPlayingGames.ModifiedAt: SQL-side "last modified" audit stamp (AUDIT-01, D-11).
IF COL_LENGTH('NowPlayingGames', 'ModifiedAt') IS NULL
BEGIN
    ALTER TABLE NowPlayingGames ADD ModifiedAt DATETIME2 NULL;
END
GO

-- No backfill: pre-existing rows read NULL for both new columns above. Only rows
-- touched after this migration runs get a timestamp, stamped exclusively in T-SQL.

-- sp_UpdateGame: unchanged signature and WHERE clause, adds ModifiedAt = GETUTCDATE() (D-10).
-- ANSI_NULLS/QUOTED_IDENTIFIER must be explicitly ON at CREATE time: Games carries filtered
-- indexes (IX_Games_IGDBId, UX_Games_SteamAppId_PerUser), and SQL Server checks the SET options
-- baked into the procedure at creation, not the caller's session, before allowing any UPDATE
-- against a table with a filtered index. sqlcmd defaults to QUOTED_IDENTIFIER OFF, unlike SSMS.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE sp_UpdateGame
    @Id               INT,
    @Name             NVARCHAR(100),
    @EstimatedMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET Name             = @Name,
        EstimatedMinutes = @EstimatedMinutes,
        ModifiedAt       = GETUTCDATE()
    WHERE Id = @Id AND IsActive = 1;
END
GO

-- sp_DeleteGame: unchanged signature, adds DeletedAt/ModifiedAt stamps (D-06) and an
-- IsActive = 1 guard on the WHERE clause so a repeat delete is a zero-row no-op instead
-- of overwriting the original DeletedAt (AUDIT-02 idempotency). Also fixes a pre-existing
-- bug: with SET NOCOUNT ON, Dapper's ExecuteAsync always returns -1 for a bare UPDATE, so
-- GameManager.DeleteAsync's "rows > 0" check has always evaluated false. Selecting
-- @@ROWCOUNT explicitly gives the caller a real affected-row count without dropping the
-- NOCOUNT convention used by every other proc in this codebase.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE sp_DeleteGame
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET IsActive   = 0,
        DeletedAt  = GETUTCDATE(),
        ModifiedAt = GETUTCDATE()
    WHERE Id = @Id AND IsActive = 1;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- sp_UpsertNowPlayingGame: unchanged signature and IF EXISTS shape, stamps ModifiedAt
-- on both the update branch and the insert branch (D-11). ANSI_NULLS/QUOTED_IDENTIFIER
-- pinned ON for the same reason as sp_UpdateGame above, so this proc stays safe if a
-- filtered index is ever added to NowPlayingGames.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE sp_UpsertNowPlayingGame
    @UserId INT,
    @GameId INT,
    @Slot   INT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM NowPlayingGames WHERE UserId = @UserId AND Slot = @Slot)
        UPDATE NowPlayingGames
           SET GameId     = @GameId,
               AddedAt    = GETUTCDATE(),
               ModifiedAt = GETUTCDATE()
         WHERE UserId = @UserId AND Slot = @Slot;
    ELSE
        INSERT INTO NowPlayingGames (UserId, GameId, Slot, ModifiedAt)
        VALUES (@UserId, @GameId, @Slot, GETUTCDATE());
END
GO
