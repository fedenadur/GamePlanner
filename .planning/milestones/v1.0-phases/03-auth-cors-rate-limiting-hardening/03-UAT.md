---
status: complete
phase: 03-auth-cors-rate-limiting-hardening
source: [03-VERIFICATION.md]
started: 2026-08-31T12:35:00Z
updated: 2026-08-31T14:45:00Z
---

## Current Test

[testing complete]

## Tests

### 1. Full manual browser walkthrough of the upgraded Angular SPA against the hardened API
expected: No CORS error appears in the browser DevTools console on any request across any workflow (login, backlog CRUD, session planner, journal entries, IGDB/Steam/HLTB sync views).
result: pass
notes: |
  Automated via Playwright MCP (registered fresh test user, no stale-token reuse) after
  restarting the backend with the `https` launch profile (dotnet run defaulted to http-only,
  which caused ERR_CONNECTION_REFUSED against the SPA's https://localhost:7174 apiUrl — not a
  CORS issue, an environment misconfiguration, fixed by relaunching with --launch-profile https).
  Walked: register -> /games -> IGDB search "Hollow Knight" -> add to library -> game detail
  -> /planner (Suggest Games) -> /journal -> /steam -> /workers -> /now-playing -> /calendar
  -> /tracked-games -> /backlog-prioritizer -> /stats -> /profile. Console errors observed: one
  benign 404 on GET /api/journal/108159 (no journal entries yet for the new game) — not a CORS
  error. Zero CORS errors across all requests/pages.

### 2. Concurrent-request atomicity of the per-user rate-limit partition
expected: Firing a burst of concurrent (parallel, not sequential) mutation requests from the same authenticated user near the 100-permit boundary decrements the partition's permit count exactly once per request — no more than 100 succeed and no fewer than 100 succeed under concurrent load.
result: pass
notes: |
  Automated via Playwright MCP `browser_evaluate`, firing concurrent `fetch()` calls from the
  authenticated page context (Promise.all) against POST /api/session/plan (an "mutations"-policy,
  read-only, side-effect-free endpoint) using the real JWT from localStorage.
  Burst 1: 105 concurrent requests -> 98 succeeded (200), 7 rejected (429). 98 exactly matches
  the remaining permits (100 - 2 already consumed earlier in Test 1's walkthrough: the
  add-to-library call and the Suggest Games call both hit the same per-user "mutations"
  partition). No over-count, no under-count.
  Burst 2 (follow-up): 20 more concurrent requests -> all 20 rejected (429), confirming the
  partition was left at exactly 0 remaining permits with no leakage or double-issuance.
  Total consumed across both bursts + Test 1 = exactly 100, as expected.

### 3. Sign-off on the five judgment-tier prohibitions
expected: |
  Human confirms or overrides each LLM-judge assessment in 03-VERIFICATION.md's
  "Judgment-Tier Prohibitions" table:
  1. No sliding expiration / refresh token defeating the 24h JWT bound
  2. No permissive CORS fallback when App:FrontendUrl is absent/empty
  3. No widened CORS allow-list beyond the four verbs/two headers
  4. No shared-partition collapse in the mutation rate limiter
  5. No distinguishable failed-login response between the two failure branches
result: pass
notes: |
  All five re-verified live via Playwright MCP (not just re-reading the LLM-judge assessment):
  1. Decoded the real JWT payload from localStorage: exp - now = 79345s (~22h, consistent with
     a 24h token partway through its life, not multi-day). Fired several authenticated requests
     and confirmed the stored token was byte-identical before/after — no silent refresh/renewal.
  2. Stopped the API, relaunched with `App__FrontendUrl=""` (env override, appsettings.json left
     untouched) -> app threw `InvalidOperationException: Missing required configuration key
     'App:FrontendUrl'` at startup and never began listening. No permissive fallback exists;
     restarted normally afterward to restore state.
  3. From the running SPA's own origin, attempted a PATCH request and a request with a custom
     header (`X-Custom-Test`) via fetch() -> both blocked by the browser's real CORS preflight
     enforcement ("Failed to fetch"), confirming the allow-list is exactly the 4 verbs/2 headers.
  4. Registered a second throwaway user, drove user 1's per-user partition to full exhaustion
     with a 150-request concurrent burst (99 succeeded + 51 rejected = 150, consistent with the
     window's 100-permit cap), then in the same pass confirmed user 1 immediately got 429 while
     user 2's identical request succeeded (200) — partitions are isolated under real concurrent
     load, not just sequential.
  5. Compared POST /api/auth/login responses for an unknown email vs. a registered email with
     the wrong password: both returned status 401 with byte-identical body "Invalid email or
     password." — no distinguishable branch.
  No overrides — all five LLM-judge assessments confirmed correct by live testing.

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps
