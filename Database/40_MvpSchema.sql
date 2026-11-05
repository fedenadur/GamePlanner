USE GamePlannerDB;
GO

-- MVP schema (DDL only, idempotent). Data is migrated by 41_MvpDataMigration.sql.

IF COL_LENGTH('Users', 'IsAdmin') IS NULL
    ALTER TABLE Users ADD IsAdmin BIT NOT NULL CONSTRAINT DF_Users_IsAdmin DEFAULT 0;
GO

IF COL_LENGTH('Games', 'Store') IS NULL
    ALTER TABLE Games ADD Store NVARCHAR(30) NOT NULL CONSTRAINT DF_Games_Store DEFAULT 'Other';
GO

IF COL_LENGTH('Games', 'IsFavorite') IS NULL
    ALTER TABLE Games ADD IsFavorite BIT NOT NULL CONSTRAINT DF_Games_IsFavorite DEFAULT 0;
GO

-- NowPlayingGames.Device replaces Slot; added nullable here, completed by script 41.
IF COL_LENGTH('NowPlayingGames', 'Device') IS NULL
    ALTER TABLE NowPlayingGames ADD Device NVARCHAR(20) NULL;
GO

IF OBJECT_ID('UserDevices', 'U') IS NULL
CREATE TABLE UserDevices (
    UserId    INT          NOT NULL,
    Device    NVARCHAR(20) NOT NULL,
    SortOrder INT          NOT NULL DEFAULT 0,
    CONSTRAINT PK_UserDevices PRIMARY KEY (UserId, Device),
    CONSTRAINT FK_UserDevices_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

IF OBJECT_ID('GamePlayableDevices', 'U') IS NULL
CREATE TABLE GamePlayableDevices (
    GameId INT          NOT NULL,
    Device NVARCHAR(20) NOT NULL,
    CONSTRAINT PK_GamePlayableDevices PRIMARY KEY (GameId, Device),
    CONSTRAINT FK_GamePlayableDevices_Game FOREIGN KEY (GameId) REFERENCES Games(Id)
);
GO

IF OBJECT_ID('PlaySessions', 'U') IS NULL
BEGIN
    CREATE TABLE PlaySessions (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        UserId     INT           NOT NULL,
        GameId     INT           NOT NULL,
        Device     NVARCHAR(20)  NOT NULL,
        PlayedOn   DATE          NOT NULL,
        Hours      DECIMAL(4,2)  NOT NULL,
        Note       NVARCHAR(1000) NULL,
        CreatedAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        ModifiedAt DATETIME2     NULL,
        CONSTRAINT FK_PlaySessions_User FOREIGN KEY (UserId) REFERENCES Users(Id),
        CONSTRAINT FK_PlaySessions_Game FOREIGN KEY (GameId) REFERENCES Games(Id),
        CONSTRAINT CK_PlaySessions_Hours CHECK (Hours > 0 AND Hours <= 24)
    );
    CREATE INDEX IX_PlaySessions_UserGameDate ON PlaySessions (UserId, GameId, PlayedOn DESC);
END
GO

IF OBJECT_ID('UserPreferences', 'U') IS NULL
CREATE TABLE UserPreferences (
    UserId       INT          NOT NULL PRIMARY KEY,
    MaxGameHours INT          NULL,
    SinglePlayer NVARCHAR(10) NOT NULL CONSTRAINT DF_UserPreferences_SinglePlayer DEFAULT 'Neutral',
    Narrative    NVARCHAR(10) NOT NULL CONSTRAINT DF_UserPreferences_Narrative    DEFAULT 'Neutral',
    Combat       NVARCHAR(10) NOT NULL CONSTRAINT DF_UserPreferences_Combat       DEFAULT 'Neutral',
    CONSTRAINT FK_UserPreferences_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT CK_UserPreferences_SinglePlayer CHECK (SinglePlayer IN ('Prefer','Neutral','Avoid')),
    CONSTRAINT CK_UserPreferences_Narrative    CHECK (Narrative    IN ('Prefer','Neutral','Avoid')),
    CONSTRAINT CK_UserPreferences_Combat       CHECK (Combat       IN ('Prefer','Neutral','Avoid'))
);
GO

-- PK (UserId, GenreId): a genre is in at most one list (Favorite or Avoid).
IF OBJECT_ID('UserPreferenceGenres', 'U') IS NULL
CREATE TABLE UserPreferenceGenres (
    UserId  INT          NOT NULL,
    GenreId INT          NOT NULL,
    Kind    NVARCHAR(10) NOT NULL,
    CONSTRAINT PK_UserPreferenceGenres PRIMARY KEY (UserId, GenreId),
    CONSTRAINT FK_UserPreferenceGenres_User  FOREIGN KEY (UserId)  REFERENCES Users(Id),
    CONSTRAINT FK_UserPreferenceGenres_Genre FOREIGN KEY (GenreId) REFERENCES Genres(Id),
    CONSTRAINT CK_UserPreferenceGenres_Kind CHECK (Kind IN ('Favorite','Avoid'))
);
GO
