---
created: 2026-08-30T01:18:26.903Z
title: Session planner leaks cross-user game data
area: api
severity: major
files:
  - GamePlanner.API/Managers/SessionManager.cs:15
  - GamePlanner.API/Managers/SessionManager.cs:21
resolved: 2026-08-31
resolved_by: "Phase 3, plan 03-02"
---

## Resolution

Fixed in Phase 3 (auth-cors-rate-limiting-hardening), plan 03-02: `.Where(g => g.UserId ==
userId)` added to both `SessionManager.GetRecommendationAsync` (line 15) and, since the same
folded-todo reproduction traced the leak there too, `SessionPlannerManager.GetRecommendationsAsync`
(line 39). Live-verified in `03-VERIFICATION.md` (Truth 7) and re-confirmed in `03-SECURITY.md`
(T-03-07, closed) — a zero-game test account now gets a no-games response, not another account's
rows, from both session endpoints.

## Problem

Found during the Phase 1 (Angular EOL upgrade) manual UAT walkthrough — pre-existing, NOT
introduced by that phase (SessionManager.cs was untouched by any of the 6 Angular-upgrade plans).

`POST /api/session/plan` builds its game query in `SessionManager.cs` without a `.Where(g =>
g.UserId == userId)` filter — it only filters on `g.IsActive`, `g.EstimatedMinutes`, and
`g.Tags.Any(...)`. Verified live: authenticated as a freshly-registered throwaway test account
with zero games in its own library, calling the endpoint returned 5 real suggestions carrying
other accounts' data — including `playtimeHours` (e.g. 98.3h, 14.8h) and `source: "Steam"` —
none of which belonged to the test account.

This is a data-isolation / privacy gap: any authenticated user can see fragments of other users'
library composition and Steam playtime through the session planner's recommendations, without
any special access. Relevant to this project's security-hardening milestone (`.claude/CLAUDE.md`
— "Close the concrete security and reliability gaps already identified in the codebase").

## Solution

Add `.Where(g => g.UserId == userId)` to the query in `SessionManager.cs` (the constructor takes
`GamePlannerDbContext db`, so `userId` needs to be threaded in from `SessionController` the same
way other managers scope their queries — see `GameManager`/`GameImagesController` for the
established per-user filtering pattern in this codebase). Add a regression check confirming a
fresh account's `/api/session/plan` never returns another `UserId`'s rows.
