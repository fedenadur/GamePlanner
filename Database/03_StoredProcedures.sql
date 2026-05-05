-- =============================================
-- 03 - Stored Procedures (Insert / Update / Delete)
-- =============================================

USE GamePlannerDB;
GO

-- =============================================
-- sp_InsertGame
-- =============================================
CREATE OR ALTER PROCEDURE sp_InsertGame
    @Name               NVARCHAR(100),
    @Genre              NVARCHAR(50),
    @EstimatedMinutes   INT,
    @Tags               NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags)
    VALUES (@Name, @Genre, @EstimatedMinutes, @Tags);

    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

-- =============================================
-- sp_UpdateGame
-- =============================================
CREATE OR ALTER PROCEDURE sp_UpdateGame
    @Id                 INT,
    @Name               NVARCHAR(100),
    @Genre              NVARCHAR(50),
    @EstimatedMinutes   INT,
    @Tags               NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Games
    SET Name             = @Name,
        Genre            = @Genre,
        EstimatedMinutes = @EstimatedMinutes,
        Tags             = @Tags
    WHERE Id = @Id AND IsActive = 1;
END
GO

-- =============================================
-- sp_DeleteGame  (soft delete)
-- =============================================
CREATE OR ALTER PROCEDURE sp_DeleteGame
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Games
    SET IsActive = 0
    WHERE Id = @Id;
END
GO
