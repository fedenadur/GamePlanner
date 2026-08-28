# Codebase Structure

**Analysis Date:** 2026-08-28

## Directory Layout

```
GamePlanner/
├── GamePlanner.API/                    # ASP.NET Core backend
│   ├── Program.cs                      # Entry point, DI, middleware
│   ├── GamePlanner.API.csproj          # Project file, NuGet deps
│   ├── Controllers/                    # 15 API route handlers
│   │   ├── AuthController.cs
│   │   ├── GamesController.cs
│   │   ├── SessionController.cs
│   │   ├── SteamController.cs
│   │   ├── DashboardController.cs
│   │   └── ... (10 more)
│   ├── Managers/                       # Business logic (18 classes)
│   │   ├── GameManager.cs              # Game CRUD + enrichment
│   │   ├── GameSyncManager.cs          # IGDB sync
│   │   ├── SessionPlannerManager.cs    # Session planning
│   │   ├── GameImageManager.cs         # Cover download
│   │   └── ... (14 more)
│   ├── Services/                       # External API clients (6 classes)
│   │   ├── IgdbService.cs              # IGDB API wrapper
│   │   ├── SteamService.cs             # Steam API wrapper
│   │   ├── AuthService.cs              # BCrypt auth
│   │   └── ... (3 more)
│   ├── Workers/                        # Background tasks (6 classes)
│   │   ├── GameSyncWorker.cs           # IGDB sync every 24h
│   │   ├── GameImageSyncWorker.cs      # Download game covers
│   │   ├── SteamImportWorker.cs        # Import Steam library
│   │   ├── GameHLTBSyncWorker.cs       # How Long to Beat
│   │   ├── UpcomingGamesSyncWorker.cs  # Upcoming releases
│   │   └── SteamPlaytimeSyncWorker.cs  # Playtime tracking
│   ├── Context/
│   │   └── GamePlannerDbContext.cs     # EF Core DbContext
│   ├── Entities/                       # EF Core model classes (16 classes)
│   │   ├── Game.cs
│   │   ├── User.cs
│   │   ├── Genre.cs
│   │   ├── Tag.cs
│   │   ├── GameHLTB.cs
│   │   └── ... (11 more)
│   ├── Domain/                         # DTOs + API models (15+ files)
│   │   ├── GameDto.cs
│   │   ├── SessionDto.cs
│   │   ├── IgdbModels.cs
│   │   ├── SteamModels.cs
│   │   └── ... (11 more)
│   ├── Mapping/                        # Extension methods for ToDto()
│   │   └── (mapping extensions for each entity)
│   └── Properties/
│       └── launchSettings.json         # Dev server config
│
├── game-planner-ui/                    # Angular frontend
│   ├── src/
│   │   ├── main.ts                     # Entry point
│   │   ├── app/
│   │   │   ├── app.module.ts           # Root module
│   │   │   ├── app.component.ts        # Root component
│   │   │   ├── app-routing.module.ts   # Routes config
│   │   │   ├── components/             # 18 feature components
│   │   │   │   ├── dashboard/
│   │   │   │   ├── game-list/
│   │   │   │   ├── game-detail/
│   │   │   │   ├── game-card/          # Reusable card
│   │   │   │   ├── session-planner/
│   │   │   │   ├── steam-import/
│   │   │   │   ├── login/
│   │   │   │   ├── register/
│   │   │   │   ├── profile/
│   │   │   │   ├── sidebar/
│   │   │   │   ├── calendar/
│   │   │   │   ├── now-playing/
│   │   │   │   ├── backlog-prioritizer/
│   │   │   │   ├── journal/
│   │   │   │   ├── stats-dashboard/
│   │   │   │   ├── tracked-games/
│   │   │   │   ├── workers/
│   │   │   │   └── igdb-search-modal/  # Reusable modal
│   │   │   ├── services/               # HTTP client wrappers (11 files)
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── game.service.ts
│   │   │   │   ├── dashboard.service.ts
│   │   │   │   └── ... (8 more)
│   │   │   ├── guards/
│   │   │   │   └── auth.guard.ts       # Route protection
│   │   │   ├── interceptors/
│   │   │   │   └── auth.interceptor.ts # Add JWT header
│   │   │   ├── directives/
│   │   │   │   └── click-outside.directive.ts
│   │   │   └── shared/                 # Reusable utilities (if any)
│   │   ├── environments/
│   │   │   ├── environment.ts          # Dev config
│   │   │   └── environment.prod.ts     # Prod config
│   │   └── assets/                     # Static images, icons
│   ├── angular.json                    # Angular CLI config
│   ├── package.json                    # npm dependencies
│   └── tsconfig.json                   # TypeScript config
│
├── Database/                           # SQL migrations (34 numbered files)
│   ├── 01_CreateDatabase.sql
│   ├── 02_CreateTables.sql
│   ├── 03_StoredProcedures.sql
│   ├── 04_SeedData.sql
│   ├── 05_AlterGames_AddIGDBColumns.sql
│   ├── ... (numbered sequentially)
│   ├── 32_CreateGameImages.sql
│   ├── 33_RemoveUniqueIndex_IGDBId.sql
│   └── 34_FixStoredProcedure_UpsertIGDBGame.sql
│
├── GamePlanner.sln                     # Visual Studio solution file
├── CLAUDE.md                           # Project conventions
├── WIREFRAMES.md                       # UI wireframes
└── .planning/                          # GSD documentation
    └── codebase/                       # This file + others
```

## Directory Purposes

**GamePlanner.API/:**
- Purpose: REST API backend, business logic, database access
- Contains: Controllers, Managers, Services, Workers, EF Core models
- Key files: `Program.cs` (startup), `GamePlannerDbContext.cs` (database)

**GamePlanner.API/Controllers/:**
- Purpose: HTTP route handlers; extract user ID from JWT; delegate to managers
- Contains: 15 controller classes
- Key files:
  - `AuthController.cs` — Login, register
  - `GamesController.cs` — Game CRUD, search
  - `SessionController.cs` — Gaming sessions
  - `DashboardController.cs` — Dashboard stats
  - `SyncController.cs` — Trigger background sync
  - Others: CalendarController, NowPlayingController, ProfileController, StatsController, etc.

**GamePlanner.API/Managers/:**
- Purpose: Business logic encapsulation; query DB via EF Core or Dapper
- Contains: 18 manager classes
- Key files:
  - `GameManager.cs` — Game queries, enrichment
  - `SessionPlannerManager.cs` — Session planning logic
  - `GameSyncManager.cs` — Sync with IGDB
  - `GameImageManager.cs` — Download/cache covers
  - `ImportJobManager.cs` — Track bulk imports
  - Others: NowPlayingManager, TrackedGamesManager, GameJournalManager, etc.

**GamePlanner.API/Services/:**
- Purpose: External API clients (IGDB, Steam, auth)
- Contains: 6 service classes (singletons for token caching)
- Key files:
  - `IgdbService.cs` — IGDB API queries, Twitch OAuth token management
  - `SteamService.cs` — Steam API queries
  - `AuthService.cs` — Password hashing (BCrypt)
  - `SteamAuthService.cs` — Steam OAuth flow
  - `CsvGameService.cs` — Parse CSV bulk imports

**GamePlanner.API/Workers/:**
- Purpose: Background long-running tasks triggered by timer or API call
- Contains: 6 `IHostedService` workers
- Key files:
  - `GameSyncWorker.cs` — Daily IGDB sync (24-hour interval)
  - `GameImageSyncWorker.cs` — Download game cover images
  - `SteamImportWorker.cs` — Import user's Steam library
  - `GameHLTBSyncWorker.cs` — Sync How Long to Beat data
  - `UpcomingGamesSyncWorker.cs` — Fetch upcoming releases
  - `SteamPlaytimeSyncWorker.cs` — Sync playtime metrics

**GamePlanner.API/Context/:**
- Purpose: EF Core database abstraction
- Contains: `GamePlannerDbContext.cs` (single file)
- Defines: 16 DbSet<Entity> properties, relationship configuration

**GamePlanner.API/Entities/:**
- Purpose: EF Core model classes (database schema in code)
- Contains: 16 entity classes
- Key files:
  - `Game.cs` — Main game entity
  - `User.cs` — User account
  - `Genre.cs`, `Tag.cs`, `Theme.cs` — Metadata
  - `GameHLTB.cs` — Playtime estimates
  - `UpcomingGame.cs`, `NowPlayingGame.cs`, `TrackedGame.cs` — User-specific data
  - `GameImage.cs` — Cover images
  - Others: GameJournalEntry, ImportJob, WorkerStatus, SyncMetadata

**GamePlanner.API/Domain/:**
- Purpose: Data Transfer Objects (DTOs) for API responses
- Contains: 15+ DTO classes + request models
- Key files:
  - `GameDto.cs` — Game response model
  - `SessionDto.cs` — Session response
  - `IgdbModels.cs` — IGDB API response models
  - `SteamModels.cs` — Steam API response models
  - Others: ProfileDto, AuthDto, DashboardDto, etc.

**GamePlanner.API/Mapping/:**
- Purpose: Extension methods to convert entities to DTOs
- Contains: One file per entity with `.ToDto()` method
- Pattern: `public static GameDto ToDto(this Game entity)`

**game-planner-ui/src/:**
- Purpose: Angular frontend source code
- Contains: Components, services, guards, interceptors

**game-planner-ui/src/app/:**
- Purpose: Root of Angular app
- Key files:
  - `app.module.ts` — Root module, imports, providers
  - `app.component.ts` — Root component (sidebar + router-outlet)
  - `app-routing.module.ts` — Route definitions with AuthGuard

**game-planner-ui/src/app/components/:**
- Purpose: Feature components (screens, modals, reusable cards)
- Contains: 18 component folders
- Key files:
  - Each component has `.ts`, `.html`, `.css` files
  - `sidebar/` — Navigation sidebar
  - `dashboard/` — Home/stats page
  - `game-list/` — Library view
  - `game-detail/` — Single game view + edit
  - `game-card/` — Reusable game card widget
  - `login/`, `register/` — Auth screens
  - `session-planner/` — Plan gaming sessions
  - `steam-import/` — Bulk import from Steam
  - Others: calendar, journal, stats-dashboard, backlog-prioritizer, etc.

**game-planner-ui/src/app/services/:**
- Purpose: HTTP client wrappers (call backend API)
- Contains: 11 service classes
- Key files:
  - `auth.service.ts` — Login/register calls
  - `game.service.ts` — Game CRUD calls
  - `dashboard.service.ts` — Dashboard data
  - `worker.service.ts` — Worker status monitoring
  - Others: profile, calendar, journal, stats, etc.

**game-planner-ui/src/app/guards/:**
- Purpose: Route protection (authentication)
- Contains: `auth.guard.ts`
- Logic: Check localStorage for JWT token; redirect to login if missing

**game-planner-ui/src/app/interceptors/:**
- Purpose: Middleware for HTTP requests
- Contains: `auth.interceptor.ts`
- Logic: Add `Authorization: Bearer <token>` header to all requests

**game-planner-ui/src/environments/:**
- Purpose: Environment-specific configuration
- Contains: `environment.ts` (dev), `environment.prod.ts` (production)
- Usage: API base URL, feature flags, etc.

**Database/:**
- Purpose: SQL Server schema migrations
- Contains: 34 numbered .sql files
- Naming: `NN_Description.sql` (e.g., `01_CreateDatabase.sql`, `32_CreateGameImages.sql`)
- Execution: Run manually in SSMS in sequence (numbered order)

## Key File Locations

**Entry Points:**
- `GamePlanner.API/Program.cs` — Backend startup (DI, middleware, worker registration)
- `game-planner-ui/src/main.ts` — Frontend bootstrap (AppModule)
- `game-planner-ui/src/app/app.component.ts` — Root component (renders sidebar + router)

**Configuration:**
- `GamePlanner.API/GamePlanner.API.csproj` — NuGet packages, target framework
- `game-planner-ui/package.json` — npm dependencies, Angular CLI scripts
- `game-planner-ui/angular.json` — Angular build config (assets, styles, scripts)
- `game-planner-ui/src/environments/environment.ts` — API base URL, dev settings

**Core Logic:**
- `GamePlanner.API/Managers/` — All business logic (game sync, session planning, etc.)
- `GamePlanner.API/Services/` — External API integration (IGDB, Steam)
- `GamePlanner.API/Workers/` — Background scheduled tasks
- `game-planner-ui/src/app/components/` — Feature screens
- `game-planner-ui/src/app/services/` — API calls

**Testing:**
- No test files found (not yet implemented)
- Recommended paths if added:
  - Backend: `GamePlanner.API/Tests/` or `GamePlanner.Tests.Unit/`
  - Frontend: `game-planner-ui/src/app/**/*.spec.ts`

**Database:**
- `Database/` — All schema migrations (01_ through 34_)
- Applied manually via SSMS or EF Core Migrations (currently manual)

## Naming Conventions

**Files:**
- Controllers: `<Name>Controller.cs` (e.g., `GamesController.cs`)
- Managers: `<Name>Manager.cs` (e.g., `GameSyncManager.cs`)
- Services: `<Name>Service.cs` (e.g., `IgdbService.cs`)
- Workers: `<Name>Worker.cs` (e.g., `GameSyncWorker.cs`)
- Entities: `<Name>.cs` (e.g., `Game.cs`, singular)
- DTOs: `<Name>Dto.cs` (e.g., `GameDto.cs`)
- Frontend Components: `<kebab-case>/` folder with `.component.ts`, `.component.html`, `.component.css`
- Migrations: `NN_Description.sql` (e.g., `32_CreateGameImages.sql`)

**Directories:**
- Backend layers: lowercase singular (`Controllers`, `Managers`, `Services`, `Workers`, `Entities`, `Domain`, `Mapping`, `Context`)
- Frontend: lowercase with hyphens (`components`, `services`, `guards`, `interceptors`, `directives`, `shared`, `environments`, `assets`)

**Namespaces (Backend):**
- Controllers: `GamePlanner.API.Controllers`
- Managers: `GamePlanner.API.Managers`
- Services: `GamePlanner.API.Services`
- Entities: `GamePlanner.API.Entities`
- Domain: `GamePlanner.API.Domain`
- Workers: `GamePlanner.API.Workers`

**Route Naming (API):**
- Pattern: `[Route("api/[controller]")]` + HTTP verb
- Examples:
  - `GET /api/games` — List
  - `GET /api/games/{id}` — Get by ID
  - `POST /api/games` — Create
  - `PUT /api/games/{id}` — Update
  - `DELETE /api/games/{id}` — Delete
  - `GET /api/games/igdb/search` — Custom query

**Component Naming (Frontend):**
- Pattern: `<feature>Component` or `<feature><Type>Component`
- Examples: `GameListComponent`, `SessionPlannerComponent`, `GameCardComponent`
- Route path: Match component name in kebab-case (e.g., `/games` for GameListComponent)

## Where to Add New Code

**New API Feature (e.g., game ratings system):**
1. **Entity:** Add `RatingEntity.cs` to `GamePlanner.API/Entities/`
2. **DbContext:** Add `DbSet<Rating>` to `GamePlannerDbContext.cs`; configure relationships in `OnModelCreating()`
3. **Manager:** Create `RatingManager.cs` in `GamePlanner.API/Managers/` with business logic (GetByGameId, Create, Update, Delete using EF LINQ)
4. **DTO:** Add `RatingDto.cs` to `GamePlanner.API/Domain/`
5. **Mapping:** Add `.ToDto()` extension in `GamePlanner.API/Mapping/`
6. **Controller:** Create `RatingsController.cs` in `GamePlanner.API/Controllers/` with HTTP endpoints (GET, POST, PUT, DELETE)
7. **Database:** Add migration file `NN_CreateRatings.sql` to `Database/` (run manually in SSMS)
8. **Frontend Service:** Create `rating.service.ts` in `game-planner-ui/src/app/services/`
9. **Frontend Component:** Create `ratings/` folder in `game-planner-ui/src/app/components/` with `.ts`, `.html`, `.css`
10. **Route:** Add route to `game-planner-ui/src/app/app-routing.module.ts` if new page-level component

**New Background Worker (e.g., sync gameplay achievements):**
1. Create `AchievementSyncWorker.cs` in `GamePlanner.API/Workers/` inheriting `BackgroundService`
2. Create `AchievementManager.cs` in `GamePlanner.API/Managers/` with sync logic
3. Register in `Program.cs`: `builder.Services.AddHostedService<AchievementSyncWorker>();`
4. Add `WorkerStatus` seed for the worker name (or auto-create on first run)
5. Expose status via `GET /api/sync/status` endpoint

**New Component/Module:**
1. Create folder in `game-planner-ui/src/app/components/<name>/`
2. Generate files: `<name>.component.ts`, `<name>.component.html`, `<name>.component.css`
3. Declare in `app.module.ts` declarations array
4. Add route to `app-routing.module.ts` if navigable
5. Create `<name>.service.ts` in `game-planner-ui/src/app/services/` if needs API calls

**Shared Utilities:**
- Reusable logic: Add to `GamePlanner.API/Managers/` (backend) or create new service in `GamePlanner.API/Services/`
- Frontend utilities: Create in `game-planner-ui/src/app/shared/` if not yet organized

## Special Directories

**Database/:**
- Purpose: SQL Server schema evolution via manual migrations
- Generated: No (manually written)
- Committed: Yes (part of version control)
- Execution: Run scripts in SSMS in numeric order (01, 02, 03, ..., 34)

**GamePlanner.API/bin/ and obj/:**
- Purpose: Build output (compiled DLLs, intermediate objects)
- Generated: Yes (by `dotnet build`)
- Committed: No (in .gitignore)

**game-planner-ui/node_modules/:**
- Purpose: npm package cache
- Generated: Yes (by `npm install`)
- Committed: No (in .gitignore)

**game-planner-ui/dist/:**
- Purpose: Production build output
- Generated: Yes (by `ng build`)
- Committed: No (in .gitignore)

**.planning/:**
- Purpose: GSD documentation, codebase maps, phase plans
- Generated: Yes (by GSD tools)
- Committed: Likely yes (for team reference)

---

*Structure analysis: 2026-08-28*
