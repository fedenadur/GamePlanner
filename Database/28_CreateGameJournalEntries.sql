CREATE TABLE GameJournalEntries (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    UserId          INT NOT NULL,
    GameId          INT NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Backlog',
    StartedAt       DATETIME2 NULL,
    FinishedAt      DATETIME2 NULL,
    LastPlayedAt    DATETIME2 NULL,
    UserRating      TINYINT NULL,
    Notes           NVARCHAR(MAX) NULL,
    FavoriteMoment  NVARCHAR(MAX) NULL,
    ReplayCount     INT NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Journal_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_Journal_Game FOREIGN KEY (GameId) REFERENCES Games(Id),
    CONSTRAINT UQ_Journal_UserGame UNIQUE (UserId, GameId),
    CONSTRAINT CK_Journal_Rating CHECK (UserRating IS NULL OR (UserRating >= 1 AND UserRating <= 10)),
    CONSTRAINT CK_Journal_Status CHECK (Status IN ('Backlog','Playing','Finished','Paused','Dropped','Replaying'))
);
GO

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
         WHERE UserId = @UserId AND GameId = @GameId;
    ELSE
        INSERT INTO GameJournalEntries
            (UserId, GameId, Status, StartedAt, FinishedAt, LastPlayedAt, UserRating, Notes, FavoriteMoment, ReplayCount)
        VALUES
            (@UserId, @GameId, @Status, @StartedAt, @FinishedAt, @LastPlayedAt, @UserRating, @Notes, @FavoriteMoment, @ReplayCount);
END
GO

CREATE OR ALTER PROCEDURE sp_DeleteGameJournalEntry
    @UserId INT,
    @GameId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM GameJournalEntries WHERE UserId = @UserId AND GameId = @GameId;
END
GO
