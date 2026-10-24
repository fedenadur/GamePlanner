---
created: 2026-10-24T00:00:00.000Z
title: UI copy and type-scale cleanup (deferred from Phase 5 UI-SPEC)
area: frontend
severity: minor
files:
  - game-planner-ui/src/app/components/login/login.component.ts
  - game-planner-ui/src/app/components/register/register.component.ts
  - game-planner-ui/src/app/components/game-list/game-list.component.html
  - game-planner-ui/src/styles.css
---

## Problem

The Phase 5 UI checker blocked on three pre-existing UI issues. The user force-approved the
UI-SPEC because Phase 5 is hardening-only under a visual/copy change lock:

1. `Could not connect to server.` (login.component.ts:52, register.component.ts:39) gives no
   next step. Suggested: `Could not connect to server. Check your connection and try again.`
2. `No games match your filters.` also shows for a fresh account with zero games. Needs a
   distinct zero-games empty state with a next step (e.g. `No games yet. Add your first game to get started.`).
3. The stylesheet uses 8 font sizes and 4 weights; consolidate to a smaller type scale.

## Solution

Address in a future UX/feature phase after the hardening milestone. Update any Phase 5
Karma/Playwright assertions on these strings in the same change.
