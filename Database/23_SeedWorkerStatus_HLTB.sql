USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'GameHLTBSyncWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('GameHLTBSyncWorker');
GO
