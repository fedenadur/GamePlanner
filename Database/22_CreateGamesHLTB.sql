USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GamesHLTB' AND xtype = 'U')
BEGIN
    CREATE TABLE GamesHLTB (
        Id                  INT            IDENTITY(1,1) PRIMARY KEY,
        Name                NVARCHAR(200)  NOT NULL,
        SteamAppId          INT            NULL,
        MainStoryHours      FLOAT          NULL,   -- comp_main (converted from seconds)
        MainPlusHours       FLOAT          NULL,   -- comp_plus (converted from seconds)
        CompletionistHours  FLOAT          NULL,   -- comp_100  (converted from seconds)
        LastUpdated         DATETIME2      NOT NULL DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_GamesHLTB_SteamAppId ON GamesHLTB (SteamAppId) WHERE SteamAppId IS NOT NULL;
    CREATE INDEX IX_GamesHLTB_Name       ON GamesHLTB (Name);
END
GO
