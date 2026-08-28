# Coding Conventions

**Analysis Date:** 2026-08-28

## Naming Patterns

**Files:**
- C# files: PascalCase matching class name (`GameManager.cs`, `AuthController.cs`)
- TypeScript files: kebab-case with type suffix (`game-detail.component.ts`, `game.service.ts`)
- Database scripts: Numbered prefix with snake_case description (`01_CreateDatabase.sql`, `15_CreateThemesPerspectivesModes.sql`)

**Classes & Types (C#):**
- PascalCase: `GameManager`, `AuthController`, `IgdbService`, `GameDto`
- Suffix conventions:
  - `Manager`: Business logic classes (`GameManager`, `SessionManager`, `GameEnrichmentManager`)
  - `Service`: External API integrations only (`IgdbService`, `SteamService`, `AuthService`)
  - `Controller`: API endpoints (`GamesController`, `AuthController`)
  - `Dto`: Data transfer objects in `Domain/` folder
  - `Entity`: Database entities in `Entities/` folder
  - Managers are NOT Services

**Functions & Methods (C#):**
- PascalCase: `GetAllAsync()`, `CreateFromIgdbAsync()`, `UpdateAsync()`
- Async methods end with `Async`: `GetByIdAsync()`, `FetchCoversByIdsAsync()`
- Properties use auto-properties with `{ get; set; }`

**Variables (C#):**
- camelCase: `userId`, `newId`, `igdbResults`, `conn`
- Private fields: `_cachedToken`, `_tokenExpiry`
- Constants: UPPER_SNAKE_CASE: `BatchSize`, `TokenUrl`, but `const int` preferred over `private static`

**Properties (C#):**
- PascalCase: `Id`, `Name`, `EstimatedMinutes`, `IsActive`
- Nullable types use `?`: `int?`, `string?`, `DateTime?`
- Collection initializers use `[]`: `public List<Genre> Genres { get; set; } = [];`

**Functions & Methods (TypeScript):**
- camelCase: `getGames()`, `createGame()`, `planSession()`
- Async methods return `Observable<T>` not Promises
- Event handlers: `on<Event>`: `onInit()`, `handleLightboxKeydown()`
- Private methods: `private methodName()`
- Getters: `get propertyName(): Type`

**Variables (TypeScript):**
- camelCase: `email`, `password`, `game`, `results`
- Components: `@Input()`, `@Output()`, `@ViewChild()` for template interaction
- Readonly collections: `readonly starPositions = [1, 2, 3, 4, 5];`

**Interfaces (TypeScript):**
- PascalCase: `Game`, `GameDetail`, `IgdbSearchResult`, `SessionRequest`
- Exported from service files where used
- Optional fields use `?`: `coverUrl?: string`, `errorMessage?: string`

## Code Style

**Formatting:**
- 2-space indentation (Frontend .editorconfig enforced)
- Insert final newline: yes
- Trim trailing whitespace: yes
- No root-level linting config (project-specific only)

**Linting:**
- Frontend: No ESLint configuration found
- Backend: No Roslyn analyzers explicitly configured
- TypeScript: Strict mode enabled via `tsconfig.json`

**C# Style Specifics:**
- File-scoped namespaces: `namespace GamePlanner.API.Managers;` (C# 11)
- Nullable reference types enabled: `#nullable enable` in .csproj
- ImplicitUsings enabled: No need to write `using System;`
- Expression-bodied properties: `private string ClientId => config["IGDB:ClientId"]!;`
- Inline initialization: `new()` preferred over `new ClassName()`
- Null-coalescing: `hltb ??= await db.GamesHLTB...` for null assignments

**TypeScript Style Specifics:**
- Single quotes for strings: `'my-string'` (enforced in .editorconfig for `.ts` files)
- Strict null checking enabled
- Type declarations on function parameters: `createGame(request: CreateGameRequest)`
- Observable types explicit: `Observable<Game[]>`, not bare `Observable`

## Import Organization

**C# Order:**
1. System namespaces: `using System;`, `using System.Collections.Generic;`
2. External packages: `using Dapper;`, `using Microsoft.EntityFrameworkCore;`
3. Project namespaces: `using GamePlanner.API.Domain;`, `using GamePlanner.API.Managers;`
4. File-scoped namespace at end: `namespace GamePlanner.API.Managers;`

**TypeScript Order:**
1. Angular core imports: `import { Component, Input } from '@angular/core';`
2. Angular modules: `import { HttpClient } from '@angular/common/http';`
3. RxJS: `import { Observable } from 'rxjs';`
4. Local services/types: `import { GameService, Game } from '../../services/game.service';`
5. Shared utilities: `import { genreMeta } from '../../shared/genre-meta';`

**Path Aliases:**
- Frontend: `baseUrl: "./"` in `tsconfig.json` (relative imports used, e.g., `../../services/`)

## Error Handling

**Backend Patterns:**
- Try-catch with logging: `catch (Exception ex) { logger.LogWarning(ex, "message"); }`
- Graceful degradation: Failed batch operations retry individually
- Null checks before operations: `if (game is null) return NotFound();`
- Range/existence validation: `if (userId is null) return Unauthorized();`
- Task cancellation: `ct.ThrowIfCancellationRequested();`

**Frontend Patterns:**
- Subscribe error callbacks: `.subscribe({ next: () => {}, error: (err) => {} })`
- Status code checking: `err.status === 401 ? 'Invalid credentials' : 'Server error'`
- Message extraction: `err.error?.message ?? 'default message'`
- UI state management: `loading`, `error` properties updated in error handlers

## Logging

**Framework:** .NET built-in `ILogger<T>` injected via constructor

**Patterns:**
- Injection: `ILogger<GameImageManager> logger` in constructor
- Information level: `logger.LogInformation("message with {Param}", value);`
- Warning level: `logger.LogWarning(ex, "message");` for recoverable errors
- Structured logging: Uses named parameters `{Total}`, `{Processed}` for filtering
- No logging in TypeScript frontend (check console via browser devtools)

## Comments

**When to Comment:**
- Section dividers for logical grouping: `// ── Auth ────────────────────────────────`
- Clarification of non-obvious logic: `// Find HLTB data: prefer SteamAppId match, fallback to Name`
- Business rule explanation: `// Prevent duplicate concurrent imports`
- Implementation intent: `// Fire-and-forget — do NOT await`
- Do NOT comment obvious code: `int id = 42; // set id to 42` ❌

**XMLDoc/TSDoc:**
- Minimal use in backend (only `SteamService` has `/// <summary>`)
- Generally not used for service/component documentation
- Focus on code clarity over doc comments

## Function Design

**Size:** Typically 10–50 lines in managers; larger methods in services allowed for API integration

**Parameters:**
- Use positional parameters for required values
- Use object types for multiple parameters: `CreateGameFromIgdbRequest request` instead of `(string name, int estimatedMinutes, ...)`
- CancellationToken as last parameter in async methods: `async Task FetchAsync(..., CancellationToken ct)`

**Return Values:**
- Async methods return `Task<T>` or `Task`
- Boolean for success/failure: `bool updated = await manager.UpdateAsync(...)`
- Nullable for optional results: `GameDto? game = await manager.GetByIdAsync(...)`
- Frontend: Always `Observable<T>` from services

## Module Design

**Exports (C#):**
- All public classes in dedicated files (one class per file typical)
- Extension method classes static: `public static class GameMapping`
- Manager/Service classes injected via DI container

**Exports (TypeScript):**
- Service file exports both service class and its interfaces: `export class GameService {}` + `export interface Game {}`
- Component exports only component: `export class GameDetailComponent {}`
- Shared utilities exported as `export function` or `export const`

**Barrel Files:**
- Not used in this project
- Import from specific files directly

## Database & Persistence

**Stored Procedures:**
- Used ONLY for Insert/Update operations via Dapper
- Named with `sp_` prefix: `sp_InsertGame`, `sp_UpsertIGDBGame`
- Called via Dapper: `await conn.ExecuteScalarAsync<int>("sp_InsertGame", ...)`

**EF LINQ:**
- Used for all SELECT queries
- Include navigation properties: `.Include(g => g.Genres).Include(g => g.Tags)`
- Filter at database: `.Where(g => g.IsActive && g.UserId == userId)`
- Sort in query: `.OrderBy(g => g.Name)`

**Migrations:**
- Numbered SQL files (01_, 02_, ..., 19_)
- Applied manually in SSMS (per CLAUDE.md)
- New migrations added as next numbered file (e.g., 27_...)

---

*Convention analysis: 2026-08-28*
