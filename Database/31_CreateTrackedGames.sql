-- TrackedGames: games (from IGDB upcoming pool) the user wants to follow
CREATE TABLE TrackedGames (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT NOT NULL,
    IGDBId    BIGINT NOT NULL,
    TrackedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_TrackedGames_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT UQ_TrackedGames_UserIgdb UNIQUE (UserId, IGDBId)
);
GO

CREATE INDEX IX_TrackedGames_User ON TrackedGames(UserId);
GO

CREATE OR ALTER PROCEDURE sp_InsertTrackedGame
    @UserId INT,
    @IGDBId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM TrackedGames WHERE UserId = @UserId AND IGDBId = @IGDBId)
        INSERT INTO TrackedGames (UserId, IGDBId)
        VALUES (@UserId, @IGDBId);
END
GO

CREATE OR ALTER PROCEDURE sp_DeleteTrackedGame
    @UserId INT,
    @IGDBId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM TrackedGames WHERE UserId = @UserId AND IGDBId = @IGDBId;
END
GO
