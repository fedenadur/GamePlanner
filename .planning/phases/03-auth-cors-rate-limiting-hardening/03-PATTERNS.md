# Phase 3: Auth, CORS & Rate Limiting Hardening - Pattern Map

**Mapped:** 2026-08-31
**Files analyzed:** 6 (all modified, no new files except one small middleware)
**Analogs found:** 6 / 6 (all via in-repo self-analogs — this phase is a reconfiguration of existing files, not new feature code)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `GamePlanner.API/Program.cs` | config (composition root) | request-response (middleware pipeline) | itself (existing `Program.cs`, being edited in place) | exact — no other composition root exists |
| `GamePlanner.API/Services/AuthService.cs` | service | request-response | `GamePlanner.API/Services/IgdbService.cs` (for `ILogger<T>` injection + structured logging convention) | role-match |
| `GamePlanner.API/Managers/SessionManager.cs` | manager | CRUD (read query) | `GamePlanner.API/Managers/GameManager.cs` (for `UserId`-scoped LINQ query pattern) | exact (same manager tier, same user-scoping concern) |
| `GamePlanner.API/Controllers/SessionController.cs` | controller | request-response | `GamePlanner.API/Controllers/GamesController.cs` / `GameJournalController.cs` (for `GetUserId()` helper pattern) | exact |
| `GamePlanner.API/Controllers/GamesController.cs` | controller | request-response | `GamePlanner.API/Controllers/AuthController.cs` (for `[EnableRateLimiting("...")]` attribute usage) | role-match |
| `GamePlanner.API/Controllers/GameJournalController.cs` | controller | request-response | `GamePlanner.API/Controllers/AuthController.cs` (for `[EnableRateLimiting("...")]` attribute usage) | role-match |
| `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` (NEW) | middleware | request-response | no existing middleware class in repo — pattern taken from RESEARCH.md (official ASP.NET Core custom-middleware shape); logging style matches `IgdbService` | no analog (new file, no `Middleware/` folder exists yet) |

## Pattern Assignments

### `GamePlanner.API/Program.cs` (config, request-response)

**Analog:** itself — current file read in full (106 lines), all target line ranges below refer to the current (pre-change) file.

**Current DI/service registration block** (lines 1-52) — unchanged, no new pattern needed; new middleware class and `AuthService`'s changed constructor don't require new registrations beyond what's already there (`AuthService` is already `AddScoped`).

**Current JWT `TokenValidationParameters`** (lines 53-69):
```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwt["Secret"]!))
        };
    });
```
D-04 extends this object literal in place — add `RequireExpirationTime = true` and `ValidAlgorithms = [SecurityAlgorithms.HmacSha256]` as two more properties in the same initializer. `SecurityAlgorithms` is already imported via `Microsoft.IdentityModel.Tokens` (line 10).

**Current rate limiter registration** (lines 71-81):
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit         = 10;
        o.Window              = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```
This is the pattern to extend, NOT copy verbatim for the new "mutations" policy — RESEARCH.md Pitfall 1 confirms `AddFixedWindowLimiter` doesn't support per-user partitioning. Add a second block using `options.AddPolicy<string>("mutations", httpContext => ...)` (see RESEARCH.md Pattern 2 for the exact partitioner + `OnRejected` shape) inside this same `AddRateLimiter` call. Reuse `System.Threading.RateLimiting` import already at line 2, add `using System.Security.Claims;` for `ClaimTypes.NameIdentifier`.

**Current CORS policy** (lines 83-89):
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
```
D-05/D-06/D-07 change this in place to:
```csharp
policy.WithOrigins(builder.Configuration["App:FrontendUrl"]!)
      .WithHeaders("Authorization", "Content-Type")
      .WithMethods("GET", "POST", "PUT", "DELETE")
```

**Current middleware pipeline order** (lines 99-104):
```csharp
app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```
**Critical reorder required** (RESEARCH.md Pitfall 2, not optional): move `UseAuthentication()` before `UseRateLimiter()` so the new user-partitioned "mutations" policy can read `HttpContext.User`. New order:
```csharp
app.UseHttpsRedirection();
app.UseMiddleware<GamePlanner.API.Middleware.CorsRejectionLoggingMiddleware>();  // NEW, before UseCors
app.UseCors("AllowAngular");
app.UseAuthentication();   // moved up
app.UseRateLimiter();      // moved down
app.UseAuthorization();
app.MapControllers();
```

---

### `GamePlanner.API/Services/AuthService.cs` (service, request-response)

**Analog:** `GamePlanner.API/Services/IgdbService.cs`

**ILogger injection pattern** (`IgdbService.cs` line 8):
```csharp
public class IgdbService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<IgdbService> logger)
```
Apply the same primary-constructor injection style to `AuthService`:
```csharp
public class AuthService(GamePlannerDbContext db, IConfiguration config, ILogger<AuthService> logger)
```

**Structured logging pattern** (`IgdbService.cs` lines 46, 160):
```csharp
logger.LogInformation("IGDB token acquired, expires in {Seconds}s", token.ExpiresIn);
...
logger.LogError("IGDB error {Status}: {Body}", response.StatusCode, body);
```
Follow the same named-parameter convention (`{Email}`, `{Reason}`) for D-14's new `LogWarning` calls.

**Current `LoginAsync`** (`AuthService.cs` lines 37-51) — the two branches to instrument:
```csharp
public async Task<AuthResponse?> LoginAsync(LoginRequest request)
{
    var user = await db.Users
        .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive);

    if (user is null || user.PasswordHash is null) return null;
    if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return null;
    ...
}
```
Add `logger.LogWarning("Failed login attempt for {Email}: {Reason}", request.Email, "user not found or inactive");` before the first `return null;`, and the wrong-password equivalent before the second. Never log `request.Password`.

**Current `GenerateToken`** (`AuthService.cs` line 71):
```csharp
expires:  DateTime.UtcNow.AddDays(7),
```
D-01 changes this single line to `DateTime.UtcNow.AddHours(24)`.

---

### `GamePlanner.API/Managers/SessionManager.cs` (manager, CRUD)

**Analog:** `GamePlanner.API/Managers/GameManager.cs` (user-scoping convention — every read method takes a `userId` parameter and filters by it; confirmed via `GamesController` call sites `manager.GetAllAsync(userId.Value)`, `manager.GetByIdAsync(id, userId.Value)`).

**Current unfiltered query** (`SessionManager.cs` lines 10-18):
```csharp
public async Task<SessionRecommendation> GetRecommendationAsync(SessionRequest request)
{
    var mood = request.Mood.Trim().ToLower();

    var candidates = await db.Games
        .Where(g => g.IsActive && g.EstimatedMinutes <= request.AvailableMinutes)
        .Include(g => g.Genres)
        .Include(g => g.Tags)
        .ToListAsync();
```
Fix (folded todo): add a `userId` parameter to the method signature and a `&& g.UserId == userId` clause to the `.Where(...)`:
```csharp
public async Task<SessionRecommendation> GetRecommendationAsync(SessionRequest request, int userId)
{
    ...
    var candidates = await db.Games
        .Where(g => g.IsActive && g.UserId == userId && g.EstimatedMinutes <= request.AvailableMinutes)
        ...
```

---

### `GamePlanner.API/Controllers/SessionController.cs` (controller, request-response)

**Analog:** `GamePlanner.API/Controllers/GamesController.cs` and `GameJournalController.cs` — both have an identical private `GetUserId()` helper (`GamesController.cs` lines 95-99, `GameJournalController.cs` lines 58-62):
```csharp
private int? GetUserId()
{
    var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return int.TryParse(claim, out var id) ? id : null;
}
```
`SessionController.cs` has no such helper today (RESEARCH.md Pitfall 5). Add this exact helper plus `using System.Security.Claims;`, and wire the `Recommend` action (`SessionController.cs` lines 13-21) to call it and pass `userId.Value` into `manager.GetRecommendationAsync(request, userId.Value)`, matching the `Unauthorized()`-on-null guard style used in `GamesController`:
```csharp
[HttpPost("recommend")]
public async Task<IActionResult> Recommend([FromBody] SessionRequest request)
{
    if (request.AvailableMinutes <= 0 || string.IsNullOrWhiteSpace(request.Mood))
        return BadRequest("AvailableMinutes and Mood are required.");

    var userId = GetUserId();
    if (userId is null) return Unauthorized();

    var result = await manager.GetRecommendationAsync(request, userId.Value);
    return Ok(result);
}
```

**Rate limiting attribute** — same as Games/Journal controllers below; apply `[EnableRateLimiting("mutations")]` to `Recommend` and `Plan` (both are POST, in-scope per D-09's controller-level wording — note D-09 says "Games + Journal + Session controllers... POST/PUT/DELETE", so both actions qualify since both are `[HttpPost]`).

---

### `GamePlanner.API/Controllers/GamesController.cs` (controller, request-response)

**Analog:** `GamePlanner.API/Controllers/AuthController.cs` — existing `[EnableRateLimiting("auth")]` usage (`AuthController.cs` lines 13, 33):
```csharp
[HttpPost("register")]
[EnableRateLimiting("auth")]
public async Task<IActionResult> Register([FromBody] RegisterRequest request)
```
Apply the identical attribute pattern (different policy name) to `GamesController`'s mutation actions — `CreateFromIgdb` (line 60), `Create` (line 69), `Update` (line 78), `Delete` (line 87):
```csharp
[HttpPost]
[EnableRateLimiting("mutations")]
public async Task<IActionResult> Create([FromBody] CreateGameRequest request)
```
Requires `using Microsoft.AspNetCore.RateLimiting;` added to the controller's import block (already present in `AuthController.cs` line 4).

---

### `GamePlanner.API/Controllers/GameJournalController.cs` (controller, request-response)

**Analog:** same as `GamesController` above — `AuthController.cs`'s `[EnableRateLimiting("auth")]` pattern. Apply `[EnableRateLimiting("mutations")]` to `CreateEntry` (line 31), `UpdateEntry` (line 40), `DeleteEntry` (line 49). Add `using Microsoft.AspNetCore.RateLimiting;` to the file's imports.

---

### `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` (NEW — middleware, request-response)

**No existing analog** — this is a net-new file/folder (no `Middleware/` directory exists in the repo today). Use RESEARCH.md's fully-specified pattern verbatim (already grounded against the codebase's conventions: primary-constructor DI matching `IgdbService`'s style, `ILogger<T>` + named-parameter logging matching `IgdbService.cs` line 160's `logger.LogError("IGDB error {Status}: {Body}", ...)` style):
```csharp
namespace GamePlanner.API.Middleware;

public class CorsRejectionLoggingMiddleware(RequestDelegate next, ILogger<CorsRejectionLoggingMiddleware> logger, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        var allowedOrigin = config["App:FrontendUrl"];

        if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, allowedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "CORS rejection: Origin {Origin} does not match configured {AllowedOrigin} for {Method} {Path}",
                origin, allowedOrigin, context.Request.Method, context.Request.Path);
        }

        await next(context);
    }
}
```
Registered in `Program.cs` via `app.UseMiddleware<GamePlanner.API.Middleware.CorsRejectionLoggingMiddleware>();` immediately before `app.UseCors("AllowAngular");` (see Program.cs pattern assignment above). File-scoped namespace `GamePlanner.API.Middleware` matches the project's file-scoped-namespace convention (`namespace GamePlanner.API.Managers;` etc., per CLAUDE.md Code Style).

**Caveat (must carry into plan):** only log when `Origin` header is non-empty — guards against logging every non-browser request (Swagger UI, curl) as a false-positive CORS rejection.

---

## Shared Patterns

### `GetUserId()` controller helper
**Source:** `GamePlanner.API/Controllers/GamesController.cs` lines 95-99 (identical copy also in `GameJournalController.cs` lines 58-62)
**Apply to:** `SessionController.cs` (currently missing this helper — RESEARCH.md Pitfall 5)
```csharp
private int? GetUserId()
{
    var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
    return int.TryParse(claim, out var id) ? id : null;
}
```

### `[EnableRateLimiting("policyName")]` attribute usage
**Source:** `GamePlanner.API/Controllers/AuthController.cs` lines 13, 33 (existing `"auth"` policy)
**Apply to:** All mutation (POST/PUT/DELETE) actions in `GamesController`, `GameJournalController`, `SessionController` — new `"mutations"` policy name, same attribute mechanism.

### `ILogger<T>` + named-parameter structured logging
**Source:** `GamePlanner.API/Services/IgdbService.cs` lines 8, 46, 160
**Apply to:** `AuthService.cs` (D-14 failed-login logging), `Program.cs`'s `OnRejected` rate-limit callback (D-15), and the new `CorsRejectionLoggingMiddleware` (D-16) — all three use this exact `logger.Log{Level}("message with {Param}", value)` convention, never string interpolation.

### User-scoped LINQ query pattern
**Source:** `GamePlanner.API/Managers/GameManager.cs` (every read method takes `userId` and filters `.Where(g => ... && g.UserId == userId)`)
**Apply to:** `SessionManager.GetRecommendationAsync` (folded todo fix) — same shape, add `userId` parameter + `&& g.UserId == userId` clause.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `GamePlanner.API/Middleware/CorsRejectionLoggingMiddleware.cs` | middleware | request-response | No `Middleware/` folder or custom middleware class exists anywhere in this codebase yet — this phase introduces the project's first one. Use RESEARCH.md's fully-specified, project-convention-aligned pattern (embedded above) as the primary source instead of an in-repo analog. |

## Metadata

**Analog search scope:** `GamePlanner.API/Program.cs`, `GamePlanner.API/Services/`, `GamePlanner.API/Controllers/`, `GamePlanner.API/Managers/`
**Files scanned:** `Program.cs`, `AuthService.cs`, `IgdbService.cs`, `SessionManager.cs`, `GameManager.cs` (referenced), `SessionController.cs`, `GamesController.cs`, `GameJournalController.cs`, `AuthController.cs`
**Pattern extraction date:** 2026-08-31
