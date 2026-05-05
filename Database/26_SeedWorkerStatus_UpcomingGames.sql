USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM WorkerStatus WHERE Name = 'UpcomingGamesSyncWorker')
    INSERT INTO WorkerStatus (Name, Status) VALUES ('UpcomingGamesSyncWorker', 'Idle');
GO
