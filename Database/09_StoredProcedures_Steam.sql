-- =============================================
-- 09 - Stored Procedures for Steam Import
-- =============================================

USE GamePlannerDB;
GO

CREATE OR ALTER PROCEDURE sp_UpdateGameFromSteam
    @Id             INT,
    @SteamAppId     INT,
    @PlaytimeHours  FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET SteamAppId    = @SteamAppId,
        PlaytimeHours = @PlaytimeHours,
        Source        = 'Steam'
    WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE sp_InsertSteamGame
    @Name           NVARCHAR(100),
    @SteamAppId     INT,
    @PlaytimeHours  FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags, SteamAppId, PlaytimeHours, Source)
    VALUES (@Name, '', 60, '', @SteamAppId, @PlaytimeHours, 'Steam');
END
GO
