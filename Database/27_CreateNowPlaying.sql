-- NowPlayingGames: up to 3 games per user, one per slot (0, 1, 2)
CREATE TABLE NowPlayingGames (
    Id      INT IDENTITY(1,1) PRIMARY KEY,
    UserId  INT NOT NULL,
    GameId  INT NOT NULL,
    Slot    INT NOT NULL,
    AddedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_NowPlaying_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_NowPlaying_Game FOREIGN KEY (GameId) REFERENCES Games(Id),
    CONSTRAINT UQ_NowPlaying_UserSlot UNIQUE (UserId, Slot)
);
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
           SET GameId  = @GameId,
               AddedAt = GETUTCDATE()
         WHERE UserId = @UserId AND Slot = @Slot;
    ELSE
        INSERT INTO NowPlayingGames (UserId, GameId, Slot)
        VALUES (@UserId, @GameId, @Slot);
END
GO

CREATE OR ALTER PROCEDURE sp_DeleteNowPlayingGame
    @UserId INT,
    @Slot   INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM NowPlayingGames WHERE UserId = @UserId AND Slot = @Slot;
END
GO
