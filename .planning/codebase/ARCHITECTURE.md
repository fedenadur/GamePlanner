<!-- refreshed: 2026-08-28 -->
# Architecture

**Analysis Date:** 2026-08-28

## System Overview

```text
┌─────────────────────────────────────────────────────────────┐
│            Angular SPA (Browser)                            │
│    game-planner-ui: Components, Services, Guards            │
├──────────────────────────────────────────────────────────────┤
│ Routing: AuthGuard → Components (Login, Dashboard, Games)   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                    HTTP/JWT
                       │
                       ▼
┌──────────────────────────────────────────────────────────────┐
│   ASP.NET Core 8.0 API (GamePlanner.API)                     │
│  Controllers ↔ Managers ↔ Services ↔ Database (EF Core)      │
├──────────────────────────────────────────────────────────────┤
│ Entry: Program.cs (DI, Auth, CORS, Rate Limiting)           │
│ Routes: /api/[Controller] with JWT Authorization             │
│ Auth: JWT Bearer + Rate Limiting on /auth endpoints         │
└──────────────────────┬──────────────────────────────────────┘
                       │
        SQL Server (GamePlannerDbContext)
                       │
                       ▼
┌──────────────────────────────────────────────────────────────┐
│    External Services (Singletons)                            │
│  - IGDB (game metadata, genres, images)                      │
│  - Steam (auth, playtime, app data)                          │
│  - CSV Service (bulk game import)                            │
│  - Twitch OAuth (IGDB token management)                      │
└──────────────────────────────────────────────────────────────┘
                       │
┌──────────────────────────────────────────────────────────────┐
│   Background Workers (IHostedService)                        │
│  - GameSyncWorker (IGDB sync every 24h)                      │
│  - GameImageSyncWorker (cover download)                      │
│  - SteamImportWorker (import from Steam library)             │
│  - GameHLTBSyncWorker (How Long to Beat data)               │
│  - UpcomingGamesSyncWorker (upcoming releases)               │
│  - SteamPlaytimeSyncWorker (playtime stats)                  │
└──────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | File |
|-----------|----------------|------|
| **AuthController** | JWT login/register, token validation | `GamePlanner.API/Controllers/AuthController.cs` |
| **GamesController** | CRUD games, search IGDB, game details | `GamePlanner.API/Controllers/GamesController.cs` |
| **SessionController** | Gaming session management | `GamePlanner.API/Controllers/SessionController.cs` |
| **GameManager** | Game data retrieval + enrichment (LINQ) | `GamePlanner.API/Managers/GameManager.cs` |
| **SessionManager** | Session planning logic | `GamePlanner.API/Managers/SessionManager.cs` |
| **GameSyncManager** | Sync games with IGDB | `GamePlanner.API/Managers/GameSyncManager.cs` |
| **IgdbService** | IGDB API queries, token caching | `GamePlanner.API/Services/IgdbService.cs` |
| **SteamService** | Steam API queries | `GamePlanner.API/Services/SteamService.cs` |
| **AuthService** | User auth (BCrypt hashing) | `GamePlanner.API/Services/AuthService.cs` |
| **GamePlannerDbContext** | EF Core DbContext, entity mapping | `GamePlanner.API/Context/GamePlannerDbContext.cs` |
| **AuthGuard** | Route protection in frontend | `game-planner-ui/src/app/guards/auth.guard.ts` |
| **GameListComponent** | Display all games, filter/sort | `game-planner-ui/src/app/components/game-list/` |
| **DashboardComponent** | Home/stats overview | `game-planner-ui/src/app/components/dashboard/` |

## Pattern Overview

**Overall:** Layered architecture with separation of concerns

**Key Characteristics:**
- **Stateless API:** Controllers delegate to Managers (business logic)
- **Database per Aggregate:** EF Core + optional Dapper for complex queries
- **External Services as Singletons:** IGDB and Steam services maintain token caching
- **Background Workers:** Long-running tasks (sync, import) via `IHostedService`
- **User Scoping:** Most entities linked to `UserId` for multi-tenancy
- **JWT Authentication:** Stateless; tokens validated on every request
- **Frontend-Backend Coupling:** Angular consumes REST API; no shared DTOs (manual sync required)

## Layers

**Presentation Layer (Frontend):**
- Purpose: Render UI, handle user input, make HTTP requests to API
- Location: `game-planner-ui/src/app/`
- Contains: Components, services, guards, interceptors, directives
- Depends on: HTTP client (via HttpInterceptor)
- Used by: Browser/end users

**API Entry Points (Controllers):**
- Purpose: Route HTTP requests, extract user ID, delegate to managers
- Location: `GamePlanner.API/Controllers/`
- Contains: 15 controller classes (`AuthController`, `GamesController`, etc.)
- Depends on: Managers, Services, DbContext (injected)
- Used by: Frontend HTTP requests

**Business Logic Layer (Managers):**
- Purpose: Implement game/session/user logic using EF LINQ or Dapper
- Location: `GamePlanner.API/Managers/`
- Contains: 18 manager classes (GameManager, SessionManager, GameSyncManager, etc.)
- Depends on: DbContext, Services (external APIs)
- Used by: Controllers, Workers

**Data Access Layer:**
- Purpose: Entity mapping, database queries, persistence
- Location: `GamePlanner.API/Context/`, `GamePlanner.API/Entities/`
- Contains: DbContext (EF Core), 16 entity classes
- Depends on: SQL Server, Microsoft.EntityFrameworkCore
- Used by: Managers

**External Integration Layer (Services):**
- Purpose: API client wrappers (IGDB, Steam, CSV import)
- Location: `GamePlanner.API/Services/`
- Contains: 6 services (IgdbService, SteamService, AuthService, etc.)
- Depends on: HttpClientFactory, configuration
- Used by: Managers, Controllers, Workers

**Background Processing (Workers):**
- Purpose: Long-running sync tasks (IGDB, Steam, HLTB, images, playtime)
- Location: `GamePlanner.API/Workers/`
- Contains: 6 worker classes inheriting `BackgroundService`
- Depends on: Managers, WorkerStatusManager, IServiceScopeFactory
- Used by: ASP.NET Core Host

**Data Transfer (Domain Models):**
- Purpose: API request/response contracts (DTOs)
- Location: `GamePlanner.API/Domain/`, `GamePlanner.API/Mapping/`
- Contains: 15+ DTO classes (GameDto, SessionDto, etc.), mapping extensions
- Depends on: Entity models
- Used by: Controllers (for serialization), Managers (optional)

## Data Flow

### Primary Request Path: Fetch All Games

1. User clicks "Games" → Router → `GameListComponent` loads
2. `GameListComponent` calls `GameService.getAllGames()` (HTTP GET)
3. **AuthInterceptor** adds `Authorization: Bearer <JWT>` header (`game-planner-ui/src/app/interceptors/auth.interceptor.ts`)
4. API receives at `GET /api/games` (`GamePlanner.API/Controllers/GamesController.cs` line 16-21)
5. `GamesController.GetAll()` extracts userId from JWT claims, calls `GameManager.GetAllAsync(userId)`
6. `GameManager.GetAllAsync()` queries DbContext using EF LINQ with includes (genres, tags, themes, etc.) (`GamePlanner.API/Managers/GameManager.cs` line 17-27)
7. EF Core maps SQL result → `Game` entities
8. `GameManager` projects entities to `GameDto` via `.ToDto()` extension (`GamePlanner.API/Mapping/`)
9. Controller returns `Ok(dtos)` → JSON serialization → HTTP 200
10. Frontend receives JSON, `GameService` returns Observable, component binds to `*ngFor`

### Game Import from Steam

1. User navigates to `/steam` → `SteamImportComponent`
2. User clicks "Import Steam Library" → calls `SteamService.initiateOAuth()`
3. OAuth flow redirects to Twitch/Steam, user approves
4. Frontend receives callback with Steam auth token, calls API `POST /api/steam/import`
5. `SteamController.ImportLibrary()` calls `SteamImportManager` → `SteamService.FetchLibraryAsync()`
6. `SteamService` queries Steam API, returns list of games
7. `SteamImportManager` upserts games into `Games` table via stored procedure or EF
8. `SteamImportWorker` (background task) later syncs playtime/details via Steam API

### Background Sync Flow (IGDB Game Data)

1. `GameSyncWorker` triggers every 24 hours (or on-demand via `/api/sync/run`)
2. Worker scope-injects `GameSyncManager` and `WorkerStatusManager`
3. `WorkerStatusManager.SetRunningAsync()` updates DB: `WorkerStatus.Status = 'Running'`
4. `GameSyncManager.RunAsync()` fetches batches from IGDB via `IgdbService.FetchBatchAsync()`
5. `IgdbService` uses cached Twitch OAuth token (renewed if expired) (`GamePlanner.API/Services/IgdbService.cs` line 25-43)
6. For each IGDB game, upserts via stored procedure `sp_UpsertIGDBGame` (Dapper)
7. On success: `WorkerStatusManager.SetCompletedAsync()` updates DB with timestamp
8. On error: `WorkerStatusManager.SetFailedAsync()` logs error message

**State Management:**
- **Frontend State:** Angular component properties + Local Service state (no global store)
- **Backend State:** SQL Server persistent; JWT token holds user identity (stateless)
- **Worker State:** `WorkerStatus` table tracks last run, success/error, for monitoring
- **Token Cache:** `IgdbService._cachedToken` singleton holds Twitch OAuth token with expiry

## Key Abstractions

**Manager Pattern:**
- Purpose: Encapsulate business logic for a domain entity (Game, Session, User, etc.)
- Examples: `GameManager`, `SessionPlannerManager`, `GameEnrichmentManager`
- Pattern: Methods return DTOs; handle EF LINQ queries or Dapper calls internally

**Service Pattern (External APIs):**
- Purpose: Wrap external API clients (IGDB, Steam, CSV)
- Examples: `IgdbService`, `SteamService`
- Pattern: Singletons for token caching; public methods return domain models or DTOs

**Worker Pattern (Background Tasks):**
- Purpose: Scheduled or triggered long-running operations
- Examples: `GameSyncWorker`, `SteamImportWorker`
- Pattern: `IHostedService`; scope-inject managers; update `WorkerStatus` table for monitoring

**DTO Mapping:**
- Purpose: Convert EF Core entities → API response contracts
- Examples: `GameDto`, `SessionDto`, `ProfileDto`
- Pattern: Extension methods `.ToDto()` in `GamePlanner.API/Mapping/`

**Frontend Component Pattern:**
- Purpose: Encapsulate a feature screen or reusable UI piece
- Examples: `GameListComponent`, `SessionPlannerComponent`, `DashboardComponent`
- Pattern: Constructor injection of services; `OnInit` calls service methods; template binds to component properties

## Entry Points

**Backend Entry Point:**
- Location: `GamePlanner.API/Program.cs` (lines 12-106)
- Triggers: `dotnet run` or container start
- Responsibilities:
  - Configure DI (managers, services, DbContext)
  - Set up JWT authentication + rate limiting
  - Configure CORS for Angular frontend (localhost:4200)
  - Register background workers
  - Map controller routes

**Frontend Entry Point:**
- Location: `game-planner-ui/src/main.ts` (lines 1-12)
- Triggers: `ng serve` or build output in browser
- Responsibilities:
  - Bootstrap Angular `AppModule`
  - Load environment config
  - Mount app to DOM

**API Endpoints (15 Controllers):**
- `POST /api/auth/login` — Authenticate user
- `POST /api/auth/register` — Create user account
- `GET /api/games` — List all games (requires JWT)
- `GET /api/games/{id}` — Get game details
- `GET /api/games/{id}/detail` — Full game with HLTB data
- `GET /api/games/igdb/search?name=X` — Search IGDB
- `POST /api/games` — Create game manually
- `PUT /api/games/{id}` — Update game
- `DELETE /api/games/{id}` — Soft delete game
- `POST /api/session/plan` — Plan gaming session
- `GET /api/sync/run` — Trigger game sync (admin)
- `GET /api/worker/status` — Check background worker status
- `POST /api/steam/import` — Import from Steam library
- `GET /api/profile` — Get user profile
- `GET /api/dashboard` — Get dashboard stats
- (and 10+ more in other controllers)

**Routes (Frontend):**
- `/login` — Login form (no guard)
- `/register` — Register form (no guard)
- `/` → `/dashboard` (redirects, guarded)
- `/games` — Game library view (guarded)
- `/planner` — Session planner (guarded)
- `/steam` — Steam import (guarded)
- `/profile` — User profile (guarded)
- `/games/:id` — Game detail (guarded)
- `/calendar` — Gaming calendar (guarded)
- `/now-playing` — Currently playing (guarded)
- `/backlog-prioritizer` — Backlog tools (guarded)
- `/journal` — Gaming journal (guarded)
- `/stats` — Statistics dashboard (guarded)
- `/tracked-games` — Tracked games (guarded)

## Architectural Constraints

- **Threading:** Single-threaded event loop (Angular frontend); ASP.NET Core async/await with thread pool. Background workers use `IHostedService` (run on separate thread in thread pool).
- **Global state:** Frontend has no global state store; backend has `SteamTokenStore` singleton for token caching and `IgdbService._cachedToken` for IGDB OAuth token. `SyncMetadata` table holds sync state.
- **Circular imports:** None detected in backend; frontend uses barrel files in `shared/` without circular deps.
- **User Isolation:** All queries filter by `UserId` (user-scoped data). Multi-tenant via user FK.
- **Concurrency:** Managers use EF Core; no explicit locking. Workers run sequentially (one per type); overlapping runs handled by `WorkerStatusManager` check.
- **Rate Limiting:** Fixed-window limiter on auth endpoints (10 requests per minute).
- **Transactions:** EF Core implicit; Dapper (raw SQL) used for bulk/stored procedures without explicit transaction wrapping.

## Anti-Patterns

### Missing Explicit Error Handling in Components

**What happens:** Frontend components call services but don't explicitly handle HTTP errors or timeouts; errors logged to console only.

**Why it's wrong:** Users see no feedback on failed requests; error state not propagated to UI.

**Do this instead:** Wrap service calls in try-catch in components; set error properties on component; display error messages in template. Example in `GameListComponent`:
```typescript
async ngOnInit() {
  try {
    this.games = await this.gameService.getAllGames().toPromise();
  } catch (error) {
    this.errorMessage = 'Failed to load games. Please try again.';
    this.logger.error('Game load error:', error);
  }
}
```

### Managers Querying Multiple Related Entities Inefficiently

**What happens:** `GameManager.GetAllAsync()` includes all related entities (Genres, Tags, Themes, PlayerPerspectives, GameModes) every time, even if only basic info needed.

**Why it's wrong:** SELECT N+1 problem on large libraries (N games = 5*N queries). Slows down API responses.

**Do this instead:** Create overloads or parameters: `GetAllAsync(userId, includeMetadata=false)` to conditionally include only needed related entities. Use projection to DTOs early.

### Service Constructors with Large DI Lists

**What happens:** Managers injected with DbContext, IConfiguration, multiple services leads to growing constructor signatures.

**Why it's wrong:** Violates Single Responsibility; hard to test; obscures dependencies.

**Do this instead:** Consider factory pattern or service registrations; inject only what's directly needed. Use IServiceProvider for lazy loading of optional services.

## Error Handling

**Strategy:** Try-catch in managers + worker status tracking. Unhandled exceptions propagated to API via `ProblemDetails` (ASP.NET Core default).

**Patterns:**
- **Managers:** Catch `DbUpdateException` → log → throw `InvalidOperationException` with user-friendly message
- **Services (External APIs):** Catch `HttpRequestException` → log → throw with context
- **Workers:** Catch all exceptions → log → update `WorkerStatus` table with error message
- **Frontend:** Subscribe to HTTP errors via `AuthInterceptor`; no global error boundary yet

## Cross-Cutting Concerns

**Logging:**
- Backend: `ILogger<T>` injected; log levels used appropriately (Info for sync start, Warn for cancellation, Error for failures)
- Frontend: `console.log()` / `console.error()` only; no structured logging

**Validation:**
- Backend: EF Core attribute validation on entities (e.g., `[Required]`, `[StringLength]`); manual validation in managers (e.g., null checks, business rule checks)
- Frontend: Template-level validation (HTML5 `required`, `pattern`); no model-level validator yet

**Authentication:**
- Backend: JWT Bearer scheme; token validated in middleware; user ID extracted from claims in controllers
- Frontend: `AuthGuard` checks for token in localStorage; redirects to `/login` if missing

**Authorization:**
- Backend: `[Authorize]` attribute on controllers; no role-based access (all authenticated users have same permissions)
- Frontend: Routes guarded by `AuthGuard`; no role-based component hiding yet

---

*Architecture analysis: 2026-08-28*
