-- =============================================
-- 33 - Remove Unique Index on IGDBId
-- =============================================

USE GamePlannerDB;
GO

-- Allow duplicate IGDBId values (same IGDB game can exist for multiple users)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Games') AND name = 'UX_Games_IGDBId')
	DROP INDEX UX_Games_IGDBId ON Games;
GO

-- Create non-unique index for query performance
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('Games') AND name = 'IX_Games_IGDBId')
	CREATE NONCLUSTERED INDEX IX_Games_IGDBId
		ON Games (IGDBId)
		WHERE IGDBId IS NOT NULL;
GO
