USE GamePlannerDB;
GO

-- Users table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Users' AND xtype = 'U')
BEGIN
    CREATE TABLE Users (
        Id           INT            IDENTITY(1,1) PRIMARY KEY,
        Email        NVARCHAR(256)  NOT NULL,
        PasswordHash NVARCHAR(512)  NULL,          -- NULL for Steam-only accounts
        Username     NVARCHAR(100)  NULL,
        SteamId      NVARCHAR(50)   NULL,
        IsActive     BIT            NOT NULL DEFAULT 1,
        CreatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_Users_Email   UNIQUE (Email),
        CONSTRAINT UQ_Users_SteamId UNIQUE (SteamId)
    );
END
GO

-- Add UserId FK to Games (nullable so existing rows are not broken)
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Games') AND name = 'UserId'
)
BEGIN
    ALTER TABLE Games ADD UserId INT NULL REFERENCES Users(Id);
END
GO
