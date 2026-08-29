<!-- GSD:project-start source:PROJECT.md -->

## Project

**GamePlanner — Security Hardening Milestone**

GamePlanner is a personal game-tracking web app (Angular SPA + ASP.NET Core 8 API + SQL Server) that lets a user manage their game backlog, track now-playing sessions, journal entries, and sync metadata from IGDB, Steam, and HowLongToBeat. This milestone is a dedicated security hardening pass over the existing app before any further feature work.

**Core Value:** Close the concrete security and reliability gaps already identified in the codebase (credential handling, CORS/rate limiting, resource leaks, missing audit trail) without breaking existing functionality.

### Constraints

- **Security**: No production users currently, but credential-handling and dependency-EOL fixes still apply regardless of deployment scale — fix as if it will be exposed
- **Scope**: Hardening only — no feature work in this milestone, per explicit user decision
- **Tech stack**: EF LINQ for reads, stored procedures only for insert/update/delete (existing project convention, from CLAUDE.md) — hardening fixes must respect this pattern
- **Testing**: Add only targeted tests for hardening-touched code paths, not a full suite

<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->

## Technology Stack

## Languages

- C# (.NET 8.0) - Backend API (`GamePlanner.API/`)
- TypeScript 4.6.2 - Frontend application (`game-planner-ui/`)
- SQL - Database scripts (`Database/`)
- JavaScript - Node.js tooling (npm scripts, build configuration)

## Runtime

- .NET 8.0 (CLR) - Backend execution
- Node.js - Frontend development and build
- SQL Server (SSMS) - Database runtime
- NuGet (implicit via .NET SDK) - Backend dependencies
- npm - Frontend dependencies
- Lockfile: `game-planner-ui/package-lock.json` (present)

## Frameworks

- ASP.NET Core 8.0 - Web API framework in `GamePlanner.API/`
- Angular 13.3.0 - Frontend SPA framework in `game-planner-ui/`
- Entity Framework Core 8.0 - ORM for database access in `GamePlanner.API/`
- Dapper 2.1.72 - Lightweight micro-ORM for SQL queries in `GamePlanner.API/`
- Swashbuckle.AspNetCore 6.6.2 - Swagger/OpenAPI documentation in `GamePlanner.API/`
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0 - JWT token validation
- BCrypt.Net-Next 4.0.3 - Password hashing in `GamePlanner.API/Services/`
- Jasmine 4.0.0 - Test framework in `game-planner-ui/`
- Karma 6.3.0 - Test runner in `game-planner-ui/`
- Karma Chrome Launcher 3.1.0 - Browser for testing
- Karma Jasmine HTML Reporter 1.7.0 - Test result reporting
- Angular CLI 13.3.1 - Frontend build tooling
- Angular DevKit Build Angular 13.3.1 - Build orchestration
- Angular Compiler CLI 13.3.0 - TypeScript compilation for Angular
- RxJS 7.5.0 - Reactive programming library in `game-planner-ui/`
- Zone.js 0.11.4 - Zone polyfill for Angular in `game-planner-ui/`

## Key Dependencies

- Microsoft.EntityFrameworkCore.SqlServer 8.0.* - EF Core SQL Server provider
- Microsoft.AspNetCore.OpenApi 8.0.26 - OpenAPI/Swagger support
- Microsoft.IdentityModel.Tokens - JWT token validation in `GamePlanner.API/Program.cs`
- Microsoft.EntityFrameworkCore.Tools 8.0.* - EF Core migration tools (dev-only)
- tslib 2.3.0 - TypeScript runtime library in `game-planner-ui/`
- @types/jasmine 3.10.0 - Type definitions for Jasmine
- @types/node 12.11.1 - Type definitions for Node.js

## Configuration

- `GamePlanner.API/appsettings.json` - Production settings
- `GamePlanner.API/appsettings.Development.json` - Development overrides
- `game-planner-ui/src/environments/environment.ts` - Frontend dev config
- `game-planner-ui/src/environments/environment.prod.ts` - Frontend production config
- `game-planner-ui/angular.json` - Angular CLI configuration
- `game-planner-ui/tsconfig.json` - TypeScript configuration
- SQL Server connection string in `ConnectionStrings:DefaultConnection`
- IGDB API credentials in `IGDB:ClientId`, `IGDB:ClientSecret`
- Steam API key in `Steam:ApiKey`
- JWT secret in `Jwt:Secret`
- HLTB CSV path in `HLTB:CsvPath` (development only)
- Backend base URL in `App:BaseUrl`
- Frontend URL in `App:FrontendUrl` for CORS
- `GamePlanner.sln` - Visual Studio solution file
- `GamePlanner.API/GamePlanner.API.csproj` - Backend project configuration
- `game-planner-ui/tsconfig.app.json` - Application TypeScript config
- `game-planner-ui/tsconfig.spec.json` - Test TypeScript config

## Platform Requirements

- Windows (primary) or cross-platform .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code
- SQL Server (2019 or later, or SQL Server Express)
- Node.js 14+ (for frontend development)
- npm 6+ (for frontend package management)
- .NET 8.0 Runtime (or SDK for self-contained deployments)
- SQL Server instance
- IIS or standalone Kestrel hosting
- Node.js + npm for frontend build (or pre-built SPA)
- Nginx/IIS for static file serving of frontend
- HTTP/HTTPS (configured on `https://localhost:7174` locally)
- CORS enabled for frontend on `http://localhost:4200` (development)

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

## Naming Patterns

- C# files: PascalCase matching class name (`GameManager.cs`, `AuthController.cs`)
- TypeScript files: kebab-case with type suffix (`game-detail.component.ts`, `game.service.ts`)
- Database scripts: Numbered prefix with snake_case description (`01_CreateDatabase.sql`, `15_CreateThemesPerspectivesModes.sql`)
- PascalCase: `GameManager`, `AuthController`, `IgdbService`, `GameDto`
- Suffix conventions:
- PascalCase: `GetAllAsync()`, `CreateFromIgdbAsync()`, `UpdateAsync()`
- Async methods end with `Async`: `GetByIdAsync()`, `FetchCoversByIdsAsync()`
- Properties use auto-properties with `{ get; set; }`
- camelCase: `userId`, `newId`, `igdbResults`, `conn`
- Private fields: `_cachedToken`, `_tokenExpiry`
- Constants: UPPER_SNAKE_CASE: `BatchSize`, `TokenUrl`, but `const int` preferred over `private static`
- PascalCase: `Id`, `Name`, `EstimatedMinutes`, `IsActive`
- Nullable types use `?`: `int?`, `string?`, `DateTime?`
- Collection initializers use `[]`: `public List<Genre> Genres { get; set; } = [];`
- camelCase: `getGames()`, `createGame()`, `planSession()`
- Async methods return `Observable<T>` not Promises
- Event handlers: `on<Event>`: `onInit()`, `handleLightboxKeydown()`
- Private methods: `private methodName()`
- Getters: `get propertyName(): Type`
- camelCase: `email`, `password`, `game`, `results`
- Components: `@Input()`, `@Output()`, `@ViewChild()` for template interaction
- Readonly collections: `readonly starPositions = [1, 2, 3, 4, 5];`
- PascalCase: `Game`, `GameDetail`, `IgdbSearchResult`, `SessionRequest`
- Exported from service files where used
- Optional fields use `?`: `coverUrl?: string`, `errorMessage?: string`

## Code Style

- 2-space indentation (Frontend .editorconfig enforced)
- Insert final newline: yes
- Trim trailing whitespace: yes
- No root-level linting config (project-specific only)
- Frontend: No ESLint configuration found
- Backend: No Roslyn analyzers explicitly configured
- TypeScript: Strict mode enabled via `tsconfig.json`
- File-scoped namespaces: `namespace GamePlanner.API.Managers;` (C# 11)
- Nullable reference types enabled: `#nullable enable` in .csproj
- ImplicitUsings enabled: No need to write `using System;`
- Expression-bodied properties: `private string ClientId => config["IGDB:ClientId"]!;`
- Inline initialization: `new()` preferred over `new ClassName()`
- Null-coalescing: `hltb ??= await db.GamesHLTB...` for null assignments
- Single quotes for strings: `'my-string'` (enforced in .editorconfig for `.ts` files)
- Strict null checking enabled
- Type declarations on function parameters: `createGame(request: CreateGameRequest)`
- Observable types explicit: `Observable<Game[]>`, not bare `Observable`

## Import Organization

- Frontend: `baseUrl: "./"` in `tsconfig.json` (relative imports used, e.g., `../../services/`)

## Error Handling

- Try-catch with logging: `catch (Exception ex) { logger.LogWarning(ex, "message"); }`
- Graceful degradation: Failed batch operations retry individually
- Null checks before operations: `if (game is null) return NotFound();`
- Range/existence validation: `if (userId is null) return Unauthorized();`
- Task cancellation: `ct.ThrowIfCancellationRequested();`
- Subscribe error callbacks: `.subscribe({ next: () => {}, error: (err) => {} })`
- Status code checking: `err.status === 401 ? 'Invalid credentials' : 'Server error'`
- Message extraction: `err.error?.message ?? 'default message'`
- UI state management: `loading`, `error` properties updated in error handlers

## Logging

- Injection: `ILogger<GameImageManager> logger` in constructor
- Information level: `logger.LogInformation("message with {Param}", value);`
- Warning level: `logger.LogWarning(ex, "message");` for recoverable errors
- Structured logging: Uses named parameters `{Total}`, `{Processed}` for filtering
- No logging in TypeScript frontend (check console via browser devtools)

## Comments

- Section dividers for logical grouping: `// ── Auth ────────────────────────────────`
- Clarification of non-obvious logic: `// Find HLTB data: prefer SteamAppId match, fallback to Name`
- Business rule explanation: `// Prevent duplicate concurrent imports`
- Implementation intent: `// Fire-and-forget — do NOT await`
- Do NOT comment obvious code: `int id = 42; // set id to 42` ❌
- Minimal use in backend (only `SteamService` has `/// <summary>`)
- Generally not used for service/component documentation
- Focus on code clarity over doc comments

## Function Design

- Use positional parameters for required values
- Use object types for multiple parameters: `CreateGameFromIgdbRequest request` instead of `(string name, int estimatedMinutes, ...)`
- CancellationToken as last parameter in async methods: `async Task FetchAsync(..., CancellationToken ct)`
- Async methods return `Task<T>` or `Task`
- Boolean for success/failure: `bool updated = await manager.UpdateAsync(...)`
- Nullable for optional results: `GameDto? game = await manager.GetByIdAsync(...)`
- Frontend: Always `Observable<T>` from services

## Module Design

- All public classes in dedicated files (one class per file typical)
- Extension method classes static: `public static class GameMapping`
- Manager/Service classes injected via DI container
- Service file exports both service class and its interfaces: `export class GameService {}` + `export interface Game {}`
- Component exports only component: `export class GameDetailComponent {}`
- Shared utilities exported as `export function` or `export const`
- Not used in this project
- Import from specific files directly

## Database & Persistence

- Used ONLY for Insert/Update operations via Dapper
- Named with `sp_` prefix: `sp_InsertGame`, `sp_UpsertIGDBGame`
- Called via Dapper: `await conn.ExecuteScalarAsync<int>("sp_InsertGame", ...)`
- Used for all SELECT queries
- Include navigation properties: `.Include(g => g.Genres).Include(g => g.Tags)`
- Filter at database: `.Where(g => g.IsActive && g.UserId == userId)`
- Sort in query: `.OrderBy(g => g.Name)`
- Numbered SQL files (01_, 02_, ..., 19_)
- Applied manually in SSMS (per CLAUDE.md)
- New migrations added as next numbered file (e.g., 27_...)

<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

## System Overview

```text

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

- **Stateless API:** Controllers delegate to Managers (business logic)
- **Database per Aggregate:** EF Core + optional Dapper for complex queries
- **External Services as Singletons:** IGDB and Steam services maintain token caching
- **Background Workers:** Long-running tasks (sync, import) via `IHostedService`
- **User Scoping:** Most entities linked to `UserId` for multi-tenancy
- **JWT Authentication:** Stateless; tokens validated on every request
- **Frontend-Backend Coupling:** Angular consumes REST API; no shared DTOs (manual sync required)

## Layers

- Purpose: Render UI, handle user input, make HTTP requests to API
- Location: `game-planner-ui/src/app/`
- Contains: Components, services, guards, interceptors, directives
- Depends on: HTTP client (via HttpInterceptor)
- Used by: Browser/end users
- Purpose: Route HTTP requests, extract user ID, delegate to managers
- Location: `GamePlanner.API/Controllers/`
- Contains: 15 controller classes (`AuthController`, `GamesController`, etc.)
- Depends on: Managers, Services, DbContext (injected)
- Used by: Frontend HTTP requests
- Purpose: Implement game/session/user logic using EF LINQ or Dapper
- Location: `GamePlanner.API/Managers/`
- Contains: 18 manager classes (GameManager, SessionManager, GameSyncManager, etc.)
- Depends on: DbContext, Services (external APIs)
- Used by: Controllers, Workers
- Purpose: Entity mapping, database queries, persistence
- Location: `GamePlanner.API/Context/`, `GamePlanner.API/Entities/`
- Contains: DbContext (EF Core), 16 entity classes
- Depends on: SQL Server, Microsoft.EntityFrameworkCore
- Used by: Managers
- Purpose: API client wrappers (IGDB, Steam, CSV import)
- Location: `GamePlanner.API/Services/`
- Contains: 6 services (IgdbService, SteamService, AuthService, etc.)
- Depends on: HttpClientFactory, configuration
- Used by: Managers, Controllers, Workers
- Purpose: Long-running sync tasks (IGDB, Steam, HLTB, images, playtime)
- Location: `GamePlanner.API/Workers/`
- Contains: 6 worker classes inheriting `BackgroundService`
- Depends on: Managers, WorkerStatusManager, IServiceScopeFactory
- Used by: ASP.NET Core Host
- Purpose: API request/response contracts (DTOs)
- Location: `GamePlanner.API/Domain/`, `GamePlanner.API/Mapping/`
- Contains: 15+ DTO classes (GameDto, SessionDto, etc.), mapping extensions
- Depends on: Entity models
- Used by: Controllers (for serialization), Managers (optional)

## Data Flow

### Primary Request Path: Fetch All Games

### Game Import from Steam

### Background Sync Flow (IGDB Game Data)

- **Frontend State:** Angular component properties + Local Service state (no global store)
- **Backend State:** SQL Server persistent; JWT token holds user identity (stateless)
- **Worker State:** `WorkerStatus` table tracks last run, success/error, for monitoring
- **Token Cache:** `IgdbService._cachedToken` singleton holds Twitch OAuth token with expiry

## Key Abstractions

- Purpose: Encapsulate business logic for a domain entity (Game, Session, User, etc.)
- Examples: `GameManager`, `SessionPlannerManager`, `GameEnrichmentManager`
- Pattern: Methods return DTOs; handle EF LINQ queries or Dapper calls internally
- Purpose: Wrap external API clients (IGDB, Steam, CSV)
- Examples: `IgdbService`, `SteamService`
- Pattern: Singletons for token caching; public methods return domain models or DTOs
- Purpose: Scheduled or triggered long-running operations
- Examples: `GameSyncWorker`, `SteamImportWorker`
- Pattern: `IHostedService`; scope-inject managers; update `WorkerStatus` table for monitoring
- Purpose: Convert EF Core entities → API response contracts
- Examples: `GameDto`, `SessionDto`, `ProfileDto`
- Pattern: Extension methods `.ToDto()` in `GamePlanner.API/Mapping/`
- Purpose: Encapsulate a feature screen or reusable UI piece
- Examples: `GameListComponent`, `SessionPlannerComponent`, `DashboardComponent`
- Pattern: Constructor injection of services; `OnInit` calls service methods; template binds to component properties

## Entry Points

- Location: `GamePlanner.API/Program.cs` (lines 12-106)
- Triggers: `dotnet run` or container start
- Responsibilities:
- Location: `game-planner-ui/src/main.ts` (lines 1-12)
- Triggers: `ng serve` or build output in browser
- Responsibilities:
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

```typescript

```

### Managers Querying Multiple Related Entities Inefficiently

### Service Constructors with Large DI Lists

## Error Handling

- **Managers:** Catch `DbUpdateException` → log → throw `InvalidOperationException` with user-friendly message
- **Services (External APIs):** Catch `HttpRequestException` → log → throw with context
- **Workers:** Catch all exceptions → log → update `WorkerStatus` table with error message
- **Frontend:** Subscribe to HTTP errors via `AuthInterceptor`; no global error boundary yet

## Cross-Cutting Concerns

- Backend: `ILogger<T>` injected; log levels used appropriately (Info for sync start, Warn for cancellation, Error for failures)
- Frontend: `console.log()` / `console.error()` only; no structured logging
- Backend: EF Core attribute validation on entities (e.g., `[Required]`, `[StringLength]`); manual validation in managers (e.g., null checks, business rule checks)
- Frontend: Template-level validation (HTML5 `required`, `pattern`); no model-level validator yet
- Backend: JWT Bearer scheme; token validated in middleware; user ID extracted from claims in controllers
- Frontend: `AuthGuard` checks for token in localStorage; redirects to `/login` if missing
- Backend: `[Authorize]` attribute on controllers; no role-based access (all authenticated users have same permissions)
- Frontend: Routes guarded by `AuthGuard`; no role-based component hiding yet

<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

| Skill | Description | Path |
|-------|-------------|------|
| openspec-apply-change | Implement tasks from an OpenSpec change. Use when the user wants to start implementing, continue implementation, or work through tasks. | `.claude/skills/openspec-apply-change/SKILL.md` |
| openspec-archive-change | Archive a completed change in the experimental workflow. Use when the user wants to finalize and archive a change after implementation is complete. | `.claude/skills/openspec-archive-change/SKILL.md` |
| openspec-explore | Enter explore mode - a thinking partner for exploring ideas, investigating problems, and clarifying requirements. Use when the user wants to think through something before or during a change. | `.claude/skills/openspec-explore/SKILL.md` |
| openspec-propose | Propose a new change with all artifacts generated in one step. Use when the user wants to quickly describe what they want to build and get a complete proposal with design, specs, and tasks ready for implementation. | `.claude/skills/openspec-propose/SKILL.md` |
| openspec-sync-specs | Sync delta specs from a change to main specs. Use when the user wants to update main specs with changes from a delta spec, without archiving the change. | `.claude/skills/openspec-sync-specs/SKILL.md` |
| openspec-update-change | Update an OpenSpec change by revising its existing planning artifacts and keeping them coherent with one another. Use when the user wants to revise a change's plan, fold new decisions into it, or reconcile its artifacts after an edit. Never edits code. | `.claude/skills/openspec-update-change/SKILL.md` |
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
