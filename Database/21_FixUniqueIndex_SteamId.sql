USE GamePlannerDB;
GO

-- Drop the constraint that blocks multiple NULL SteamIds
IF EXISTS (
    SELECT * FROM sys.key_constraints
    WHERE name = 'UQ_Users_SteamId' AND parent_object_id = OBJECT_ID('Users')
)
BEGIN
    ALTER TABLE Users DROP CONSTRAINT UQ_Users_SteamId;
END
GO

-- Recreate as a filtered unique index — only enforces uniqueness when SteamId is NOT NULL
IF NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE name = 'UX_Users_SteamId' AND object_id = OBJECT_ID('Users')
)
BEGIN
    CREATE UNIQUE INDEX UX_Users_SteamId
        ON Users (SteamId)
        WHERE SteamId IS NOT NULL;
END
GO
