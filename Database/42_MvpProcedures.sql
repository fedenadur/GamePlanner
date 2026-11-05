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

-- ── User devices and preferences ────────────────────────────────────────
-- @Devices: comma-separated device codes, in display order. Returns 0 = saved,
-- 1 = a device being removed still has a game in Now Playing (nothing changes).
CREATE OR ALTER PROCEDURE sp_SaveUserDevices
    @UserId  INT,
    @Devices NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @New TABLE (Device NVARCHAR(20) NOT NULL PRIMARY KEY, SortOrder INT NOT NULL);
    INSERT INTO @New (Device, SortOrder)
    SELECT v, ROW_NUMBER() OVER (ORDER BY CHARINDEX(',' + v + ',', ',' + @Devices + ',')) - 1
    FROM (SELECT DISTINCT LTRIM(RTRIM(value)) AS v FROM STRING_SPLIT(@Devices, ',')) s
    WHERE v <> '';

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM NowPlayingGames np
               WHERE np.UserId = @UserId
                 AND np.Device <> 'General'
                 AND np.Device NOT IN (SELECT Device FROM @New))
    BEGIN
        ROLLBACK TRAN;
        RETURN 1;
    END

    -- A game on the General slot moves to the first device of a non-empty list.
    UPDATE NowPlayingGames SET Device = (SELECT TOP 1 Device FROM @New ORDER BY SortOrder)
     WHERE UserId = @UserId AND Device = 'General' AND EXISTS (SELECT 1 FROM @New);

    DELETE FROM UserDevices WHERE UserId = @UserId;
    INSERT INTO UserDevices (UserId, Device, SortOrder)
    SELECT @UserId, Device, SortOrder FROM @New;

    COMMIT TRAN;
    RETURN 0;
END
GO

-- Genres come by name separated by '|' (some genre names contain commas). Unknown names are ignored.
CREATE OR ALTER PROCEDURE sp_SaveUserPreferences
    @UserId         INT,
    @MaxGameHours   INT,
    @SinglePlayer   NVARCHAR(10),
    @Narrative      NVARCHAR(10),
    @Combat         NVARCHAR(10),
    @FavoriteGenres NVARCHAR(MAX),
    @AvoidGenres    NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM UserPreferences WHERE UserId = @UserId)
        UPDATE UserPreferences
           SET MaxGameHours = @MaxGameHours, SinglePlayer = @SinglePlayer,
               Narrative = @Narrative, Combat = @Combat
         WHERE UserId = @UserId
    ELSE
        INSERT INTO UserPreferences (UserId, MaxGameHours, SinglePlayer, Narrative, Combat)
        VALUES (@UserId, @MaxGameHours, @SinglePlayer, @Narrative, @Combat);

    DELETE FROM UserPreferenceGenres WHERE UserId = @UserId;

    INSERT INTO UserPreferenceGenres (UserId, GenreId, Kind)
    SELECT @UserId, g.Id, 'Favorite'
    FROM Genres g
    WHERE g.Name IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@FavoriteGenres, '|'));

    INSERT INTO UserPreferenceGenres (UserId, GenreId, Kind)
    SELECT @UserId, g.Id, 'Avoid'
    FROM Genres g
    WHERE g.Name IN (SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@AvoidGenres, '|'))
      AND NOT EXISTS (SELECT 1 FROM UserPreferenceGenres u WHERE u.UserId = @UserId AND u.GenreId = g.Id);

    COMMIT TRAN;
END
GO

-- Redefined: a game that leaves the Journal also leaves Now Playing (only Playing/Replaying games are in it).
CREATE OR ALTER PROCEDURE sp_DeleteGameJournalEntry
    @UserId INT,
    @GameId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;
    DELETE FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId;
    DELETE FROM NowPlayingGames    WHERE UserId = @UserId AND GameId = @GameId;
    COMMIT TRAN;
END
GO

-- ── Play sessions ──────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_InsertPlaySession
    @UserId   INT,
    @GameId   INT,
    @Device   NVARCHAR(20),
    @PlayedOn DATE,
    @Hours    DECIMAL(4,2),
    @Note     NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM Games WHERE Id = @GameId AND UserId = @UserId AND IsActive = 1)
        RETURN -1;

    DECLARE @NewId INT;
    BEGIN TRAN;
    INSERT INTO PlaySessions (UserId, GameId, Device, PlayedOn, Hours, Note)
    VALUES (@UserId, @GameId, @Device, @PlayedOn, @Hours, @Note);
    SET @NewId = SCOPE_IDENTITY();

    IF EXISTS (SELECT 1 FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId)
        UPDATE GameJournalEntries
           SET LastPlayedAt = CASE WHEN LastPlayedAt IS NULL OR LastPlayedAt < @PlayedOn THEN @PlayedOn ELSE LastPlayedAt END,
               UpdatedAt = GETUTCDATE()
         WHERE UserId = @UserId AND GameId = @GameId;
    ELSE
        INSERT INTO GameJournalEntries (UserId, GameId, Status, LastPlayedAt)
        VALUES (@UserId, @GameId, 'Backlog', @PlayedOn);
    COMMIT TRAN;

    RETURN @NewId;
END
GO

CREATE OR ALTER PROCEDURE sp_UpdatePlaySession
    @Id       INT,
    @UserId   INT,
    @Device   NVARCHAR(20),
    @PlayedOn DATE,
    @Hours    DECIMAL(4,2),
    @Note     NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @GameId INT, @Rows INT;
    BEGIN TRAN;
    UPDATE ps
       SET Device = @Device, PlayedOn = @PlayedOn, Hours = @Hours, Note = @Note, ModifiedAt = GETUTCDATE()
      FROM PlaySessions ps
      JOIN Games g ON g.Id = ps.GameId AND g.IsActive = 1
     WHERE ps.Id = @Id AND ps.UserId = @UserId;
    SET @Rows = @@ROWCOUNT;

    IF @Rows > 0
    BEGIN
        SELECT @GameId = GameId FROM PlaySessions WHERE Id = @Id;
        UPDATE GameJournalEntries
           SET LastPlayedAt = (SELECT MAX(PlayedOn) FROM PlaySessions WHERE UserId = @UserId AND GameId = @GameId),
               UpdatedAt = GETUTCDATE()
         WHERE UserId = @UserId AND GameId = @GameId;
    END
    COMMIT TRAN;
    RETURN @Rows;
END
GO

CREATE OR ALTER PROCEDURE sp_DeletePlaySession
    @Id     INT,
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @GameId INT, @Rows INT;
    BEGIN TRAN;
    SELECT @GameId = ps.GameId
      FROM PlaySessions ps
      JOIN Games g ON g.Id = ps.GameId AND g.IsActive = 1
     WHERE ps.Id = @Id AND ps.UserId = @UserId;

    DELETE FROM PlaySessions WHERE Id = @Id AND UserId = @UserId AND GameId = @GameId;
    SET @Rows = @@ROWCOUNT;

    -- Recompute only when sessions remain; otherwise leave LastPlayedAt untouched.
    IF @Rows > 0 AND EXISTS (SELECT 1 FROM PlaySessions WHERE UserId = @UserId AND GameId = @GameId)
        UPDATE GameJournalEntries
           SET LastPlayedAt = (SELECT MAX(PlayedOn) FROM PlaySessions WHERE UserId = @UserId AND GameId = @GameId),
               UpdatedAt = GETUTCDATE()
         WHERE UserId = @UserId AND GameId = @GameId;
    COMMIT TRAN;
    RETURN @Rows;
END
GO

-- ── Library: store, favorite, playable devices ──────────────────────────────
-- @CoverImageUrl: NULL = keep the current cover; '' = clear it.
CREATE OR ALTER PROCEDURE sp_UpdateGame
    @Id               INT,
    @Name             NVARCHAR(100),
    @EstimatedMinutes INT,
    @Store            NVARCHAR(30)  = 'Other',
    @IsFavorite       BIT           = 0,
    @CoverImageUrl    NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET Name             = @Name,
        EstimatedMinutes = @EstimatedMinutes,
        Store            = @Store,
        IsFavorite       = @IsFavorite,
        CoverImageUrl    = CASE WHEN @CoverImageUrl IS NULL THEN CoverImageUrl
                                WHEN @CoverImageUrl = '' THEN NULL
                                ELSE @CoverImageUrl END,
        ModifiedAt       = GETUTCDATE()
    WHERE Id = @Id AND IsActive = 1;
END
GO

-- Returns the number of rows affected (0 = game not the user's or deleted).
CREATE OR ALTER PROCEDURE sp_SetGameFavorite
    @Id         INT,
    @UserId     INT,
    @IsFavorite BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET IsFavorite = @IsFavorite,
        ModifiedAt = GETUTCDATE()
    WHERE Id = @Id AND UserId = @UserId AND IsActive = 1;

    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- @Devices: comma-separated device codes. Empty/NULL = no explicit rows (the store default applies at read time).
CREATE OR ALTER PROCEDURE sp_SetGamePlayableDevices
    @GameId  INT,
    @Devices NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DELETE FROM GamePlayableDevices WHERE GameId = @GameId;
    IF @Devices IS NOT NULL AND LEN(@Devices) > 0
        INSERT INTO GamePlayableDevices (GameId, Device)
        SELECT DISTINCT @GameId, LTRIM(RTRIM(value)) FROM STRING_SPLIT(@Devices, ',') WHERE LTRIM(RTRIM(value)) <> '';
    COMMIT TRAN;
END
GO
