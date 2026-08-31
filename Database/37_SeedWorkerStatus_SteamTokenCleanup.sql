USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM WorkerStatus WHERE Name = 'SteamTokenCleanupWorker')
    INSERT INTO WorkerStatus (Name) VALUES ('SteamTokenCleanupWorker');
GO
