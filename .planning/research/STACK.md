# Stack Research

**Domain:** Security hardening pass on an existing ASP.NET Core 8 API + Angular 13.3.0 SPA (brownfield, personal/local use)
**Researched:** 2026-08-28
**Confidence:** MEDIUM-HIGH (backend hardening findings cross-checked against Microsoft Learn/NuGet; Angular version data cross-checked against angular.dev + HeroDevs EOL tracker; no Context7 MCP available this session, WebSearch used with cross-verification instead)

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `Microsoft.AspNetCore.RateLimiting` (built-in, no NuGet package) | ships with ASP.NET Core 8.0 (already referenced via `AddRateLimiter`/`UseRateLimiter`) | Rate limiting for auth + data-mutation endpoints | Already partially wired up in this codebase for `/auth`. It's in-box, zero extra dependency, supports Fixed Window, Sliding Window, Token Bucket, and Concurrency algorithms, and can partition by user identity via `PartitionedRateLimiter`. For a single-instance personal app there is no need for a distributed limiter (Redis-backed) — the in-memory partitioned limiter is the correct scope match. **Confidence: HIGH** (Microsoft Learn, cross-checked against 3 independent tutorials). |
| `Microsoft.Extensions.Http.Resilience` 10.9.0 | Latest as of Aug 2026, targets `net8.0` directly (confirmed multi-target, not .NET 9/10-only) | Resiliency wrapper (retry, circuit breaker, timeout) around `HttpClient` calls to IGDB/Steam/HLTB | This is the official successor to the old `Microsoft.Extensions.Http.Polly` package, built on Polly v8's `ResiliencePipeline`. `AddStandardResilienceHandler()` gives a sensible default pipeline (rate limiter, total timeout, retry w/ exponential backoff, circuit breaker, per-attempt timeout) in one call registered on `IHttpClientFactory`, replacing the need to hand-roll Polly policies. Directly addresses the CONCERNS.md finding about broad `catch (Exception)` with no retry/circuit-breaker around `IgdbService`/`SteamService`/`GameImageManager`. **Confidence: HIGH** (NuGet package page + Microsoft .NET blog, cross-checked). |
| `Polly.Core` 8.7.0 (transitive via the package above; do not add directly unless you need custom pipelines beyond the standard handler) | Current stable Polly v8 line | Underlying resilience-strategy engine | Polly v8 is a from-scratch rewrite (old `Policy`/`PolicyWrap` API is gone) unified around `ResiliencePipeline`, designed for zero-allocation and used by `Microsoft.Extensions.Http.Resilience` under the hood. You will rarely touch this directly — use the `Microsoft.Extensions.Http.Resilience` DI extensions instead. **Confidence: HIGH**. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Net.Http.Json` (already in .NET 8 BCL) | built-in | `PostAsync`/`PostAsJsonAsync` with `FormUrlEncodedContent` for OAuth token requests | Use to move IGDB's Twitch OAuth `client_id`/`client_secret`/`grant_type` and Steam's `key=` parameter out of the URL. See "What NOT to Use" / rationale below — this is a defense-in-depth improvement, not fixing a functional bug. |
| `Microsoft.Extensions.Options` `IOptionsMonitor<T>` (built-in) | Strongly-typed CORS-origin + rate-limit config bound from `appsettings.{Environment}.json` | Environment-scoped CORS/config | Use to bind an `AllowedOrigins: string[]` (and similar rate-limit knobs) section per environment instead of the hardcoded `WithOrigins("http://localhost:4200")` in `Program.cs`. Standard ASP.NET Core pattern — no extra package needed. |
| Angular CLI `ng update` schematics (built-in to `@angular/cli`) | run once per major version during the upgrade | Automated Angular 13→21 migration | Each `ng update @angular/core@N @angular/cli@N` step runs code-mod schematics that fix known breaking changes automatically. This is the only supported upgrade mechanism — do not hand-edit package.json versions and expect it to work. |
| `@angular/core:karma-to-vitest` (schematic, ships with Angular 21 CLI) | run once, optional | Migrate test runner off Karma | See Angular section below — only apply after landing on Angular 21, and only if you choose to modernize the test runner in this milestone (optional, can be deferred). |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `angular.dev/update-guide` | Generates the exact step-by-step instructions (schematics + manual fixes) for a specific `from → to` Angular version pair | Run this for each hop (13→14, 14→15, …) rather than trying to plan the whole path from memory — breaking changes are version-pair-specific. |
| xUnit + Moq (already flagged in CONCERNS.md, not new) | Targeted tests for hardened auth/manager/service code paths | Out of scope for this STACK.md (owned by testing dimension), but resilience-wrapped `HttpClient` calls are more testable via `Microsoft.Extensions.Http.Resilience`'s injectable pipeline than hand-rolled try/catch, which is a secondary benefit of the resilience library choice. |

## Installation

```bash
# Backend — resiliency
dotnet add GamePlanner.API package Microsoft.Extensions.Http.Resilience --version 10.9.0

# Rate limiting middleware: no install needed — Microsoft.AspNetCore.RateLimiting
# ships inside the ASP.NET Core 8 shared framework, already partially wired up
# in Program.cs (extend existing AddRateLimiter policies, don't add a package).

# Frontend — Angular upgrade (run once per major version, sequentially)
cd game-planner-ui
npx @angular/cli@14 update @angular/core@14 @angular/cli@14
# ...repeat for 15, 16, 17, 18, 19, 20, 21, checking angular.dev/update-guide
# for manual steps at each hop before moving to the next.

# Optional, after reaching Angular 21, only if modernizing test runner in-scope:
ng generate @angular/core:karma-to-vitest
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| ASP.NET Core built-in `RateLimiting` middleware | `AspNetCoreRateLimit` (third-party NuGet, IP/client-rules-based) | Only if you need config-file-driven rules across many endpoints without writing partition-key code, or need distributed (Redis) counters across multiple app instances. Not needed here — single-instance personal deployment, and the built-in middleware already covers `/auth`; extending it to more endpoints is a smaller diff than swapping libraries. |
| `Microsoft.Extensions.Http.Resilience` (Polly v8 wrapper) | Hand-rolled `Polly` v8 `ResiliencePipeline` directly | Use raw Polly only if you need a resilience strategy the standard/HTTP-specific handler doesn't expose (e.g., a non-HTTP resource, custom hedging strategy). For wrapping `HttpClient` calls to IGDB/Steam/HLTB, the standard handler covers retry+circuit-breaker+timeout in one call — less code to maintain. |
| Angular 21 (target LTS) | Angular 20 | Angular 20 is also valid — it's in LTS through Nov 2026 — but it has a shorter remaining support runway than 21 (through May 2027) and still defaults to Karma. Choose 20 only if you specifically want to avoid the zoneless-by-default / Vitest-by-default changes introduced in 21 and are willing to re-upgrade again sooner. |
| `FormUrlEncodedContent` in POST body for IGDB/Steam credentials | Leave credentials in query string (current state) | Never, for this milestone — see Pitfalls/CONCERNS.md. Note: Steam's Web API officially supports both `key=` query param and `x-webapi-key` header as equally valid per Steamworks docs, and Twitch's OAuth docs specify body-form params even though IGDB's own examples show query params. Moving both to header/body is a best-practice hardening change, not a fix for broken functionality — do not describe it to stakeholders as "was insecure/non-functional," it's "hardened against credential leakage via logs/proxies/history." |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Old `Microsoft.Extensions.Http.Polly` package (Polly v7-based) | Deprecated in favor of `Microsoft.Extensions.Http.Resilience`; Polly v7's `Policy`/`PolicyWrap` API was fully replaced by v8's `ResiliencePipeline`; mixing v7 and v8 Polly packages in one project causes API confusion and duplicate dependencies | `Microsoft.Extensions.Http.Resilience` (wraps Polly v8) |
| Skipping Angular major versions (e.g., `npm install @angular/core@21` directly from 13) | Angular does not support skip-version upgrades; each major version's schematics assume the previous major's shape. Skipping guarantees broken builds and undocumented runtime errors. | Sequential `ng update` through every major version: 13→14→15→16→17→18→19→20→21 |
| Karma + `karma-chrome-launcher` as a long-term choice for *new* test setup | Karma has been deprecated by its own maintainers since April 2023 and Angular's `@angular/build:karma` builder is treated as end-of-life by the Angular team; Angular 21 ships Vitest as the default for new projects | Migrate to Vitest via `ng generate @angular/core:karma-to-vitest` once on Angular 21 (optional in this milestone — existing Karma setup still runs on 21, migration can be deferred to a follow-up if time-boxed) |
| `AllowAnyOrigin()` combined with `AllowCredentials()` in CORS policy | Browsers reject this combination outright, and even where accepted historically it's the textbook CORS misconfiguration exploited for CSRF-adjacent attacks | Explicit `WithOrigins(allowedOriginsFromConfig)` per environment; since this app uses JWT Bearer (Authorization header, not cookies), `AllowCredentials()` likely isn't needed at all — verify before adding it |
| `AllowAnyHeader()` / `AllowAnyMethod()` in production CORS policy | Overly broad — allows any custom header and any HTTP verb from the allowed origin, more attack surface than needed | `WithHeaders("Authorization", "Content-Type")` and `WithMethods("GET", "POST", "PUT", "DELETE")` (or whatever the actual API surface uses) |
| Distributed rate limiter / Redis-backed token store as part of *this* milestone | Explicitly out of scope per PROJECT.md — single-instance personal deployment, no current scaling need | In-memory `PartitionedRateLimiter` (rate limiting) and periodic cleanup timer on the existing `ConcurrentDictionary` (`SteamTokenStore`) — both already the right scope for this app |

## Stack Patterns by Variant

**If keeping the app single-instance / personal-use (current scope):**
- Use the in-memory built-in `RateLimiting` middleware, partitioned by JWT user-id claim (fallback to host/IP for unauthenticated routes).
- Use a simple `PeriodicTimer`/`IHostedService` cleanup loop for `SteamTokenStore` expired-entry removal (no Redis needed).
- Because this project has zero third-party Angular UI libraries (`package.json` shows only `@angular/*`, `rxjs`, `tslib`, `zone.js` — no Material, no NgRx, no UI kit), the sequential `ng update` path has very few third-party compatibility blockers; budget time for the schematics + manual breaking-change fixes per hop, not for dependency-conflict resolution.

**If this app is ever deployed to multiple instances (future, out of scope now):**
- Swap the in-memory rate limiter for a Redis-backed distributed limiter (e.g., `RedisRateLimiting` or a custom `IDistributedCache`-backed partition store) — note this now as a documented follow-up, don't build it speculatively.
- Move `SteamTokenStore` to a distributed cache (Redis) instead of `ConcurrentDictionary` — also already flagged in CONCERNS.md as a scaling limit, correctly deferred.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Angular 21 | TypeScript ~5.9, RxJS ~7.8, zone.js ~0.15 (or removed if opting into zoneless — optional, not required by the upgrade) | Confirm exact pinned ranges via `angular.dev/reference/versions` for the specific 20→21 hop; the official Angular docs compatibility table is the single source of truth and is refreshed every release — do not hardcode exact patch versions into planning docs, let `ng update` pick them. |
| Angular 21 CLI | Node.js ^20.19.0 \|\| ^22.12.0 \|\| ^24.0.0 (verify exact range at time of upgrade via `angular.dev/reference/versions`) | Node 12 (currently pinned via `@types/node ^12.11.1` in this repo) is far below what any Angular 14+ requires — Node upgrade on the dev machine is a prerequisite, not just a package.json bump. |
| Angular 21 default test runner | Vitest (Karma still supported via explicit `@angular/build:karma` builder) | Existing Jasmine *spec syntax* (`describe`/`it`/`expect`) is unaffected by a Karma→Vitest runner swap — Vitest is Jasmine/Jest-syntax compatible, so existing/new spec files don't need a rewrite, only the runner config changes. |
| `Microsoft.Extensions.Http.Resilience` 10.9.0 | .NET 8.0 (also net9, net10, netstandard2.0, net462) | Package versioning follows the "aligned with latest .NET" scheme (10.x ships alongside .NET 10 tooling) but is multi-targeted and confirmed to run on net8.0 — safe to add to this net8.0 project without bumping the target framework. |
| ASP.NET Core built-in `RateLimiting` | ASP.NET Core 8.0+ (already the project's version) | No version compatibility concern — it's part of the shared framework already in use. |

## Sources

- Microsoft Learn — Rate limiting middleware in ASP.NET Core (https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) — confirmed algorithms (Fixed/Sliding/Token Bucket/Concurrency) and partition-by-user pattern. Confidence: HIGH.
- Microsoft .NET Blog — Building resilient cloud services with .NET 8 (https://devblogs.microsoft.com/dotnet/building-resilient-cloud-services-with-dotnet-8/) — confirmed `Microsoft.Extensions.Http.Resilience` is the Polly v8-based successor to `Microsoft.Extensions.Http.Polly`, and `AddStandardResilienceHandler` default pipeline contents. Confidence: HIGH.
- NuGet Gallery — Microsoft.Extensions.Http.Resilience package page (https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) — confirmed current version 10.9.0 and net8.0 target compatibility. Confidence: HIGH.
- NuGet Gallery — Polly package page (https://www.nuget.org/packages/polly/) — confirmed current stable line 8.7.0. Confidence: HIGH.
- Multiple CORS how-to articles cross-checked (oneuptime.com, antondevtips.com, mykolaaleksandrov.dev) — confirmed environment-based appsettings CORS pattern and the `AllowAnyOrigin()+AllowCredentials()` anti-pattern warning appears consistently across independent sources. Confidence: MEDIUM (web tutorials, cross-verified across 3+ independent sources, not single-source).
- angular.dev/reference/releases and angular.dev/reference/versions (official Angular docs) — confirmed Angular 21 released Nov 2025, LTS through May 2027; Angular 20 LTS through Nov 2026; sequential upgrade requirement. Confidence: HIGH (official vendor docs).
- HeroDevs Blog — Angular Version History (https://www.herodevs.com/blog-posts/angular-version-history-every-release-date-support-window-and-end-of-life-date-from-angularjs-to-angular-22) — cross-check for EOL dates, independent of angular.dev. Confidence: MEDIUM (third-party, but consistent with official source).
- angular.dev/guide/testing/migrating-to-vitest (official Angular docs) — confirmed Karma deprecation status (deprecated since April 2023 by Karma's own maintainers), Vitest as Angular 21 default, and the `karma-to-vitest` schematic. Confidence: HIGH.
- Steamworks Documentation — Authentication using Web API Keys (https://partner.steamgames.com/doc/webapi_overview/auth) — confirmed Steam Web API accepts both `key=` query param and `x-webapi-key` header as equally valid. Confidence: HIGH (official vendor docs).
- Twitch Developers — Getting OAuth Access Tokens (https://dev.twitch.tv/docs/authentication/getting-tokens-oauth/) and IGDB API docs (https://api-docs.igdb.com/) — confirmed Twitch's own docs specify body-form params for the token endpoint IGDB re-uses, while IGDB's examples show query params (both work; body/header is the more defensible hardening choice). Confidence: MEDIUM (mixed/conflicting vendor examples, WebSearch-only, no Context7 MCP available this session).
- RFC 6749 (OAuth 2.0 Authorization Framework), datatracker.ietf.org — confirmed HTTP Basic auth is the MUST-support client-authentication method, body params are MAY-support; both outrank query-string credentials for security. Confidence: HIGH (primary spec source).

---
*Stack research for: ASP.NET Core 8 + Angular security-hardening milestone (GamePlanner)*
*Researched: 2026-08-28*
