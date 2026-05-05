USE GamePlannerDB;
GO

-- 1. Migrate Genres from Games.Genre (comma-separated)
INSERT INTO Genres (Name)
SELECT DISTINCT LTRIM(RTRIM(value))
FROM Games
CROSS APPLY STRING_SPLIT(Genre, ',')
WHERE IsActive = 1
  AND LTRIM(RTRIM(value)) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM Genres WHERE Name = LTRIM(RTRIM(value))
  );
GO

-- 2. Populate GameGenres
INSERT INTO GameGenres (GameId, GenreId)
SELECT DISTINCT g.Id, gen.Id
FROM Games g
CROSS APPLY STRING_SPLIT(g.Genre, ',') s
JOIN Genres gen ON gen.Name = LTRIM(RTRIM(s.value))
WHERE g.IsActive = 1
  AND LTRIM(RTRIM(s.value)) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM GameGenres gg WHERE gg.GameId = g.Id AND gg.GenreId = gen.Id
  );
GO

-- 3. Migrate Tags from Games.Tags (comma-separated)
INSERT INTO Tags (Name)
SELECT DISTINCT LTRIM(RTRIM(value))
FROM Games
CROSS APPLY STRING_SPLIT(Tags, ',')
WHERE IsActive = 1
  AND LTRIM(RTRIM(value)) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM Tags WHERE Name = LTRIM(RTRIM(value))
  );
GO

-- 4. Populate GameTags
INSERT INTO GameTags (GameId, TagId)
SELECT DISTINCT g.Id, t.Id
FROM Games g
CROSS APPLY STRING_SPLIT(g.Tags, ',') s
JOIN Tags t ON t.Name = LTRIM(RTRIM(s.value))
WHERE g.IsActive = 1
  AND LTRIM(RTRIM(s.value)) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM GameTags gt WHERE gt.GameId = g.Id AND gt.TagId = t.Id
  );
GO
