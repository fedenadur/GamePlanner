-- =============================================
-- 02 - Create Tables
-- =============================================

USE GamePlannerDB;
GO

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name = 'Games' AND xtype = 'U')
BEGIN
    CREATE TABLE Games (
        Id                    INT             IDENTITY(1,1)   PRIMARY KEY,
        Name                  NVARCHAR(100)   NOT NULL,
        Genre                 NVARCHAR(50)    NOT NULL,
        EstimatedMinutes      INT             NOT NULL,   -- 30, 60, or 120
        Tags                  NVARCHAR(200)   NOT NULL,   -- comma-separated: "chill,story,coop,focused"
        IsActive              BIT             NOT NULL    DEFAULT 1,
        CreatedAt             DATETIME2       NOT NULL    DEFAULT GETUTCDATE()
    );
END
GO
