# External Integrations

**Analysis Date:** 2026-08-28

## APIs & External Services

**Game Data:**
- **IGDB (Internet Game Database)** - Primary source for game metadata
  - SDK/Client: `IgdbService` in `GamePlanner.API/Services/IgdbService.cs`
  - Auth: OAuth 2.0 via Twitch (client credentials flow)
  - Credentials: `IGDB:ClientId`, `IGDB:ClientSecret` in config
  - Endpoints: `https://api.igdb.com/v4/` (games, screenshots, artworks)
  - Token endpoint: `https://id.twitch.tv/oauth2/token`
  - Batch size: Configurable via `IGDB:BatchSize` (default 500)
  - Used by: `GameSyncWorker`, `GameEnrichmentManager`, `GameImageSyncManager`

**User Gaming Platform:**
- **Steam Web API** - Player library and playtime data
  - SDK/Client: `SteamService` in `GamePlanner.API/Services/SteamService.cs`
  - Auth: API key-based (`Steam:ApiKey`)
  - Credentials: `Steam:ApiKey` in config
  - Base URL: `https://api.steampowered.com`
  - Key endpoints:
    - `ISteamUser/ResolveVanityURL/v1/` - Resolve vanity usernames
    - `ISteamUser/GetPlayerSummaries/v2/` - User profile data
    - `IPlayerService/GetOwnedGames/v1/` - Library contents
  - Used by: `SteamImportWorker`, `SteamPlaytimeSyncWorker`, `SteamAuthService`

**Game Duration Data:**
- **HowLongToBeat (CSV Export)** - Game completion time reference
  - Data source: CSV file (`howlongtobeat_games.csv` in project root)
  - Path config: `HLTB:CsvPath` in development config
  - Client: `CsvGameService` in `GamePlanner.API/Services/CsvGameService.cs`
  - Fields parsed: game name, Steam App ID, main story hours, main+extras hours, completionist hours
  - Used by: `GameHLTBSyncWorker` for enriching game records

## Data Storage

**Databases:**
- **SQL Server** - Primary data store
  - Provider: Microsoft SQL Server (2019 or later recommended)
  - Database: `GamePlannerDB`
  - Connection: Windows authentication (Trusted_Connection in local setup)
  - Connection string: `Server=localhost;Database=GamePlannerDB;Trusted_Connection=True;TrustServerCertificate=True;`
  - Config key: `ConnectionStrings:DefaultConnection`
  - Client: Entity Framework Core 8.0 via `GamePlannerDbContext` in `GamePlanner.API/Context/GamePlannerDbContext.cs`
  - Also supports Dapper queries for complex operations

**File Storage:**
- **Local filesystem only** - CSV data and cached images
  - HLTB CSV: Path specified in `HLTB:CsvPath`
  - No cloud storage configured

**Caching:**
- **In-memory caching (IgdbService)** - IGDB token caching
  - OAuth token cached with 1-minute safety margin before expiry
  - Singleton instance: `IgdbService` registered as singleton in `Program.cs`

## Authentication & Identity

**Auth Provider:**
- **Custom JWT-based** - Internal user authentication
  - Implementation: `AuthService` in `GamePlanner.API/Services/AuthService.cs`
  - Token issuer: `GamePlannerAPI` (configurable)
  - Token audience: `GamePlannerClient` (configurable)
  - Secret key: `Jwt:Secret` in config (must be 32+ characters in production)
  - Algorithm: HS256 (HMAC SHA-256)
  - Token validation in `Program.cs` with lifetime checks
  - Frontend storage: localStorage (key: `gp_token`)

**Steam OAuth Integration:**
- **Steam Web API OpenID** - External account linking
  - Implementation: `SteamAuthService` in `GamePlanner.API/Services/SteamAuthService.cs`
  - Token store: `SteamTokenStore` (singleton) in `GamePlanner.API/Services/SteamTokenStore.cs`
  - Used for: Linking Steam library to GamePlanner account

**Password Security:**
- **BCrypt (BCrypt.Net-Next 4.0.3)** - Hashing/verification
  - Used in `AuthService` for user password storage

## Rate Limiting

**Auth Endpoints:**
- **Fixed Window Limiter** - Protects auth routes
  - Limit: 10 requests per minute
  - Config in `Program.cs` (middleware configured with policy "auth")
  - HTTP status: 429 Too Many Requests on limit exceeded

## Monitoring & Observability

**Error Tracking:**
- None detected - No Sentry, Application Insights, or error tracking service configured

**Logs:**
- **Built-in ASP.NET Core Logging**
  - Default level: Information
  - Microsoft.AspNetCore level: Warning
  - Configured in `appsettings.json`
  - Services log via constructor-injected `ILogger<T>`
  - Key log points: IGDB token acquisition, Steam resolution, worker status updates

## CI/CD & Deployment

**Hosting:**
- **Self-hosted or cloud IaaS** - No predefined deployment target
  - Runs on Kestrel (ASP.NET Core built-in server)
  - Can be deployed to: IIS, Docker, Azure App Service, AWS, etc.
  - Frontend: Static SPA (dist/ folder) served via web server

**CI Pipeline:**
- **GitHub Actions** (.github/ directory present)
  - No specific workflows configured (workflows not present in repo)
  - Ready for custom implementation

## Environment Configuration

**Required env vars / Config:**
- Backend configuration via `appsettings.json` and `appsettings.Development.json`:
  - `ConnectionStrings:DefaultConnection` - SQL Server connection
  - `IGDB:ClientId` - IGDB OAuth client ID
  - `IGDB:ClientSecret` - IGDB OAuth client secret
  - `IGDB:BatchSize` - Records per IGDB sync batch (default 500)
  - `Steam:ApiKey` - Steam Web API key
  - `Jwt:Secret` - JWT signing secret (32+ characters)
  - `Jwt:Issuer` - Token issuer (default: GamePlannerAPI)
  - `Jwt:Audience` - Token audience (default: GamePlannerClient)
  - `App:BaseUrl` - Backend API URL (default: https://localhost:7174)
  - `App:FrontendUrl` - Frontend URL for CORS (default: http://localhost:4200)
  - `HLTB:CsvPath` - Path to HowLongToBeat CSV (development only)

- Frontend configuration via `src/environments/environment.ts`:
  - `apiUrl` - Backend API endpoint (default: https://localhost:7174)

**Secrets location:**
- Development: `GamePlanner.API/appsettings.Development.json` (committed to repo — SECURITY RISK)
- Production: Environment variables or Azure Key Vault / equivalent secret management
- Frontend: Baked into environment files at build time (apiUrl safe; no secrets stored)

## Webhooks & Callbacks

**Incoming:**
- None detected - Application does not expose webhook endpoints

**Outgoing:**
- None detected - Application does not call external webhooks

## Background Workers

**Scheduled Sync Operations:**
All registered in `Program.cs` as `IHostedService`:

1. **GameSyncWorker** - Syncs games from IGDB
   - Implementation: `GamePlanner.API/Workers/GameSyncWorker.cs`
   - Frequency: Configured via worker status table
   - Dependencies: `IgdbService`, `GameSyncManager`

2. **GameImageSyncWorker** - Fetches game cover art and screenshots
   - Implementation: `GamePlanner.API/Workers/GameImageSyncWorker.cs`
   - Frequency: Periodic sync of IGDB images
   - Dependencies: `IgdbService`, `GameImageManager`

3. **SteamImportWorker** - Imports Steam library for users
   - Implementation: `GamePlanner.API/Workers/SteamImportWorker.cs`
   - Frequency: Per-user import jobs
   - Dependencies: `SteamService`, `GameImportManager`

4. **GameHLTBSyncWorker** - Enriches games with HowLongToBeat data
   - Implementation: `GamePlanner.API/Workers/GameHLTBSyncWorker.cs`
   - Frequency: Periodic sync
   - Dependencies: `CsvGameService`, `GameHLTBManager`

5. **UpcomingGamesSyncWorker** - Fetches upcoming game releases
   - Implementation: `GamePlanner.API/Workers/UpcomingGamesSyncWorker.cs`
   - Frequency: Periodic sync from IGDB
   - Dependencies: `IgdbService`, `UpcomingGamesManager`

6. **SteamPlaytimeSyncWorker** - Updates playtime for owned games
   - Implementation: `GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs`
   - Frequency: Periodic sync from Steam API
   - Dependencies: `SteamService`, `SteamPlaytimeSyncManager`

## Data Flow

**Game Import (Steam → GamePlanner):**
1. User initiates import via UI → `GameImportManager`
2. `SteamService` fetches owned games from Steam API
3. Games matched/created in database via `GameSyncManager`
4. Images fetched from IGDB via `GameImageSyncWorker`
5. HowLongToBeat data enriched via `GameHLTBSyncWorker`

**Game Enrichment (IGDB → GamePlanner):**
1. Periodic `GameSyncWorker` calls `IgdbService` in batches
2. Genre, theme, perspective, mode data stored in related tables
3. Ratings and release dates added to `Games` table

---

*Integration audit: 2026-08-28*
