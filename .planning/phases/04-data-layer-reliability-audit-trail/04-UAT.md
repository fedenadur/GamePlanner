---
status: testing
phase: 04-data-layer-reliability-audit-trail
source: [04-VERIFICATION.md]
started: 2026-08-31T20:39:46.495Z
updated: 2026-08-31T20:39:46.495Z
---

## Current Test

number: 1
name: D-14 live sweep check: /workers dashboard + log line (04-02 human-check)
expected: |
  SteamTokenCleanupWorker shows a recent LastSuccessAt on /workers, and the API log carries
  exactly one count-only removal line (e.g. 'SteamTokenCleanupWorker: removed 3 expired
  token(s).') after 3 abandoned Steam logins age past their 5-minute expiry and a sweep runs
awaiting: user response

## Tests

### 1. D-14 live sweep check: /workers dashboard + log line (04-02 human-check)
expected: SteamTokenCleanupWorker shows a recent LastSuccessAt on /workers, and the API log carries exactly one count-only removal line (e.g. 'SteamTokenCleanupWorker: removed 3 expired token(s).') after 3 abandoned Steam logins age past their 5-minute expiry and a sweep runs
result: [pending]

### 2. 04-03 Human-check A: delete-failure UI message with the API stopped
expected: Clicking delete on a game while the API is down renders the fixed sentence "Could not delete game. Please try again." with no status code, exception text, or response body visible anywhere on the page
result: [pending]

### 3. 04-03 Human-check B: IGDBId cross-user re-verification (folded todo, still pending)
expected: Two games newly added through the live "Add Game" IGDB search flow still read IsActive = 1 after GameSyncWorker and GameImageSyncWorker both report Completed
result: [pending]

## Summary

total: 3
passed: 0
issues: 0
pending: 3
skipped: 0
blocked: 0

## Gaps
