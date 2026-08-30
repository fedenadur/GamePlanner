USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'FailedItemCount')
    ALTER TABLE WorkerStatus ADD FailedItemCount          INT           NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkerStatus') AND name = 'LastPartialFailureDetail')
    ALTER TABLE WorkerStatus ADD LastPartialFailureDetail NVARCHAR(500) NULL;
GO
