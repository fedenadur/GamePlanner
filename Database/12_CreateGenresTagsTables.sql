USE GamePlannerDB;
GO

-- Genres lookup
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Genres' AND xtype = 'U')
BEGIN
    CREATE TABLE Genres (
        Id   INT           IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT UQ_Genres_Name UNIQUE (Name)
    );
END
GO

-- Tags lookup
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Tags' AND xtype = 'U')
BEGIN
    CREATE TABLE Tags (
        Id   INT           IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        CONSTRAINT UQ_Tags_Name UNIQUE (Name)
    );
END
GO

-- Game ↔ Genre relation
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameGenres' AND xtype = 'U')
BEGIN
    CREATE TABLE GameGenres (
        GameId  INT NOT NULL REFERENCES Games(Id),
        GenreId INT NOT NULL REFERENCES Genres(Id),
        CONSTRAINT PK_GameGenres PRIMARY KEY (GameId, GenreId)
    );
END
GO

-- Game ↔ Tag relation
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'GameTags' AND xtype = 'U')
BEGIN
    CREATE TABLE GameTags (
        GameId INT NOT NULL REFERENCES Games(Id),
        TagId  INT NOT NULL REFERENCES Tags(Id),
        CONSTRAINT PK_GameTags PRIMARY KEY (GameId, TagId)
    );
END
GO
