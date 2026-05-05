USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'UpcomingGames' AND xtype = 'U')
BEGIN
    CREATE TABLE UpcomingGames (
        Id          INT             IDENTITY(1,1)   PRIMARY KEY,
        IGDBId      BIGINT          NOT NULL,
        Name        NVARCHAR(200)   NOT NULL,
        CoverUrl    NVARCHAR(500)   NULL,
        ReleaseDate DATETIME2       NOT NULL,
        Hypes       INT             NOT NULL        DEFAULT 0,
        Genres      NVARCHAR(1000)  NULL,
        CachedAt    DATETIME2       NOT NULL        DEFAULT GETUTCDATE()
    );

    CREATE INDEX IX_UpcomingGames_ReleaseDate ON UpcomingGames (ReleaseDate);
    CREATE UNIQUE INDEX UX_UpcomingGames_IGDBId ON UpcomingGames (IGDBId);
END
GO
