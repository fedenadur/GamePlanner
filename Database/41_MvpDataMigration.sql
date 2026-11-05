USE GamePlannerDB;
GO

-- MVP data migration (spec 6.11). Transactional and idempotent: a second run changes nothing.
-- Back up GamePlannerDB before the first run.
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @n INT;

-- 1. Store from Source / SteamAppId
UPDATE Games SET Store = 'Steam'
 WHERE (SteamAppId IS NOT NULL OR Source = 'Steam') AND Store <> 'Steam';
SET @n = @@ROWCOUNT;
PRINT 'Step 1 - Games.Store set to Steam: ' + CAST(@n AS VARCHAR(10));

-- 2. Default devices (PC, SteamDeck) for users with Now Playing rows and no devices
INSERT INTO UserDevices (UserId, Device, SortOrder)
SELECT u.UserId, d.Device, d.SortOrder
  FROM (SELECT DISTINCT UserId FROM NowPlayingGames) u
 CROSS JOIN (VALUES ('PC', 0), ('SteamDeck', 1)) d(Device, SortOrder)
 WHERE NOT EXISTS (SELECT 1 FROM UserDevices ud WHERE ud.UserId = u.UserId);
SET @n = @@ROWCOUNT;
PRINT 'Step 2 - UserDevices rows inserted: ' + CAST(@n AS VARCHAR(10));

-- 4 (run first, so slot-2 games are covered before their row is deleted).
-- Every game in Now Playing gets a Playing journal entry.
-- 4a. Missing entries are created.
INSERT INTO GameJournalEntries (UserId, GameId, Status, StartedAt)
SELECT n.UserId, n.GameId, 'Playing', n.AddedAt
  FROM NowPlayingGames n
 WHERE NOT EXISTS (SELECT 1 FROM GameJournalEntries j WHERE j.UserId = n.UserId AND j.GameId = n.GameId);
SET @n = @@ROWCOUNT;
PRINT 'Step 4a - Journal entries created: ' + CAST(@n AS VARCHAR(10));

-- 4b. Finished becomes Replaying.
UPDATE j SET Status = 'Replaying', UpdatedAt = GETUTCDATE()
  FROM GameJournalEntries j
  JOIN NowPlayingGames n ON n.UserId = j.UserId AND n.GameId = j.GameId
 WHERE j.Status = 'Finished';
SET @n = @@ROWCOUNT;
PRINT 'Step 4b - Journal Finished -> Replaying: ' + CAST(@n AS VARCHAR(10));

-- 4c. Any other non-active status becomes Playing.
UPDATE j SET Status = 'Playing', UpdatedAt = GETUTCDATE()
  FROM GameJournalEntries j
  JOIN NowPlayingGames n ON n.UserId = j.UserId AND n.GameId = j.GameId
 WHERE j.Status NOT IN ('Playing', 'Replaying');
SET @n = @@ROWCOUNT;
PRINT 'Step 4c - Journal other status -> Playing: ' + CAST(@n AS VARCHAR(10));

-- 4d. StartedAt defaults to AddedAt.
UPDATE j SET StartedAt = n.AddedAt, UpdatedAt = GETUTCDATE()
  FROM GameJournalEntries j
  JOIN NowPlayingGames n ON n.UserId = j.UserId AND n.GameId = j.GameId
 WHERE j.StartedAt IS NULL;
SET @n = @@ROWCOUNT;
PRINT 'Step 4d - Journal StartedAt filled: ' + CAST(@n AS VARCHAR(10));

-- 3. Slot -> Device (only while the Slot column still exists)
IF COL_LENGTH('NowPlayingGames', 'Slot') IS NOT NULL
BEGIN
    DECLARE @n3 INT;

    EXEC sp_executesql N'
        UPDATE NowPlayingGames SET Device = CASE Slot WHEN 0 THEN ''PC'' WHEN 1 THEN ''SteamDeck'' END
         WHERE Device IS NULL AND Slot IN (0, 1);
        SET @c = @@ROWCOUNT;', N'@c INT OUTPUT', @c = @n3 OUTPUT;
    PRINT 'Step 3 - Slot 0/1 rows moved to PC/SteamDeck: ' + CAST(@n3 AS VARCHAR(10));

    DELETE FROM NowPlayingGames WHERE Device IS NULL;
    SET @n3 = @@ROWCOUNT;
    PRINT 'Step 3 - Other-slot rows removed from Now Playing (journal kept as Playing): ' + CAST(@n3 AS VARCHAR(10));

    ALTER TABLE NowPlayingGames ALTER COLUMN Device NVARCHAR(20) NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_NowPlaying_UserDevice')
        ALTER TABLE NowPlayingGames ADD CONSTRAINT UQ_NowPlaying_UserDevice UNIQUE (UserId, Device);
    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_NowPlaying_UserGame')
        ALTER TABLE NowPlayingGames ADD CONSTRAINT UQ_NowPlaying_UserGame UNIQUE (UserId, GameId);

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_NowPlaying_UserSlot')
        ALTER TABLE NowPlayingGames DROP CONSTRAINT UQ_NowPlaying_UserSlot;
    ALTER TABLE NowPlayingGames DROP COLUMN Slot;
    PRINT 'Step 3 - Slot column and UQ_NowPlaying_UserSlot dropped';
END
ELSE
    PRINT 'Step 3 - Slot already migrated: nothing to do';

COMMIT TRAN;
GO
