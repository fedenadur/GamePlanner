USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameImages' AND xtype = 'U')
BEGIN
    CREATE TABLE GameImages (
        Id            INT            IDENTITY(1,1) PRIMARY KEY,
        GameId        INT            NOT NULL,
        IgdbImageId   NVARCHAR(100)  NOT NULL,
        ImageType     NVARCHAR(20)   NOT NULL,  -- cover | screenshot | artwork
        Url           NVARCHAR(500)  NOT NULL,
        Width         INT            NULL,
        Height        INT            NULL,
        SyncedAt      DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_GameImages_Games FOREIGN KEY (GameId) REFERENCES Games(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_GameImages_GameId_IgdbImageId UNIQUE (GameId, IgdbImageId)
    );
END
GO

CREATE OR ALTER PROCEDURE sp_UpsertGameImage
    @GameId       INT,
    @IgdbImageId  NVARCHAR(100),
    @ImageType    NVARCHAR(20),
    @Url          NVARCHAR(500),
    @Width        INT = NULL,
    @Height       INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    MERGE GameImages AS target
    USING (SELECT @GameId AS GameId, @IgdbImageId AS IgdbImageId) AS source
        ON target.GameId = source.GameId AND target.IgdbImageId = source.IgdbImageId
    WHEN MATCHED THEN
        UPDATE SET Url = @Url, Width = @Width, Height = @Height, SyncedAt = GETUTCDATE()
    WHEN NOT MATCHED THEN
        INSERT (GameId, IgdbImageId, ImageType, Url, Width, Height)
        VALUES (@GameId, @IgdbImageId, @ImageType, @Url, @Width, @Height);
END
GO

CREATE OR ALTER PROCEDURE sp_UpdateGameLastImageSync
    @GameId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games SET LastImageSyncDate = GETUTCDATE() WHERE Id = @GameId;
END
GO
