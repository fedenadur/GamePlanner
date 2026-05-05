USE GamePlannerDB;
GO

-- Themes lookup
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Themes' AND xtype = 'U')
BEGIN
    CREATE TABLE Themes (
        Id   INT           IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT UQ_Themes_Name UNIQUE (Name)
    );
END
GO

-- PlayerPerspectives lookup
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'PlayerPerspectives' AND xtype = 'U')
BEGIN
    CREATE TABLE PlayerPerspectives (
        Id   INT           IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT UQ_PlayerPerspectives_Name UNIQUE (Name)
    );
END
GO

-- GameModes lookup
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameModes' AND xtype = 'U')
BEGIN
    CREATE TABLE GameModes (
        Id   INT           IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT UQ_GameModes_Name UNIQUE (Name)
    );
END
GO

-- Game ↔ Theme relation
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameThemes' AND xtype = 'U')
BEGIN
    CREATE TABLE GameThemes (
        GameId  INT NOT NULL REFERENCES Games(Id),
        ThemeId INT NOT NULL REFERENCES Themes(Id),
        CONSTRAINT PK_GameThemes PRIMARY KEY (GameId, ThemeId)
    );
END
GO

-- Game ↔ PlayerPerspective relation
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GamePlayerPerspectives' AND xtype = 'U')
BEGIN
    CREATE TABLE GamePlayerPerspectives (
        GameId               INT NOT NULL REFERENCES Games(Id),
        PlayerPerspectiveId  INT NOT NULL REFERENCES PlayerPerspectives(Id),
        CONSTRAINT PK_GamePlayerPerspectives PRIMARY KEY (GameId, PlayerPerspectiveId)
    );
END
GO

-- Game ↔ GameMode relation
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameModesRelation' AND xtype = 'U')
BEGIN
    CREATE TABLE GameModesRelation (
        GameId     INT NOT NULL REFERENCES Games(Id),
        GameModeId INT NOT NULL REFERENCES GameModes(Id),
        CONSTRAINT PK_GameModesRelation PRIMARY KEY (GameId, GameModeId)
    );
END
GO
