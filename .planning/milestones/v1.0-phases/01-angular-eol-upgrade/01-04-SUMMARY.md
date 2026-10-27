---
phase: 01-angular-eol-upgrade
plan: 04
subsystem: infra
tags: [angular, typescript, npm, ng-update, node, dependency-upgrade, frontend]

# Dependency graph
requires:
  - phase: 01-03
    provides: game-planner-ui running on Angular 20.3.30 via three sequential ng update hops (17->18, 18->19, 19->20)
provides:
  - game-planner-ui running on Angular 21.2.22 via one ng update hop (20->21), with the Karma toolchain fully intact despite v21's Vitest new-project default shift
  - Local Node.js runtime upgraded from v24.14.0 to v24.19.0, satisfying Angular 22's ^24.15.0 engine requirement ahead of the final hop
  - Confirmation that the Angular 21 build/test/boot set still passes unchanged after the Node runtime bump, via a fresh npm ci reinstall
affects: [01-05, 01-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sequential ng update per major, identical pattern to 01-01/01-02/01-03 (working tree carries pre-existing unrelated WIP that must never be touched/staged/committed)"
    - "NEW this plan: v21's built-in control-flow (@if/@for) codemod ran as a mandatory migration with no decline option (unlike every prior hop's optional migrations) -- handled by accepting the codemod's automatic run, then manually reverting the 19 affected template files back to *ngIf/*ngFor structural directives in the same commit, per D-03's structural-directive constraint"
    - "Node runtime upgrade is a non-repository, machine-local change verified via node -v/npm -v rather than a git diff; the only in-repo trace is the SUMMARY's before/after version record and (conditionally) a lockfile-refresh commit if npm ci changes package-lock.json"

key-files:
  created: []
  modified:
    - game-planner-ui/package.json
    - game-planner-ui/package-lock.json
    - game-planner-ui/src/main.ts
    - game-planner-ui/tsconfig.json
    - game-planner-ui/src/app/directives/click-outside.directive.ts

key-decisions:
  - "Task 1 (hop 8, 20->21): @angular/* -> ^21.2.22, CLI/build-angular -> 21.2.22, typescript -> ~5.9.3 (crossing into the >=5.9.0<6.0.0 range this hop admits for the first time), zone.js unchanged ~0.15.1, karma unchanged ~6.3.0, no vitest dependency added despite v21 making Vitest the new-project default. Mandatory bootstrap-options-to-providers codemod updated src/main.ts (bootstrap pattern preserved: platformBrowserDynamic().bootstrapModule(AppModule)). Mandatory tsconfig lib-property migration removed an explicit lib array. A TS2345 strictness error in click-outside.directive.ts (EventTarget no longer assignable to HTMLElement under TS 5.9) was fixed inline (Rule 1)."
  - "Task 1 deviation: v21's built-in control-flow (@if/@for) migration ran as a MANDATORY codemod with no interactive decline prompt, unlike every prior hop where this migration was optional and skippable by running non-interactively. Per D-03 (this project stays on structural directives, never converts to built-in control-flow), the codemod's changes were manually reverted across all 19 affected template files back to *ngIf/*ngFor, landed in the same hop commit so the net diff shows zero @if/@for markers and the acceptance grep for `@if|@for` in src/ returns 0."
  - "Task 2 (Node runtime upgrade): the developer upgraded the system Node install themselves (as Administrator) from v24.14.0 to v24.19.0 after the prior agent hit an elevation requirement it could not satisfy non-interactively. No version manager (nvm-windows) is installed on this machine -- consistent with 01-01-SUMMARY.md's decision to stay on the system install rather than adding a version manager preemptively. Orchestrator independently verified the new version (v24.19.0, satisfying ^24.15.0) in a fresh shell before this continuation began; this executor re-verified it again via a separate PowerShell invocation and via the plan's own NODE_ENGINE_OK assertion."
  - "npm ci in game-planner-ui/ completed successfully on the new runtime (984 packages reinstalled) with zero change to package.json or package-lock.json -- the Node bump did not alter any dependency resolution, so no separate lockfile-refresh commit was needed (per the plan's explicit 'only if npm ci legitimately changes package-lock.json' condition)."
  - "The npm ERESOLVE warning during npm ci (karma@6.3.20 installed vs. @angular/build@21.2.22's optional peer karma@^6.4.0) is a pre-existing peer-dependency mismatch from the v21 hop itself, not something introduced or worsened by the Node bump -- it is a warning, not a failure, and npm ci still exited 0."

requirements-completed: [DEP-01, DEP-02]

coverage:
  - id: D1
    description: "Angular 20 -> 21 hop (hop 8) executed via ng update: build/test/all-builders/strictness/boot-smoke all green, Karma toolchain intact with no vitest dependency, mandatory control-flow codemod manually reverted to preserve structural directives, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "commit cb5321d — ANGULAR_21_OK, npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK (karmaConfig still karma.conf.js), MAIN_OK, DECL_OK, ZONE_OK, STRICTNESS_OK, SMOKE_OK, @if/@for count=0 after manual revert, standalone/bootstrapApplication count=0, git log -1 = cb5321d"
        status: pass
    human_judgment: false
  - id: D2
    description: "Local Node.js runtime upgraded to satisfy Angular 22's ^22.22.3 || ^24.15.0 || ^26.0.0 engine range, persisting across fresh shells, with the Angular 21 build/test/boot set re-verified clean under npm ci on the new runtime and no engine-check bypass introduced"
    requirement: "DEP-02"
    verification:
      - kind: other
        ref: "node -v: v24.14.0 -> v24.19.0 (npm -v: unchanged 8.12.1); NODE_ENGINE_OK 24.19.0 printed; confirmed identical in a separate fresh PowerShell shell; npm ci exits 0 (984 packages, no package.json/package-lock.json diff); npm run build exit 0; npm test -- --watch=false --browsers=ChromeHeadless exit 0 (0 of 0); KARMA_BUILDER_OK printed; SMOKE_OK printed (curl found app-root on port 4200); grep for engine-strict/--ignore-engines/NG_DISABLE_VERSION_CHECK in package.json/.npmrc returns 0 hits; git status --porcelain package.json empty"
        status: pass
    human_judgment: false

duration: ~30min
completed: 2026-08-29
status: complete
---

# Phase 1 Plan 4: Angular 20 -> 21 + Node Runtime Upgrade Summary

**Advanced game-planner-ui to Angular 21.2.22 (hop 8 of 9), manually reverting v21's first-ever mandatory (non-optional) control-flow codemod to preserve structural directives, then closed the Node engine gap for Angular 22 by upgrading the system Node install from v24.14.0 to v24.19.0 and re-verifying the full Angular 21 build/test/boot chain on it.**

## Performance

- **Duration:** ~30 min (Task 1 executed in a prior session before the checkpoint; this continuation covered Task 2's verification plus SUMMARY/state work)
- **Started:** 2026-08-29 (Task 1, prior session)
- **Completed:** 2026-08-29T17:47Z
- **Tasks:** 2 completed (Task 1: hop 8 20->21; Task 2: Node runtime upgrade + re-verification)
- **Files modified:** 5 (package.json, package-lock.json, src/main.ts, tsconfig.json, click-outside.directive.ts) — all from Task 1; Task 2 produced no repository file changes

## Accomplishments

- **Task 1 (Hop 8, 20 -> 21):** `npx ng update @angular/core@21 @angular/cli@21 --allow-dirty`. `@angular/*` -> `^21.2.22`, CLI/build-angular -> `21.2.22`, typescript -> `~5.9.3` (zone.js `~0.15.1` and karma `~6.3.0` unchanged, already in range). No `vitest` dependency present despite v21 making it the CLI's new-project test-runner default — the classic karma builder, `karmaConfig: 'karma.conf.js'`, and `karma@6.x` all confirmed intact. Mandatory bootstrap-options-to-providers codemod updated `src/main.ts` while preserving `platformBrowserDynamic().bootstrapModule(AppModule)`. Mandatory tsconfig migration removed an explicit `lib` array. A TypeScript 5.9 strictness regression in `click-outside.directive.ts` (`EventTarget` no longer assignable to `HTMLElement`) was fixed inline (Rule 1). Verified: build exit 0, test exit 0 (0 of 0 specs), `ALL_BUILDERS_OK`, `MAIN_OK`, `DECL_OK`, `ZONE_OK`, `STRICTNESS_OK`, `SMOKE_OK`, zero standalone/bootstrapApplication/zoneless markers.
- **Task 1 deviation:** unlike every prior hop, v21's built-in control-flow (`@if`/`@for`) migration ran as a **mandatory** codemod with no interactive decline option, rewriting 19 template files. Per D-03 (structural directives only, no control-flow conversion), those 19 files were manually reverted back to `*ngIf`/`*ngFor` in the same hop commit — the final diff and the `@if|@for` grep both confirm zero occurrences.
- **Task 2 (Node runtime upgrade):** The prior session hit a blocking-human checkpoint because the installed Node `v24.14.0` was one minor short of Angular 22's `^24.15.0` requirement and the upgrade needed elevation the non-interactive agent couldn't obtain. **The developer upgraded Node themselves as Administrator.** This continuation independently re-verified: `node -v` → `v24.19.0`, `npm -v` → `8.12.1` (unchanged), confirmed identical in a fresh PowerShell shell (not just the bash tool's shell), and via the plan's own engine-range assertion script (`NODE_ENGINE_OK 24.19.0`). Then ran `npm ci` in `game-planner-ui/` (984 packages, exit 0, zero diff to `package.json`/`package-lock.json`), re-ran `npm run build` (exit 0) and `npm test -- --watch=false --browsers=ChromeHeadless` (exit 0, 0 of 0 specs), confirmed `KARMA_BUILDER_OK`, and ran a boot smoke check on port 4200 (`SMOKE_OK` — `app-root` found in served HTML). No engine-check bypass (`engine-strict`, `--ignore-engines`, `NG_DISABLE_VERSION_CHECK`) was added anywhere.

## Task Commits

1. **Task 1: Hop 8 — Angular 20 -> 21** - `cb5321d` (chore)
2. **Task 2: Node runtime upgrade** - no commit (npm ci produced zero lockfile/package.json diff, per the plan's explicit no-op condition; the runtime change itself is machine-local, not a repository artifact)

## Files Created/Modified

- `game-planner-ui/package.json` - `@angular/*` `20.3.30 -> 21.2.22`; `@angular/cli`/`@angular-devkit/build-angular` in lockstep; `typescript` `~5.8.3 -> ~5.9.3`; `zone.js`/`karma` unchanged
- `game-planner-ui/package-lock.json` - fully regenerated at the hop; unchanged again after Task 2's `npm ci`
- `game-planner-ui/src/main.ts` - mandatory bootstrap-options-to-providers codemod; bootstrap pattern (`platformBrowserDynamic().bootstrapModule(AppModule)`) preserved
- `game-planner-ui/tsconfig.json` - mandatory migration removed an explicit `lib` compiler option array; all four strictness flags preserved
- `game-planner-ui/src/app/directives/click-outside.directive.ts` - Rule 1 fix for a TS2345 error surfaced by the TypeScript 5.9 bump (`EventTarget` no longer assignable to `HTMLElement`)
- 19 template files (`.html`) - v21's mandatory control-flow codemod ran automatically, then was manually reverted back to `*ngIf`/`*ngFor` in the same commit (net diff: zero change to these files versus pre-hop state)

## Decisions Made

- Node strategy: no version manager (nvm-windows) installed on this machine, consistent with 01-01-SUMMARY.md's decision. The developer upgraded the system Node install directly (as Administrator) from v24.14.0 to v24.19.0, satisfying Angular 22's `^24.15.0` requirement within the installed Node 24 line — no jump to Node 26, no downgrade to 22.22.3.
- v21's control-flow codemod is mandatory (no decline flag), a first among the nine hops in this chain. Handled by accepting the automatic run and reverting its output in the same commit rather than fighting the CLI or skipping the hop.
- No separate lockfile-refresh commit for Task 2: `npm ci` on the new runtime produced byte-identical `package.json`/`package-lock.json`, so the plan's own "commit only if it legitimately changes" condition was not triggered.
- `--allow-dirty` used for the Task 1 `ng update` invocation (never `--force`), consistent with every prior plan in this phase — the working tree's pre-existing unrelated WIP (backend C# managers, game-detail component lightbox refactor) was left untouched throughout.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed TS2345 strictness error in click-outside.directive.ts**
- **Found during:** Task 1 (hop 8, Angular 20 -> 21)
- **Issue:** TypeScript 5.9's stricter DOM typings no longer allow `EventTarget` to be implicitly assignable to `HTMLElement`, breaking a build that compiled cleanly under TypeScript 5.8.
- **Fix:** Corrected the type narrowing/cast at the affected call site so the directive compiles under TS 5.9 with no loosening of `tsconfig.json` strictness flags.
- **Files modified:** `game-planner-ui/src/app/directives/click-outside.directive.ts`
- **Verification:** `npm run build` exits 0; `STRICTNESS_OK` assertion (`strict`, `strictInjectionParameters`, `strictInputAccessModifiers`, `strictTemplates` all still `true`) passes.
- **Committed in:** `cb5321d` (Task 1 commit)

**2. [Technique, not a Rule 1-4 deviation] Manual revert of v21's mandatory control-flow codemod**
- **Found during:** Task 1 (hop 8, Angular 20 -> 21)
- **Situation:** Unlike every prior hop where `use-application-builder`, `control-flow-migration`, and similar migrations were offered as optional/declinable prompts, v21's built-in control-flow (`@if`/`@for`) migration ran automatically against 19 template files as part of `ng update`'s mandatory codemod set, with no flag to suppress it.
- **Resolution:** Allowed the codemod to run, then manually reverted each of the 19 affected `.html` files back to `*ngIf`/`*ngFor` structural directives, landing the revert in the same hop commit so the net effect (per D-03) is zero structural-directive-to-control-flow conversion.
- **Files affected:** 19 template files across `game-planner-ui/src/app/components/`.
- **Verification:** `grep -rn --include=*.html -E '@if|@for' src/ | grep -v '<!--' | wc -l` returns `0`; `npm run build` and `npm test` both exit 0 post-revert.
- **Commit:** `cb5321d` (isolated within the single hop commit)

---

**Total deviations:** 1 auto-fixed bug (Rule 1), plus 1 non-Rule execution technique (mandatory-codemod revert).
**Impact on plan:** Both necessary to keep the hop compiling correctly and to honor the project's explicit structural-directive constraint (D-03). No scope creep.

## Issues Encountered

- **Node runtime elevation (Task 2, resolved via checkpoint):** the prior session's non-interactive agent could not obtain the elevation needed to upgrade the system Node install past `v24.14.0`, and correctly stopped rather than attempting a workaround (disabling the engine check, forcing an alternate package, etc.). The developer performed the upgrade manually as Administrator; this continuation re-verified the result independently (fresh-shell `node -v`, the plan's own `NODE_ENGINE_OK` assertion, and a full re-run of `npm ci` + build + test + boot smoke) before proceeding, per the checkpoint-resume protocol.
- **Pre-existing peer-dependency warning (Task 2, not a new issue):** `npm ci` emits an `ERESOLVE` warning because `@angular/build@21.2.22` optionally peers on `karma@^6.4.0` while this project pins `karma: ~6.3.0`. This warning already existed from the v21 hop itself (Task 1) and was not introduced or worsened by the Node bump; `npm ci` still exits 0 and the installed `karma@6.3.20` satisfies the project's own pinned range. No action taken — tracked here for visibility, not treated as a blocker.

## User Setup Required

None for this plan going forward. The one manual step this plan depended on — the Administrator-level Node upgrade — was already completed by the developer before this continuation began.

## Next Phase Readiness

- `game-planner-ui` now runs on Angular 21.2.22 with Karma/Jasmine, NgModule bootstrap, all three classic builders, TS strictness, and structural directives (`*ngIf`/`*ngFor`) all intact.
- Local Node runtime is now `v24.19.0`, comfortably inside Angular 22's `^24.15.0` engine range — the only remaining prerequisite for plan `01-05`'s final hop (20/21 -> 22) is the TypeScript 6.0 major jump itself.
- The `npm ci` reinstall on the new runtime proved the Node bump broke nothing: build, test, karma-builder, and boot-smoke checks are all green.
- No blockers.

---
*Phase: 01-angular-eol-upgrade*
*Completed: 2026-08-29*

## Self-Check: PASSED

- FOUND: `.planning/phases/01-angular-eol-upgrade/01-04-SUMMARY.md`
- FOUND: commit `cb5321d` (Angular 20 -> 21)
- FOUND: `game-planner-ui/package.json` reports `@angular/core` `^21.2.22`
- FOUND: Node runtime `v24.19.0` verified in a fresh shell, satisfying `^24.15.0`
