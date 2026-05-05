USE GamePlannerDB;
GO

-- Make Genre/Tags nullable so old rows still exist but new inserts don't require them
ALTER TABLE Games ALTER COLUMN Genre NVARCHAR(50)  NULL;
ALTER TABLE Games ALTER COLUMN Tags  NVARCHAR(200) NULL;
GO

-- sp_InsertGame: Genre and Tags removed (handled via EF junction tables)
CREATE OR ALTER PROCEDURE sp_InsertGame
    @Name             NVARCHAR(100),
    @EstimatedMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Games (Name, EstimatedMinutes)
    VALUES (@Name, @EstimatedMinutes);
    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

-- sp_UpdateGame: Genre and Tags removed
CREATE OR ALTER PROCEDURE sp_UpdateGame
    @Id               INT,
    @Name             NVARCHAR(100),
    @EstimatedMinutes INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET Name             = @Name,
        EstimatedMinutes = @EstimatedMinutes
    WHERE Id = @Id AND IsActive = 1;
END
GO
