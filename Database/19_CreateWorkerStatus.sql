USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'WorkerStatus' AND xtype = 'U')
BEGIN
    CREATE TABLE WorkerStatus (
        Id            INT            IDENTITY(1,1) PRIMARY KEY,
        Name          NVARCHAR(100)  NOT NULL,
        Status        NVARCHAR(20)   NOT NULL DEFAULT 'Idle',  -- Idle | Running | Completed | Failed
        LastRunAt     DATETIME2      NULL,
        LastSuccessAt DATETIME2      NULL,
        LastError     NVARCHAR(500)  NULL,
        CONSTRAINT UQ_WorkerStatus_Name UNIQUE (Name)
    );
END
GO

-- Seed known workers
IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'GameSyncWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('GameSyncWorker');

IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'GameImageSyncWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('GameImageSyncWorker');
GO
