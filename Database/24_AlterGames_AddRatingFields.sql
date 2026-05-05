USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'AggregatedRating')
    ALTER TABLE Games ADD AggregatedRating      FLOAT    NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'AggregatedRatingCount')
    ALTER TABLE Games ADD AggregatedRatingCount INT      NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Games') AND name = 'LastIGDBSyncAt')
    ALTER TABLE Games ADD LastIGDBSyncAt        DATETIME2 NULL;
GO
