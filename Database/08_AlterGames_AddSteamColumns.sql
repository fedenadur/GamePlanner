-- =============================================
-- 08 - Alter Games: add Steam columns
-- =============================================

USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'SteamAppId')
    ALTER TABLE Games ADD SteamAppId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'PlaytimeHours')
    ALTER TABLE Games ADD PlaytimeHours FLOAT NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'Source')
    ALTER TABLE Games ADD Source NVARCHAR(20) NOT NULL DEFAULT 'Manual';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Games') AND name = 'UX_Games_SteamAppId')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Games_SteamAppId
        ON Games (SteamAppId)
        WHERE SteamAppId IS NOT NULL;
GO
