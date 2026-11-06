USE GamePlannerDB;
GO

-- Steam imports get Store = 'Steam' (spec 6.5). Same signature and behaviour as script 20 otherwise.
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

    INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags, SteamAppId, PlaytimeHours, Source, UserId, Store)
    VALUES (@Name, '', 60, '', @SteamAppId, @PlaytimeHours, 'Steam', @UserId, 'Steam');
END
GO
