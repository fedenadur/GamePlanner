USE GamePlannerDB;
GO

-- MVP stored procedures (idempotent). Tasks 3-6 append their procedures to this script.
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ── Now Playing (per device) ────────────────────────────────────────────
-- Slot-based procedures are replaced by the device-based ones below.
IF OBJECT_ID('sp_UpsertNowPlayingGame', 'P') IS NOT NULL DROP PROCEDURE sp_UpsertNowPlayingGame;
GO
IF OBJECT_ID('sp_DeleteNowPlayingGame', 'P') IS NOT NULL DROP PROCEDURE sp_DeleteNowPlayingGame;
GO

-- Internal helper: applies an outcome (Finished/Dropped/Paused/None) to a journal entry.
CREATE OR ALTER PROCEDURE sp_ApplyJournalOutcome
    @UserId  INT,
    @GameId  INT,
    @Outcome NVARCHAR(10),
    @Rating  TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Outcome = 'None' RETURN;

    IF NOT EXISTS (SELECT 1 FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId)
        INSERT INTO GameJournalEntries (UserId, GameId, Status) VALUES (@UserId, @GameId, 'Backlog');

    UPDATE GameJournalEntries
       SET Status     = @Outcome,
           FinishedAt = CASE WHEN @Outcome = 'Finished' THEN CAST(GETUTCDATE() AS DATE) ELSE FinishedAt END,
           UserRating = CASE WHEN @Outcome = 'Finished' AND @Rating IS NOT NULL THEN @Rating ELSE UserRating END,
           UpdatedAt  = GETUTCDATE()
     WHERE UserId = @UserId AND GameId = @GameId;
END
GO

-- Returns: 0 ok, 1 game not the user's or deleted, 2 slot occupied without outcome,
--          3 game already on another device, 4 invalid outcome/rating
CREATE OR ALTER PROCEDURE sp_StartNowPlaying
    @UserId          INT,
    @Device          NVARCHAR(20),
    @GameId          INT,
    @OutgoingOutcome NVARCHAR(10) = NULL,
    @OutgoingRating  TINYINT      = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @OutgoingOutcome IS NOT NULL AND @OutgoingOutcome NOT IN ('Finished','Dropped','Paused','None') RETURN 4;
    IF @OutgoingRating IS NOT NULL AND (@OutgoingRating < 1 OR @OutgoingRating > 10) RETURN 4;

    DECLARE @Result INT = 0;
    DECLARE @CurrentGameId INT = NULL;

    BEGIN TRY
        BEGIN TRAN;

        IF NOT EXISTS (SELECT 1 FROM Games WHERE Id = @GameId AND UserId = @UserId AND IsActive = 1)
        BEGIN
            SET @Result = 1;
        END
        ELSE
        BEGIN
            SELECT @CurrentGameId = GameId
              FROM NowPlayingGames WITH (UPDLOCK, HOLDLOCK)
             WHERE UserId = @UserId AND Device = @Device;

            IF @CurrentGameId = @GameId
                SET @Result = 0 -- already playing it here: no-op
            ELSE IF @CurrentGameId IS NOT NULL AND @OutgoingOutcome IS NULL
                SET @Result = 2
            ELSE IF EXISTS (SELECT 1 FROM NowPlayingGames WITH (UPDLOCK, HOLDLOCK)
                             WHERE UserId = @UserId AND GameId = @GameId AND Device <> @Device)
                SET @Result = 3
            ELSE
            BEGIN
                IF @CurrentGameId IS NOT NULL
                BEGIN
                    EXEC sp_ApplyJournalOutcome @UserId, @CurrentGameId, @OutgoingOutcome, @OutgoingRating;
                    DELETE FROM NowPlayingGames WHERE UserId = @UserId AND Device = @Device;
                END

                INSERT INTO NowPlayingGames (UserId, GameId, Device, ModifiedAt)
                VALUES (@UserId, @GameId, @Device, GETUTCDATE());

                IF EXISTS (SELECT 1 FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId)
                    UPDATE GameJournalEntries
                       SET ReplayCount = ReplayCount + CASE WHEN Status = 'Finished' THEN 1 ELSE 0 END,
                           Status      = CASE Status WHEN 'Finished' THEN 'Replaying'
                                                     WHEN 'Replaying' THEN 'Replaying'
                                                     ELSE 'Playing' END,
                           StartedAt   = COALESCE(StartedAt, CAST(GETUTCDATE() AS DATE)),
                           UpdatedAt   = GETUTCDATE()
                     WHERE UserId = @UserId AND GameId = @GameId
                ELSE
                    INSERT INTO GameJournalEntries (UserId, GameId, Status, StartedAt)
                    VALUES (@UserId, @GameId, 'Playing', CAST(GETUTCDATE() AS DATE));
            END
        END

        COMMIT TRAN;
        RETURN @Result;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN
            IF ERROR_MESSAGE() LIKE '%UQ_NowPlaying_UserGame%' RETURN 3;
            RETURN 2;
        END;
        THROW;
    END CATCH
END
GO

-- Returns: 0 ok, 1 slot empty, 4 invalid outcome/rating
CREATE OR ALTER PROCEDURE sp_RemoveNowPlaying
    @UserId  INT,
    @Device  NVARCHAR(20),
    @Outcome NVARCHAR(10),
    @Rating  TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF ISNULL(@Outcome, '') NOT IN ('Finished','Dropped','Paused','None') RETURN 4;
    IF @Rating IS NOT NULL AND (@Rating < 1 OR @Rating > 10) RETURN 4;

    DECLARE @CurrentGameId INT = NULL;

    BEGIN TRAN;

    SELECT @CurrentGameId = GameId
      FROM NowPlayingGames WITH (UPDLOCK, HOLDLOCK)
     WHERE UserId = @UserId AND Device = @Device;

    IF @CurrentGameId IS NULL
    BEGIN
        COMMIT TRAN;
        RETURN 1;
    END

    EXEC sp_ApplyJournalOutcome @UserId, @CurrentGameId, @Outcome, @Rating;
    DELETE FROM NowPlayingGames WHERE UserId = @UserId AND Device = @Device;

    COMMIT TRAN;
    RETURN 0;
END
GO

-- ── Journal / Games (redefined) ─────────────────────────────────────────
-- Returns: 0 ok, 1 game not the user's or deleted. A non-playing status frees the Now Playing slot.
CREATE OR ALTER PROCEDURE sp_UpsertGameJournalEntry
    @UserId         INT,
    @GameId         INT,
    @Status         NVARCHAR(20),
    @StartedAt      DATETIME2 = NULL,
    @FinishedAt     DATETIME2 = NULL,
    @LastPlayedAt   DATETIME2 = NULL,
    @UserRating     TINYINT   = NULL,
    @Notes          NVARCHAR(MAX) = NULL,
    @FavoriteMoment NVARCHAR(MAX) = NULL,
    @ReplayCount    INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM Games WHERE Id = @GameId AND UserId = @UserId AND IsActive = 1) RETURN 1;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId)
        UPDATE GameJournalEntries
           SET Status         = @Status,
               StartedAt      = @StartedAt,
               FinishedAt     = @FinishedAt,
               LastPlayedAt   = @LastPlayedAt,
               UserRating     = @UserRating,
               Notes          = @Notes,
               FavoriteMoment = @FavoriteMoment,
               ReplayCount    = @ReplayCount,
               UpdatedAt      = GETUTCDATE()
         WHERE UserId = @UserId AND GameId = @GameId
    ELSE
        INSERT INTO GameJournalEntries
            (UserId, GameId, Status, StartedAt, FinishedAt, LastPlayedAt, UserRating, Notes, FavoriteMoment, ReplayCount)
        VALUES
            (@UserId, @GameId, @Status, @StartedAt, @FinishedAt, @LastPlayedAt, @UserRating, @Notes, @FavoriteMoment, @ReplayCount);

    IF @Status NOT IN ('Playing', 'Replaying')
        DELETE FROM NowPlayingGames WHERE UserId = @UserId AND GameId = @GameId;

    COMMIT TRAN;
    RETURN 0;
END
GO

CREATE OR ALTER PROCEDURE sp_DeleteGame
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Rows INT;

    BEGIN TRAN;

    UPDATE Games
    SET IsActive   = 0,
        DeletedAt  = GETUTCDATE(),
        ModifiedAt = GETUTCDATE()
    WHERE Id = @Id AND IsActive = 1;
    SET @Rows = @@ROWCOUNT;

    DELETE FROM NowPlayingGames WHERE GameId = @Id;

    COMMIT TRAN;

    SELECT @Rows AS RowsAffected;
END
GO
