USE GamePlannerDB;
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Users') AND name = 'DisplayName'
)
BEGIN
    ALTER TABLE Users ADD DisplayName NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Users') AND name = 'ProfileImageUrl'
)
BEGIN
    ALTER TABLE Users ADD ProfileImageUrl NVARCHAR(500) NULL;
END
GO

-- Make Email nullable for Steam-only accounts
IF EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID('Users') AND name = 'Email' AND is_nullable = 0
)
BEGIN
    ALTER TABLE Users ALTER COLUMN Email NVARCHAR(256) NULL;
END
GO
