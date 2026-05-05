USE GamePlannerDB;
GO

CREATE OR ALTER PROCEDURE sp_InsertSteamGame
    @Name          NVARCHAR(100),
    @SteamAppId    INT,
    @PlaytimeHours FLOAT,
    @UserId        INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @UserId IS NULL
        THROW 50001, 'UserId is required for Steam game insert.', 1;

    INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags, SteamAppId, PlaytimeHours, Source, UserId)
    VALUES (@Name, '', 60, '', @SteamAppId, @PlaytimeHours, 'Steam', @UserId);
END
GO

CREATE OR ALTER PROCEDURE sp_UpdateGameFromSteam
    @Id            INT,
    @SteamAppId    INT,
    @PlaytimeHours FLOAT,
    @UserId        INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET SteamAppId    = @SteamAppId,
        PlaytimeHours = @PlaytimeHours,
        Source        = 'Steam'
    WHERE Id = @Id AND UserId = @UserId;
END
GO
