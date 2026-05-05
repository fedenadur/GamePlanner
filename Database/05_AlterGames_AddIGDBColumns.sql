-- =============================================
-- 05 - Alter Games: add IGDB columns
-- =============================================

USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'IGDBId')
    ALTER TABLE Games ADD IGDBId BIGINT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'IGDBGenres')
    ALTER TABLE Games ADD IGDBGenres NVARCHAR(300) NULL;  -- comma-separated from IGDB
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'FirstReleaseDate')
    ALTER TABLE Games ADD FirstReleaseDate DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'UpdatedAt')
    ALTER TABLE Games ADD UpdatedAt DATETIME2 NULL;
GO

-- Unique index so upsert logic can key on IGDBId
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Games') AND name = 'UX_Games_IGDBId')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Games_IGDBId
        ON Games (IGDBId)
        WHERE IGDBId IS NOT NULL;
GO
