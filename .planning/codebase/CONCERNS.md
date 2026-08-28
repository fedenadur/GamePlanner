# Codebase Concerns

**Analysis Date:** 2026-08-28

## Critical Security Issues

### IGDB API Credentials in URL Query Strings

**Risk:** Credentials transmitted unencrypted in URLs, logged by HTTP clients and proxies

**Files:** `GamePlanner.API/Services/IgdbService.cs` (line 30)

**Current Code:**
```csharp
var url = $"{TokenUrl}?client_id={ClientId}&client_secret={ClientSecret}&grant_type=client_credentials";
var response = await client.PostAsync(url, null);
```

**Issue:** ClientSecret is embedded in URL string sent to PostAsync. This violates OAuth2 security best practices and exposes credentials to:
- HTTP client libraries that may log URLs
- Proxy servers that cache URLs
- Browser history if accidentally accessed
- Server logs

**Fix Approach:** Use FormUrlEncodedContent in POST body instead of query parameters. Similarly affects `SteamService.cs` lines 41, 59, 76 where Steam API keys are embedded in URLs.

### Steam API Key in Request URLs

**Risk:** Same as IGDB credentials — API keys exposed in URL strings

**Files:** `GamePlanner.API/Services/SteamService.cs` (lines 41, 59, 76)

**Examples:**
- Line 41: `ISteamUser/ResolveVanityURL/v1/?key={ApiKey}&vanityurl={vanity}`
- Line 59: `ISteamUser/GetPlayerSummaries/v2/?key={ApiKey}&steamids={steamId}`
- Line 76: `IPlayerService/GetOwnedGames/v1/?key={ApiKey}&steamid={steamId}&include_appinfo=true&format=json`

**Fix Approach:** Migrate to Authorization headers or request bodies per Steam API documentation.

### CORS Policy Hardcoded for Development

**Risk:** Security bypass in production if configuration not updated

**Files:** `GamePlanner.API/Program.cs` (lines 86-88)

**Current Code:**
```csharp
options.AddPolicy("AllowAngular", policy =>
    policy.WithOrigins("http://localhost:4200")
          .AllowAnyHeader()
          .AllowAnyMethod());
```

**Issue:** Localhost CORS origin only works for development. No environment-based configuration for production CORS policies.

**Fix Approach:** Use environment-based configuration to load CORS origins from appsettings.Production.json. Restrict AllowAnyHeader/AllowAnyMethod to specific headers and methods.

---

## Resource Leaks

### SqlConnection Not Disposed Properly

**Risk:** Connection pool exhaustion, memory leaks, eventual application hang

**Files:** `GamePlanner.API/Managers/GameManager.cs` (lines 101, 125, 158, 175)

**Affected Methods:**
- `CreateFromIgdbAsync` (line 101): `using var conn = CreateConnection();` but no explicit call to Open()
- `CreateAsync` (line 125): Same pattern
- `UpdateAsync` (line 158): Same pattern
- `DeleteAsync` (line 175): Same pattern

**Issue:** While `using` statements handle disposal, the connection is created fresh for each operation instead of reusing the DbContext. Dapper operations should be encapsulated or DbContext should handle all data access.

**Fix Approach:** Either (1) Move all data modifications through EF Core DbContext instead of mixing Dapper/SqlConnection, or (2) Create a dedicated Dapper repository class that manages connection pooling.

### SteamTokenStore Memory Accumulation

**Risk:** Unbounded memory growth in long-running application

**Files:** `GamePlanner.API/Services/SteamTokenStore.cs`

**Current Code:**
```csharp
private readonly ConcurrentDictionary<string, (AuthResponse Response, DateTime Expiry)> _store = new();

public AuthResponse? Redeem(string code)
{
    if (!_store.TryRemove(code, out var entry)) return null;
    if (entry.Expiry < DateTime.UtcNow) return null;
    return entry.Response;
}
```

**Issue:** Expired tokens are only removed if explicitly redeemed. Unused/abandoned tokens remain in memory forever, growing the dictionary indefinitely.

**Fix Approach:** Implement periodic cleanup task (e.g., every 10 minutes) that removes expired entries using `_store.RemoveWhere()` with expiry check.

---

## Data Integrity Issues

### sp_UpsertIGDBGame Inserts with Invalid Data

**Risk:** Games inserted with empty Genre and Tags violate NOT NULL constraints silently, corrupt game data

**Files:** `Database/34_FixStoredProcedure_UpsertIGDBGame.sql` (lines 32-33)

**Stored Procedure:**
```sql
INSERT INTO Games (Name, Genre, EstimatedMinutes, Tags, IGDBId, IGDBGenres, FirstReleaseDate, UpdatedAt, UserId)
VALUES (@Name, '', 60, '', @IGDBId, @IGDBGenres, @FirstReleaseDate, @UpdatedAt, @UserId);
```

**Issue:** Hardcoded empty strings for Genre/Tags contradict database schema where these are NOT NULL. EstimatedMinutes hardcoded to 60.

**Fix Approach:** Remove INSERT statement; games should be created only through `sp_InsertGame` (used in GameManager). Use UPDATE-only in UPSERT for existing records.

### sp_UpsertGameImage Doesn't Update ImageType

**Risk:** Image classification can become incorrect after first insertion, causing incorrect image galleries

**Files:** `Database/32_CreateGameImages.sql` (lines 31-38)

**Stored Procedure:**
```sql
WHEN MATCHED THEN
    UPDATE SET Url = @Url, Width = @Width, Height = @Height, SyncedAt = GETUTCDATE()
WHEN NOT MATCHED THEN
    INSERT (GameId, IgdbImageId, ImageType, Url, Width, Height)
```

**Issue:** MERGE only updates Url/Width/Height on match but NOT ImageType. If image type changes (e.g., from 'screenshot' to 'artwork'), the update fails to reflect it.

**Fix Approach:** Add `ImageType = @ImageType` to the UPDATE clause in WHEN MATCHED.

---

## Test Coverage Gaps

### No Unit Tests, Integration Tests, or E2E Tests

**Risk:** Cannot verify functionality, regressions undetected, brittle refactoring

**Files:** Entire codebase — no test files found except dev dependencies

**Impact:**
- Core services untested: `GameManager`, `GameSyncManager`, `IgdbService`, `SteamService`, `AuthService`
- Complex business logic untested: `BacklogPrioritizerManager` (276 LOC), `SessionPlannerManager` (237 LOC)
- Worker services have no validation: 6 background workers with no tests
- Frontend completely untested: All Angular components

**Priority:** High — Major functionality areas have zero test coverage.

**Fix Approach:**
1. Add xUnit test project `GamePlanner.Tests`
2. Add Moq for mocking external dependencies (IGDB, Steam APIs)
3. Write unit tests for all managers (minimum 70% code coverage)
4. Add Angular Jasmine tests for critical components
5. Set up CI/CD to enforce test execution

---

## Dependencies at Risk

### Angular 13.3.0 — End of Life

**Risk:** No security updates, unpatched vulnerabilities, compatibility issues

**Files:** `game-planner-ui/package.json` (line 13)

**Current Version:** 13.3.0 (released March 2022)
**EOL Date:** April 2023 — **2+ years outdated**

**Related EOL Dependencies:**
- @angular/forms ~13.3.0 (EOL)
- @angular/router ~13.3.0 (EOL)
- rxjs ~7.5.0 (older, current is ~7.8.x)
- zone.js ~0.11.4 (outdated)
- jasmine-core ~4.0.0 (old, current is ~5.x)
- karma ~6.3.0 (old, current is ~6.4.x)
- @types/node ^12.11.1 (v12 is EOL, should be v20+)
- typescript ~4.6.2 (old, current is ~5.x)

**Fix Approach:** Execute `ng update @angular/cli @angular/core` to migrate to Angular 18 LTS (current stable). Test thoroughly. Update all related packages.

---

## Error Handling & Resilience

### Broad Exception Handling with Silent Failures

**Risk:** External API failures silently logged but not surfaced to user, incomplete data syncs

**Files:** `GamePlanner.API/Managers/GameImageManager.cs` (lines 69-72, 90-93, 177-189)

**Pattern:**
```csharp
try
{
    var fetched = await igdb.FetchCoversByIdsAsync(withId.Select(g => g.IGDBId!.Value), ct);
    igdbResults = fetched.ToDictionary(r => r.Id);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Batch cover fetch failed, will retry individually.");
}
```

**Issue:** Catches all Exception types, logs warning, continues processing. No retry logic, no circuit breaker, no exponential backoff. If IGDB API is down, silently skips games without alerting user.

**Fix Approach:**
1. Use Polly library for retry policies with exponential backoff
2. Implement circuit breaker for external APIs
3. Track failed game IDs for later manual retry
4. Alert user/admin when external API calls fail repeatedly

### Hardcoded CancellationToken.None

**Risk:** Worker cannot cancel requests when instructed to stop, hangs on external API calls

**Files:** `GamePlanner.API/Services/SteamAuthService.cs` (line 43)

**Code:**
```csharp
var summary = await steam.GetPlayerSummaryAsync(steamId, CancellationToken.None);
```

**Issue:** Uses `CancellationToken.None` instead of accepting/forwarding cancellation token from caller. Prevents graceful shutdown if Steam API hangs.

**Fix Approach:** Accept `CancellationToken ct` parameter and pass through to service calls.

---

## Code Quality & Maintainability

### Mixed Data Access Patterns

**Risk:** Inconsistency, difficult to maintain, potential bugs in data consistency

**Files:** Entire codebase

**Pattern:**
- Stored procedures via Dapper: `GameManager.CreateAsync`, `GameImageManager.SaveCoverAsync`
- EF Core LINQ: `GameManager.GetAllAsync`, `GameImageManager.RunAsync`
- Direct SqlConnection: Multiple managers

**Issue:** No consistent data access strategy. Some operations use stored procedures, others use EF, creating mental overhead and potential for inconsistency.

**Fix Approach:** Establish data access pattern:
1. Use EF Core for all queries (reads)
2. Use stored procedures ONLY for complex aggregations or bulk operations
3. Move CREATE/UPDATE/DELETE to stored procedures or remove them entirely
4. Create a repository pattern if using EF for all access

### Long-Running Background Workers with Large Intervals

**Risk:** Stale data, slow sync, potential missed updates

**Files:** `GamePlanner.API/Workers/GameSyncWorker.cs` (line 9)

**Current Intervals:**
- GameSyncWorker: 24 hours
- GameImageSyncWorker: (check interval)
- SteamPlaytimeSyncWorker: (check interval)

**Issue:** 24-hour intervals mean user data can be up to 24 hours stale. No adaptive backoff based on changes detected.

**Fix Approach:** Make intervals configurable. Consider shorter intervals (2-6 hours) for active users, longer for inactive.

### Hardcoded Batch Sizes

**Risk:** May be inefficient for database or API rate limits

**Files:**
- `GamePlanner.API/Managers/GameImageManager.cs` (line 16): `BatchSize = 50`
- `GamePlanner.API/Services/IgdbService.cs` (line 20): `BatchSize = 500` (from config, better)

**Issue:** Batch size of 50 for images may be overly conservative, causing excessive round trips.

**Fix Approach:** Move all batch sizes to configuration (appsettings.json). Profile optimal batch size for each API.

---

## Performance Concerns

### N+1 Query Pattern in GameManager

**Risk:** Excessive database round trips for include operations

**Files:** `GamePlanner.API/Managers/GameManager.cs` (lines 18-26, 33-39, 46-52)

**Pattern:** Multiple `.Include()` calls repeated across methods
```csharp
.Include(g => g.Genres)
.Include(g => g.Tags)
.Include(g => g.Themes)
.Include(g => g.PlayerPerspectives)
.Include(g => g.GameModes)
```

**Issue:** All reads fetch the same 5 related collections. No performance concern for individual queries, but repetition suggests opportunity for a DTO with pre-filtered includes.

**Fix Approach:** Create a separate query method for fully-loaded Game entities used by multiple methods.

### Image Sync Queries In Loop

**Risk:** Quadratic complexity — batch query + individual save in loop

**Files:** `GamePlanner.API/Managers/GameImageManager.cs` (lines 200-206)

**Pattern:**
```csharp
foreach (var game in batch)
{
    await conn.ExecuteAsync("sp_UpdateGameLastImageSync", new { GameId = game.Id }, ...);
}
```

**Issue:** Batch of 50 games = 50 individual stored procedure calls after fetching images. Should batch the updates.

**Fix Approach:** Create a stored procedure `sp_UpdateGameLastImageSyncBatch` that accepts a table-valued parameter of GameIds.

---

## Fragile Areas

### External API Dependencies Without Fallback

**Risk:** Application degradation if any external API fails

**Dependencies:**
- **IGDB (Game metadata):** `IgdbService.cs` — No fallback if IGDB is unavailable
- **Steam (Game library import):** `SteamService.cs` — No fallback if Steam API fails
- **HLTB (Playtime data):** `GameHLTBManager.cs` — No fallback if HLTB data unavailable
- **Twitch OAuth (Steam login):** `SteamAuthService.cs` — Depends on Steam OpenID

**Impact:** Any API outage = loss of specific functionality

**Mitigation:** Document graceful degradation per API. For non-critical data (ratings, playtime), show "unavailable" instead of error.

### Database Schema Migrations Are Manual

**Risk:** Schema drift between environments, deployment risk

**Files:** `Database/` directory — SQL scripts numbered 01-34

**Current Process:**
1. Write numbered SQL file
2. Apply manually in SSMS per CLAUDE.md instructions
3. No rollback mechanism
4. No integration with CI/CD

**Fix Approach:** Use Entity Framework Core migrations instead. Run via `dotnet ef database update` in deployment pipeline.

---

## Missing Critical Features

### No Audit Logging

**Risk:** Cannot trace who changed what data, security/compliance issue

**Impact:** User data modifications (game edits, deletions) leave no audit trail.

**Fix Approach:** Add audit fields (CreatedBy, ModifiedBy, ModifiedAt) to Game, NowPlayingGame, GameJournalEntry entities.

### No Rate Limiting on Data Modification Endpoints

**Risk:** User could spam game creation, journal entries, session updates

**Files:** `GamePlanner.API/Program.cs` (lines 71-81)

**Current:** Rate limiter only on `/auth` endpoints. No limits on POST/PUT/DELETE game operations.

**Fix Approach:** Add rate limiter to all data modification endpoints (e.g., 100 requests/hour per user for game operations).

### No Soft Delete Audit Trail

**Risk:** `IsActive` flag hides deletions but doesn't track deletion reason or time

**Files:** All entities use `IsActive` bit column

**Issue:** When user deletes game, only IsActive is set to 0. No DeletedAt, DeletedReason, or deletion audit.

**Fix Approach:** Add DeletedAt datetime column to track when soft deletes occurred.

---

## Scaling Limits

### In-Memory Token Store (SteamTokenStore)

**Scaling Issue:** Each instance maintains its own dictionary. Horizontal scaling requires shared token store.

**Current:** Single instance, works for single-server deployment only.

**Fix Approach:** Migrate to Redis-backed token store for distributed caching.

### Background Workers Run on Single Instance

**Scaling Issue:** Workers registered as `AddHostedService` run on each instance independently.

**Current:** 6 background workers spawn on every server instance in scaled deployment.

**Problem:** If deployed to 3 servers, each worker runs 3 times, causing duplicate sync operations and database lock contention.

**Fix Approach:** Use distributed task scheduler (Hangfire, Quartz) or cloud job service (Azure Service Bus, AWS SQS) to ensure single execution.

### Database Connection Pool

**Potential Issue:** No explicit pooling configuration. Connection string uses default (100 max connections).

**Fix Approach:** Set max pool size in connection string based on expected concurrent users.

---

## Recommendations by Priority

### Critical (Fix Before Production)

1. **Move API credentials from URLs to headers** (IgdbService, SteamService)
2. **Add unit tests** for critical paths (Auth, GameManager, external API integrations)
3. **Migrate Angular to current LTS version** (v18+)
4. **Implement retry/circuit breaker for external APIs**
5. **Fix SqlConnection disposal** in GameManager

### High (Fix in Next Sprint)

1. Implement SteamTokenStore cleanup
2. Add CORS environment configuration
3. Fix sp_UpsertGameImage to update ImageType
4. Add integration tests for worker services
5. Implement rate limiting on data modification endpoints

### Medium (Planned Improvements)

1. Consolidate data access pattern (EF Core vs Dapper)
2. Migrate SQL migrations to EF Core
3. Implement distributed task scheduler for background workers
4. Add audit logging
5. Profile and optimize batch sizes

### Low (Documentation/Refactoring)

1. Document API and external service dependencies
2. Refactor duplicate Include() patterns in GameManager
3. Add soft delete audit trail
4. Make background worker intervals configurable

---

*Concerns audit: 2026-08-28*
