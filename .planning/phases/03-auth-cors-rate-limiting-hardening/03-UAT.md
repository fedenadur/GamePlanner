---
status: testing
phase: 03-auth-cors-rate-limiting-hardening
source: [03-VERIFICATION.md]
started: 2026-08-31T12:35:00Z
updated: 2026-08-31T12:35:00Z
---

## Current Test

number: 1
name: Full manual browser walkthrough of the upgraded Angular SPA against the hardened API
expected: |
  Log in fresh in the browser (to avoid a stale-token false positive), then exercise every
  major workflow — login, backlog CRUD, session planner, journal entries, IGDB/Steam/HLTB
  sync views — with the browser DevTools console open. No CORS error should appear in the
  console on any request across any workflow.
awaiting: user response

## Tests

### 1. Full manual browser walkthrough of the upgraded Angular SPA against the hardened API
expected: No CORS error appears in the browser DevTools console on any request across any workflow (login, backlog CRUD, session planner, journal entries, IGDB/Steam/HLTB sync views).
result: [pending]

### 2. Concurrent-request atomicity of the per-user rate-limit partition
expected: Firing a burst of concurrent (parallel, not sequential) mutation requests from the same authenticated user near the 100-permit boundary decrements the partition's permit count exactly once per request — no more than 100 succeed and no fewer than 100 succeed under concurrent load.
result: [pending]

### 3. Sign-off on the five judgment-tier prohibitions
expected: |
  Human confirms or overrides each LLM-judge assessment in 03-VERIFICATION.md's
  "Judgment-Tier Prohibitions" table:
  1. No sliding expiration / refresh token defeating the 24h JWT bound
  2. No permissive CORS fallback when App:FrontendUrl is absent/empty
  3. No widened CORS allow-list beyond the four verbs/two headers
  4. No shared-partition collapse in the mutation rate limiter
  5. No distinguishable failed-login response between the two failure branches
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
