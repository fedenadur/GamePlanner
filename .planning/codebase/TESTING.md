# Testing Patterns

**Analysis Date:** 2026-08-28

## Test Framework

**Runner:**
- Backend: Not detected
- Frontend: Jasmine 4.0.0 with Karma 6.3.0 (configured but no tests written)
- Config: `game-planner-ui/karma.conf.js`

**Status:**
- **No tests exist in this codebase**
- Backend: No test project (`.csproj`)
- Frontend: Framework installed but `skipTests: true` in `angular.json` (lines 10–31)

## Test Infrastructure Configuration

**Frontend Setup (Configured but Unused):**

```json
// angular.json — schematics configuration
"@schematics/angular:component": {
  "skipTests": true
},
"@schematics/angular:service": {
  "skipTests": true
}
```

This means all generated components/services exclude `.spec.ts` files.

**Available npm Commands:**
```bash
npm test              # Would run Karma (if tests existed)
npm start             # ng serve
npm build             # Production build
npm watch             # Build with watch
```

**Run Tests (If Implemented):**
```bash
cd game-planner-ui && npm test
```

## Test File Organization

**Not Applicable — No Tests**

Current state:
- No `*.test.ts` or `*.spec.ts` files in `game-planner-ui/`
- No test classes in `GamePlanner.API/`
- No test project directory

**If Tests Were Added:**
- Frontend: Co-located alongside component: `game-detail.component.spec.ts` next to `game-detail.component.ts`
- Backend: Would need separate `GamePlanner.Tests.csproj` or `[Project].Tests/` directory structure

## Coverage

**Requirements:** Not defined or enforced

**View Coverage (If Implemented):**
```bash
npm test -- --code-coverage
# Coverage would be in game-planner-ui/coverage/
```

Frontend `angular.json` build includes Karma coverage plugin configuration, but no coverage thresholds are set.

## Test Types

**Not Implemented:**
- Unit Tests: No isolated component/service tests
- Integration Tests: No database/API integration tests
- E2E Tests: No end-to-end test suite

## What Should Be Tested (Recommendations for Future Implementation)

**Backend Priority Areas:**
- `GamePlanner.API/Managers/GameManager.cs`: CRUD operations on games
- `GamePlanner.API/Services/IgdbService.cs`: External API calls, token caching, error handling
- `GamePlanner.API/Services/SteamService.cs`: Steam API integration, game data mapping
- `GamePlanner.API/Controllers/*`: Authorization, request validation, HTTP status codes
- `GamePlanner.API/Mapping/GameMapping.cs`: DTO transformation logic

**Frontend Priority Areas:**
- `game-planner-ui/src/app/services/game.service.ts`: HTTP calls, request/response handling
- `game-planner-ui/src/app/components/login/login.component.ts`: Error handling, navigation on auth failure
- `game-planner-ui/src/app/components/game-detail/game-detail.component.ts`: State management, image carousel logic
- Authentication guards: Permission checks on routes

## Error Handling in Untested Code

**Backend Patterns (No Tests):**

From `GamePlanner.API/Managers/GameImageManager.cs`:
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

**Untested Behavior:**
- Exception is silently caught; batch fallback to individual queries
- No test verifies this fallback logic works
- No test for API timeout handling

**Frontend Patterns (No Tests):**

From `game-planner-ui/src/app/components/login/login.component.ts`:
```typescript
this.auth.login(this.email, this.password).subscribe({
  next:  () => this.afterLogin(),
  error: (err) => {
    this.error = err.status === 401 ? 'Invalid email or password.'
                 : typeof err.error === 'string' ? err.error
                 : err.error?.message ?? 'Could not connect to server.';
    this.loading = false;
  }
});
```

**Untested Behavior:**
- No test verifies error message formatting by status code
- No test for loading state cleanup on error
- No test for navigation flow after authentication

## Mocking Needs (If Tests Were Added)

**Backend:**
- Database context (`GamePlannerDbContext`) — would need mock for EF queries
- Http client for IGDB/Steam APIs — use `HttpClientFactory` mocking
- Configuration (`IConfiguration`) — would need configuration values mocked
- Logger (`ILogger<T>`) — mock to verify log calls

**Frontend:**
- `HttpClient` — mock GET/POST responses
- `ActivatedRoute` — mock route parameters for component navigation
- Service methods — spy on calls with jasmine.spyOn()
- Observables — use marble testing with RxJS testing utilities

## Test Architecture (Reference for Future)

**Backend Testing Approach (Recommended):**
- xUnit or NUnit for test framework
- Moq for mocking
- FluentAssertions for readable assertions
- Arrange-Act-Assert pattern

**Frontend Testing Approach (Recommended):**
- Jasmine (already configured)
- Karma runner (already configured)
- TestBed for component testing
- HttpTestingController for HTTP mocking

## Known Untested Critical Paths

**High Risk (No Test Coverage):**

| Area | File | Risk |
|------|------|------|
| Game creation from IGDB | `GamePlanner.API/Managers/GameManager.cs` (lines 92–121) | Database deadlocks, duplicate key violations not validated |
| External API resilience | `GamePlanner.API/Services/IgdbService.cs` (lines 47–75) | Token expiry, rate limiting, network failures untested |
| Auth flow (Steam + JWT) | `GamePlanner.API/Controllers/AuthController.cs` | OAuth callback validation, token generation not verified |
| Form submission & errors | `game-planner-ui/src/app/components/login/` | Error state transitions, XSS in error messages not tested |
| Image carousel | `game-planner-ui/src/app/components/game-detail/` (lines 90–95) | Keyboard nav, array bounds, lightbox state transitions untested |

---

*Testing analysis: 2026-08-28*
