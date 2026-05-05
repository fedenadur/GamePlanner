USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'ImportJobs' AND xtype = 'U')
BEGIN
    CREATE TABLE ImportJobs (
        Id              INT           IDENTITY(1,1) PRIMARY KEY,
        UserId          INT           NOT NULL REFERENCES Users(Id),
        SteamId         NVARCHAR(100) NOT NULL,
        Status          NVARCHAR(20)  NOT NULL DEFAULT 'Pending',  -- Pending | Running | Completed | Failed
        Progress        INT           NOT NULL DEFAULT 0,           -- 0–100
        TotalGames      INT           NOT NULL DEFAULT 0,
        ProcessedGames  INT           NOT NULL DEFAULT 0,
        ErrorMessage    NVARCHAR(500) NULL,
        CreatedAt       DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CompletedAt     DATETIME2     NULL
    );
END
GO
