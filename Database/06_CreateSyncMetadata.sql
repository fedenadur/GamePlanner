-- =============================================
-- 06 - Create SyncMetadata table
-- =============================================

USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'SyncMetadata' AND xtype = 'U')
BEGIN
    CREATE TABLE SyncMetadata (
        Id                      INT         IDENTITY(1,1)   PRIMARY KEY,
        LastSyncDate            DATETIME2   NULL,
        InitialSyncCompleted    BIT         NOT NULL    DEFAULT 0
    );

    -- Seed the single control row
    INSERT INTO SyncMetadata (LastSyncDate, InitialSyncCompleted)
    VALUES (NULL, 0);
END
GO
