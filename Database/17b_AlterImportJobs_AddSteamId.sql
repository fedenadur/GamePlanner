USE GamePlannerDB;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('ImportJobs') AND name = 'SteamId'
)
BEGIN
    ALTER TABLE ImportJobs ADD SteamId NVARCHAR(100) NOT NULL DEFAULT '';
END
GO
