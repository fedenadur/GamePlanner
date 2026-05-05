USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'SteamPlaytimeSyncWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('SteamPlaytimeSyncWorker');
GO
