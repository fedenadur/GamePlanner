-- =============================================
-- 07 - Stored Procedures for IGDB Sync
-- =============================================

USE GamePlannerDB;
GO

-- =============================================
-- sp_UpsertIGDBGame
-- Insert if IGDBId not found, update if found
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpsertIGDBGame
    @IGDBId             BIGINT,
    @Name               NVARCHAR(100),
    @IGDBGenres         NVARCHAR(300),
    @FirstReleaseDate   DATETIME2,
    @UpdatedAt          DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Games WHERE IGDBId = @IGDBId)
    BEGIN
        UPDATE Games
        SET Name             = @Name,
            IGDBGenres       = @IGDBGenres,
            FirstReleaseDate = @FirstReleaseDate,
            UpdatedAt        = @UpdatedAt
        WHERE IGDBId = @IGDBId;
    END
    ELSE
    BEGIN
        INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags, IGDBId, IGDBGenres, FirstReleaseDate, UpdatedAt)
        VALUES (@Name, '', 60, '', @IGDBId, @IGDBGenres, @FirstReleaseDate, @UpdatedAt);
    END
END
GO

-- =============================================
-- sp_UpdateSyncMetadata
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpdateSyncMetadata
    @LastSyncDate           DATETIME2,
    @InitialSyncCompleted   BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE SyncMetadata
    SET LastSyncDate          = @LastSyncDate,
        InitialSyncCompleted  = @InitialSyncCompleted
    WHERE Id = 1;
END
GO
