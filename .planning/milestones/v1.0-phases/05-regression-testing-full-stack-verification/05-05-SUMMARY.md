---
phase: 05-regression-testing-full-stack-verification
plan: 05
subsystem: testing
tags: [karma, jasmine, angular-22, npm-audit, nuget-audit, dependency-remediation]
status: complete
requirements: [TEST-01, TEST-02]

requires:
  - phase: 05-regression-testing-full-stack-verification
    provides: "05-01 NuGet fix of the test project (xunit 2.9.3) and the moderate IdentityModel handoff"
provides:
  - "npm run test:ci: one-command headless Karma run (8 specs)"
  - "Specs locking AuthInterceptor, AuthGuard and the game-list delete-failure fixed message"
  - "Angular 22.2.3 runtime/compiler packages and a lockfile regenerated with npm 11.21.1"
  - "05-DEPENDENCY-AUDIT.md: D-07 evidence record for npm and NuGet"
affects: [05-06]

tech-stack:
  added: []
  patterns: ["Class interceptor tested via HTTP_INTERCEPTORS + provideHttpClient(withInterceptorsFromDi()) + HttpTestingController", "Guard and component with a single/simple constructor built directly with jasmine spy objects, no TestBed"]

key-files:
  created:
    - game-planner-ui/src/app/interceptors/auth.interceptor.spec.ts
    - game-planner-ui/src/app/guards/auth.guard.spec.ts
    - game-planner-ui/src/app/components/game-list/game-list.component.spec.ts
    - .planning/phases/05-regression-testing-full-stack-verification/05-DEPENDENCY-AUDIT.md
  modified:
    - game-planner-ui/package.json
    - game-planner-ui/package-lock.json

key-decisions:
  - "Residual vite 8.3.0 high accepted: @angular/build@22.2.4 pins it exactly, and angular.json uses only @angular-devkit/build-angular:* builders (A6 proven)"
  - "JwtBearer 8.0.0 -> 8.0.32 left unapplied per D-07 (moderate is report-only)"

metrics:
  tasks: 3
  commits: [0e981ae, 8f78da9, 55e3694]
  completed: 2026-10-24
---

# Phase 5 Plan 5: Frontend specs and dependency audit Summary

Eight headless Karma specs lock the client side of the auth contract (bearer attach, 401 logout, guard redirect) and the fixed delete-failure message, and an in-range bump of nine `@angular/*` packages to 22.2.3 with an npm 11 lockfile took `npm audit` from 29 findings (5 critical, 14 high) to 3 (0 critical, only `vite` high, accepted with evidence).

## Tasks

| Task | Name | Commit |
|------|------|--------|
| 1 (tracer) | `test:ci` script and AuthInterceptor specs (4) | 0e981ae |
| 2 | AuthGuard (2) and game-list delete-failure (2) specs | 8f78da9 |
| 3 | npm in-range bump, NuGet re-scan, audit record | 55e3694 |

## Tracer evidence

Task 1 `npm run test:ci` ran in ChromeHeadless 155: `Executed 4 of 4 SUCCESS`, exit 0, before any expansion task. Tracer verified end to end in auto mode.

## Verification

- `npm run test:ci`: 8 of 8 SUCCESS, both before and after the dependency bump.
- `npm run build`: exit 0 (existing initial-bundle budget warning only, 664.55 kB vs 500 kB, unchanged by this plan).
- Boot smoke: `ng serve --port 4300` returned HTML containing `<app-root` and logged `Compiled successfully`; the process was stopped afterwards.
- `npm audit` (npm 11.21.1): before 1 low / 9 moderate / 14 high / 5 critical; after 2 low / 0 moderate / 1 high (`vite`) / 0 critical.
- `dotnet list GamePlanner.sln package --vulnerable --include-transitive`: no High or Critical in either project; two Moderate (IdentityModel 7.0.3) reported, not fixed.
- `npm ls`: `@angular/core` and `@angular/router` 22.2.3, `typescript` 6.0.3, `jasmine-core` 6.3.0, `zone.js` 0.15.1; no `overrides` block in `package.json`.
- Production frontend sources (`auth.interceptor.ts`, `auth.guard.ts`, `game-list.component.ts`) and all `.html`/`.css` untouched; the game-detail WIP stayed unstaged.

## Deviations from Plan

**1. [Rule 3 - Blocking] ERESOLVE on first `npm install`, lockfile and node_modules regenerated**
- **Found during:** Task 3 step 3
- **Issue:** old exact optional-peer pins in the lockfile conflicted with `@angular/animations@^22.2.3` (the plan anticipated this).
- **Fix:** deleted `package-lock.json` and `node_modules`, re-ran `npx -y npm@11.21.1 install` (no force, no legacy-peer-deps, no overrides).
- **Commit:** 55e3694

**2. [Environment] Stopped the developer's `ng serve` on port 4200**
- To delete `node_modules` the node process (PID 13944, `ng serve` from `game-planner-ui`) was stopped. It is NOT running now; 05-06 restarts the dev stack. The dev API was already stopped.

Otherwise the plan was executed as written. Task 2 was TDD-flagged but adds characterization specs over unchanged production code, so there is no RED commit (the specs pass against existing behavior by design).

## Notes

- npm 11.21.1 printed `install-scripts` warnings for `esbuild`, `@parcel/watcher`, `lmdb`, `msgpackr-extract` (install scripts not run by default). Build, tests and `ng serve` all worked, so no action was taken.
- `karma-jasmine@5.1.0` still brings a nested `jasmine-core@4.6.1`; unchanged from before, the top-level harness uses 6.3.0.

## Known Stubs

None.

## Threat Flags

None. T-05-23 and T-05-24 mitigated (0 critical, `@angular/router` 22.2.3); T-05-25 accepted with evidence in the audit record.

## Self-Check: PASSED

- FOUND: the three spec files, `05-DEPENDENCY-AUDIT.md`
- FOUND commits: 0e981ae, 8f78da9, 55e3694
