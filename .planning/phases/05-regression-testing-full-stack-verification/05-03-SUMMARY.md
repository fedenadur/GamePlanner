---
phase: 05-regression-testing-full-stack-verification
plan: 03
subsystem: testing
tags: [xunit, httpclientfactory, logging, steam, igdb, credential-redaction]
status: complete
requirements: [TEST-01]

requires:
  - phase: 05-regression-testing-full-stack-verification
    provides: "ApiFactory, RecordingHandler, CapturingLoggerProvider, StubHttpClientFactory (plan 05-01)"
  - phase: 02-credential-secrets-hardening
    provides: "CRED-01/CRED-02/REL-03 contract for IgdbService and SteamService"
provides:
  - "Fix: System.Net.Http.HttpClient logging raised to Warning so the Steam API key no longer reaches Information logs"
  - "SteamServiceContractTests: real-pipeline leak test + transport and failure-surfacing facts"
  - "IgdbServiceContractTests: token form body, headers, caching, failure surfacing, no secret/token in URIs, headers or logs"
affects: [05-04, 05-06]

tech-stack:
  added: []
  patterns: ["Redaction assertions run through the real IHttpClientFactory logging handlers, not only a stub factory", "Fresh IgdbService per fact because the token is cached per instance"]

key-files:
  created:
    - GamePlanner.Tests/SteamServiceContractTests.cs
    - GamePlanner.Tests/IgdbServiceContractTests.cs
  modified:
    - GamePlanner.API/appsettings.json

key-decisions:
  - "Leak closed by configuration only (Logging:LogLevel:System.Net.Http.HttpClient = Warning); Default stays Information, Microsoft.AspNetCore stays Warning; no service code changed"

metrics:
  tasks: 2
  commits: [d384e61, 785f5e8]
  tests: "35 passed (21 prior + 14 new)"
  completed: 2026-10-24
---

# Phase 5 Plan 3: Steam/IGDB credential contract and Steam key log-leak fix Summary

The Steam API key was written to Information-level logs by HttpClientFactory's own request logging on every Steam call; a real-pipeline test proved it red, one `appsettings.json` key closed it, and 14 offline facts now lock the IGDB and Steam transport and failure-surfacing contract.

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 (tracer) | Prove the Steam key leaks through the real HttpClient pipeline, close with one logging key | d384e61 |
| 2 | Stub-based Steam and IGDB transport and failure-surfacing contract | 785f5e8 |

## Tracer evidence (red then green)

Pre-fix run of `GetOwnedGames_ThroughRealHttpClientPipeline_NeverLogsApiKey` failed on assertion (c); assertions (a) key-in-request and (b) provider-wired passed first, so the failure was not vacuous. Failing message (key replaced by `<redacted>`):

```
Steam key leaked into logs: System.Net.Http.HttpClient.Default.LogicalHandler [Information] Start processing HTTP request GET https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key=<redacted>&steamid=76561198000000001&include_appinfo=true&format=json
HTTP GET https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key=<redacted>&steamid=... | System.Net.Http.HttpClient.Default.ClientHandler [Information] Sending HTTP request GET ...?key=<redacted>&...
... | System.Net.Http.HttpClient.Default.ClientHandler [Information] Received HTTP response headers after 1.975ms - 200
... | System.Net.Http.HttpClient.Default.LogicalHandler [Information] End processing HTTP request after 10.3471ms - 200
```

Four entries leaked the key (LogicalHandler start/end, ClientHandler send/receive). The URL also appeared in the logging scope (the text after `HTTP GET ...` on each entry is the scope), so scope capture through `AddProvider` was observable (the flagged scope-capture assumption held).

After adding `"System.Net.Http.HttpClient": "Warning"`: leak test passed (1/1), full project passed (22/22). Tracer verified end-to-end, then expanded (auto-verification per orchestrator instruction; no human checkpoint).

## Verification

- `dotnet test GamePlanner.Tests/GamePlanner.Tests.csproj`: 35 passed, 0 failed (8 Steam facts, 6 IGDB facts, 21 from 05-01).
- `appsettings.json` parses as JSON; `Logging:LogLevel` is `Default: Information`, `Microsoft.AspNetCore: Warning`, `System.Net.Http.HttpClient: Warning`.
- `SteamService.cs` and `IgdbService.cs` unchanged (`git diff HEAD` clean).
- Handler-level facts confirm: `HttpRequestException` messages from `EnsureSuccessStatusCode` do not include the URL (flagged Pitfall 4 assumption now asserted, not assumed).

## Notes for the user

- `GamePlanner.API/appsettings.Development.json` (gitignored) has its own `Logging:LogLevel` block with only `Default` and `Microsoft.AspNetCore`. .NET configuration merges by key, so the base `System.Net.Http.HttpClient` key still applies in Development; no edit needed. If that block is ever changed to define the same key, it must stay at Warning or higher.
- Restart the API to pick up the new logging key (the dev API was stopped for the build; see below).

## Deviations from Plan

**1. [Rule 3 - Blocking] Stopped the running developer API process**
- **Found during:** pre-build
- **Issue:** The dev API (PID 34340, listening on 7174/5127) locks `GamePlanner.API.dll`, blocking `dotnet test`.
- **Fix:** Stopped that process as the plan convention directs; not restarted (plan 05-06 restarts the dev stack).

**2. [Ordering] Steam facts written in two commits**
- Task 1 commit contains only the leak test plus the config fix; the remaining Steam facts landed with Task 2, as the plan specifies. No behavior change from the plan.

Otherwise: plan executed as written. No behavior line failed against current code, so no hardening regression was found.

## Known Stubs

None.

## Threat Flags

None. T-05-10 (mitigated, red-then-green), T-05-11, T-05-12, T-05-14 (mitigated by facts), T-05-13 accepted as planned.

## Self-Check: PASSED

- FOUND: GamePlanner.Tests/SteamServiceContractTests.cs, GamePlanner.Tests/IgdbServiceContractTests.cs, GamePlanner.API/appsettings.json key
- FOUND commits: d384e61, 785f5e8
