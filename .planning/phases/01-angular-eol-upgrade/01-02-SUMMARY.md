---
phase: 01-angular-eol-upgrade
plan: 02
subsystem: infra
tags: [angular, typescript, npm, ng-update, dependency-upgrade, frontend]

# Dependency graph
requires: ["01-01"]
provides:
  - game-planner-ui running on Angular 17.3.12 via three sequential `ng update` hops (14->15, 15->16, 16->17), one commit per hop (D-05)
  - Confirmation that the classic browser/dev-server/karma builder trio survives the v17 esbuild/application-builder migration prompt (D-03)
  - Confirmation that the Karma toolchain survives the v16 deprecation announcement unmodified (Pitfall 1)
affects: [01-03, 01-04, 01-05, 01-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sequential ng update per major with --allow-dirty, identical pattern to 01-01 (working tree carries pre-existing unrelated WIP that must never be touched/staged/committed)"
    - "JSONC-tolerant reads for tsconfig.json when scripting verification (carried forward from 01-01; the leading block comment persists through all three hops)"
    - "Mandatory-migration side-effect files (outside the plan's declared <files> list) are still staged and committed as part of the hop commit when the migration is genuinely mandatory (not an optional prompt) — e.g. .browserslistrc deletion, src/test.ts, auth.guard.ts"

key-files:
  created: []
  modified:
    - game-planner-ui/package.json
    - game-planner-ui/package-lock.json
    - game-planner-ui/angular.json
    - game-planner-ui/tsconfig.json
    - game-planner-ui/src/test.ts
    - game-planner-ui/src/app/guards/auth.guard.ts
    - game-planner-ui/.browserslistrc (deleted by mandatory v15 CLI migration)

key-decisions:
  - "Node strategy unchanged from 01-01: stayed on Node v24.14.0 through all three hops in this plan (14->15->16->17). The CLI's own 'unsupported Node' warning persisted (informational only) but never blocked build/test/serve at any hop. Re-verify per D-02 at each remaining hop, especially the final v22 hop where RESEARCH.md's Pitfall 3 flags a genuine engine-range miss."
  - "--allow-dirty used on all three ng update invocations (not --force) for the same reason as 01-01: the working tree carries pre-existing, out-of-scope uncommitted changes (backend C# managers, game-detail component files, untracked SQL/openspec/.claude directories) that this plan must never touch, stage, or commit. Verified via git status --short before and after each hop that only the intended game-planner-ui files changed."
  - "--force was NOT needed for any of the three hops in this plan; peer dependency resolution succeeded cleanly at 15, 16, and 17."
  - "No optional migration prompts were accepted at any hop (standalone components at v15, application-builder/control-flow at v17) — non-interactive ng update only applies mandatory codemods by default, confirmed by output logs showing zero interactive prompts encountered."

requirements-completed: [DEP-01, DEP-02]

coverage:
  - id: D1
    description: "Angular 14 -> 15 hop executed via ng update, build/test/karma-builder/strictness/boot-smoke all green, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_15_OK (ts=~4.9.5 zone=~0.11.4), npm run build exit 0, npm test exit 0 (0 of 0), KARMA_BUILDER_OK, STRICTNESS_OK, SMOKE_OK, git log -1 = 101f1ad"
        status: pass
    human_judgment: false
  - id: D2
    description: "Angular 15 -> 16 hop executed via ng update, build/test/karma-builder/strictness/boot-smoke all green, karma toolchain confirmed surviving v16 deprecation announcement, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_16_OK (ts=~4.9.5 zone=~0.13.3), npm run build exit 0, npm test exit 0 (0 of 0), KARMA_BUILDER_OK, karma=~6.3.0 still present, STRICTNESS_OK, SMOKE_OK, git log -1 = 414b89a"
        status: pass
    human_judgment: false
  - id: D3
    description: "Angular 16 -> 17 hop executed via ng update, build/test/all-three-builders/strictness/boot-smoke all green, application-builder migration declined, templates still use structural directives, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_17_OK (ts=~5.4.5 zone=~0.14.10), npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK (browser/dev-server/karma), STRICTNESS_OK, SMOKE_OK, @if/@for count=0, ngIf usage count=228 (non-vacuous), git log -1 = 5906170"
        status: pass
    human_judgment: false
  - id: D4
    description: "Related deps (TypeScript, zone.js, RxJS) land inside RESEARCH.md's hop matrix peer ranges at each of the three hops, without hand-pinning"
    requirement: "DEP-02"
    verification:
      - kind: other
        ref: "Hop 2: ts ~4.9.5 (in >=4.8.2<5.0.0), zone.js ~0.11.4 (in ~0.11.4||~0.12.0), rxjs ~7.5.0 unchanged. Hop 3: ts ~4.9.5 (in >=4.9.3<5.2.0), zone.js ~0.13.3 (in ~0.13.0), rxjs unchanged. Hop 4: ts ~5.4.5 (in >=5.2.0<5.5.0), zone.js ~0.14.10 (in ~0.14.0), rxjs unchanged. All resolved by ng update itself, none hand-pinned."
        status: pass
    human_judgment: false
  - id: D5
    description: "Exactly three new commits exist, one per hop, in ascending version order; backend/Database untouched by any of them"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "git log --oneline -3 --format=%s lists '16->17', '15->16', '14->15' in that order (newest first); git show --stat HEAD/HEAD~1/HEAD~2 -- GamePlanner.API Database all return 0 lines"
        status: pass
    human_judgment: false

duration: ~35min
completed: 2026-08-29
status: complete
---

# Phase 1 Plan 2: Angular 14 -> 17 (Three Sequential Hops) Summary

**Advanced game-planner-ui through three separate `ng update` hops (14->15, 15->16, 16->17) with the classic NgModule/webpack-builder/Karma architecture fully preserved at each step, one bisectable commit per hop.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-08-29 (session start, immediately after 01-01)
- **Completed:** 2026-08-29T15:11:28Z
- **Tasks:** 3 completed (Task 1: hop 14->15, Task 2: hop 15->16, Task 3: hop 16->17)
- **Files modified:** package.json, package-lock.json, angular.json, tsconfig.json, src/test.ts, src/app/guards/auth.guard.ts (across all three hops); .browserslistrc deleted

## Accomplishments
- **Hop 2 (14 -> 15):** `npx ng update @angular/core@15 @angular/cli@15 --allow-dirty`. `@angular/*` -> `^15.2.10`, CLI/build-angular -> `15.2.11`, TypeScript -> `~4.9.5` (zone.js/rxjs unchanged, already in range). Mandatory migrations: deleted `.browserslistrc` (matched CLI default), stripped stale `require` calls from `src/test.ts`, bumped `tsconfig.json` compiler `target`/`useDefineForClassFields`. No optional standalone-component prompt was accepted (non-interactive run only applies mandatory codemods). Verified: build exit 0, test exit 0 (0 of 0 specs), `KARMA_BUILDER_OK`, `STRICTNESS_OK`, boot smoke `SMOKE_OK`, zero standalone/control-flow/zoneless markers.
- **Hop 3 (15 -> 16):** `npx ng update @angular/core@16 @angular/cli@16 --allow-dirty`. `@angular/*` -> `^16.2.12`, CLI/build-angular -> `16.2.16`, zone.js -> `~0.13.3` (TypeScript/rxjs unchanged, already in range). Mandatory migration removed the deprecated `CanActivate`/`Resolve` interface `implements` clause from `src/app/guards/auth.guard.ts`. Per RESEARCH.md's Pitfall 1, explicitly confirmed the Karma toolchain (`karma: ~6.3.0`) is still present in `package.json` and the `test` builder is unchanged — the v16 Karma deprecation announcement did not remove or degrade it. Verified: build exit 0, test exit 0, `KARMA_BUILDER_OK`, `STRICTNESS_OK`, `SMOKE_OK`.
- **Hop 4 (16 -> 17):** `npx ng update @angular/core@17 @angular/cli@17 --allow-dirty`. `@angular/*` -> `^17.3.12`, CLI/build-angular -> `17.3.17`, TypeScript -> `~5.4.5` (first TS 5.x on the chain), zone.js -> `~0.14.10`. Mandatory `angular.json` migration renamed deprecated build-target option keys (Pitfall 2 materialized: `serve` target's `browserTarget` reference became `buildTarget`) — all three architect builders (`build`/`serve`/`test`) confirmed unchanged (`@angular-devkit/build-angular:browser` / `:dev-server` / `:karma`). No optional prompts accepted: the esbuild/Vite `application`-builder migration and the `@if`/`@for` control-flow conversion were both declined (non-interactive `ng update` never offers them without an explicit named schematic flag). Verified: build exit 0, test exit 0, `ALL_BUILDERS_OK`, `STRICTNESS_OK`, `SMOKE_OK`, zero `@if`/`@for` occurrences, 228 live `*ngIf` occurrences confirming the structural-directive check is non-vacuous, zero standalone/`bootstrapApplication` markers.
- Confirmed after all three hops: `src/main.ts` still bootstraps via `platformBrowserDynamic().bootstrapModule(AppModule)`, `app.module.ts` still has a `declarations:` array, `polyfills.ts` still imports `zone.js`, and every `tsconfig.json` strictness flag (`strict`, `strictInjectionParameters`, `strictInputAccessModifiers`, `strictTemplates`) remained `true` throughout.
- Node strategy: stayed on Node v24.14.0 across all three hops (no version switch needed); the Angular CLI's "unsupported Node" warning is cosmetic at these majors and never blocked any command.

## Task Commits

1. **Task 1: Hop 2 — Angular 14 -> 15** - `101f1ad` (chore)
2. **Task 2: Hop 3 — Angular 15 -> 16** - `414b89a` (chore)
3. **Task 3: Hop 4 — Angular 16 -> 17** - `5906170` (chore)

## Files Created/Modified
- `game-planner-ui/package.json` - `@angular/*` progressed `14.3.0 -> 15.2.10 -> 16.2.12 -> 17.3.12`; `@angular/cli`/`@angular-devkit/build-angular` progressed in lockstep; `typescript` progressed `~4.6.4 -> ~4.9.5 -> ~5.4.5`; `zone.js` progressed `~0.11.4 -> ~0.13.3 -> ~0.14.10`; `rxjs` unchanged at `~7.5.0` throughout (no hop in this plan requires a bump)
- `game-planner-ui/package-lock.json` - fully regenerated at each of the three hops
- `game-planner-ui/angular.json` - untouched at hops 2 and 3; at hop 4, mandatory migration renamed deprecated build-target option keys on the `serve` target while all three builder strings (`browser`/`dev-server`/`karma`) stayed unchanged
- `game-planner-ui/tsconfig.json` - `compilerOptions.target` bumped `es2020 -> ES2022`, `useDefineForClassFields` set `false` explicitly by hop 2's mandatory migration; all strictness flags preserved through all three hops
- `game-planner-ui/src/test.ts` - stale `require` calls removed by hop 2's mandatory Karma-builder-main-file migration
- `game-planner-ui/src/app/guards/auth.guard.ts` - deprecated `CanActivate`/`Resolve` interface `implements` clause removed by hop 3's mandatory migration (logic unchanged, purely a type-surface cleanup)
- `game-planner-ui/.browserslistrc` - deleted by hop 2's mandatory migration (file matched the CLI's default configuration, so the CLI now applies its built-in default implicitly)

## Decisions Made
- Node strategy: stayed on Node v24.14.0 through all three hops in this plan, consistent with 01-01's decision — no Angular CLI in this range refused to run on it. Re-verify at each remaining hop per D-02, especially the v22 final hop flagged by RESEARCH.md Pitfall 3.
- `--allow-dirty` used on every `ng update` invocation in this plan (never `--force`) for the same reason documented in 01-01: pre-existing, out-of-scope uncommitted changes in the working tree (backend managers, `game-detail` component, untracked SQL/openspec/.claude directories) must never be touched. Verified via `git status --short` before and after each of the three hops that only the intended `game-planner-ui/` files changed and the pre-existing unrelated diffs stayed byte-identical.
- `--force` was not needed at hop 2, 3, or 4 — peer dependency resolution succeeded cleanly all three times.
- No optional migrations were accepted at any hop. Running `ng update` non-interactively already restricts execution to mandatory codemods only, so no extra decline flag was needed — confirmed by inspecting each hop's full CLI output for the absence of any standalone/control-flow/application-builder/zoneless schematic execution.

## Deviations from Plan

None - plan executed exactly as written. Mandatory migration side effects (`.browserslistrc` deletion, `src/test.ts`, `auth.guard.ts` changes, `angular.json` build-target key rename) fell outside the plan's declared `<files>` list per task but are expected, in-scope consequences of running `ng update`'s bundled mandatory codemods (per RESEARCH.md's "Don't Hand-Roll" guidance) rather than deviations requiring a Rule 1-4 classification — none of them touched application behavior, none loosened type-safety, and all were verified not to affect the pre-existing unrelated WIP files.

## Issues Encountered

None. All three hops completed cleanly on the first attempt with no `ERESOLVE` conflicts, no schema-validation failures (Pitfall 2's failure mode did not manifest — the mandatory migration handled the option rename correctly), and no ChromeHeadless launch issues (Chrome was already confirmed present in 01-01).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- `game-planner-ui` now runs on Angular 17.3.12 with Karma/Jasmine, NgModule bootstrap, all three classic builders, and TS strictness all intact — ready for plan `01-03` to continue the hop sequence (17 -> 18 -> ... toward the phase target of v22).
- The `--allow-dirty` pattern, Node v24.14.0 strategy, and JSONC-tolerant `tsconfig.json` parsing note from 01-01 all continue to apply unchanged; no further action needed beyond awareness.
- Flag carried forward from 01-01: re-confirm Angular 22 is still current stable at the final hop if a live web-fetch tool becomes available, or via manual developer confirmation — RESEARCH.md's dated finding remains within its validity window but has not been independently re-verified this session either.
- No blockers. Node v24.14.0 remains sufficient through v17; re-verify per-hop per D-02's Node.js column in RESEARCH.md's matrix, with particular attention to Pitfall 3 (Node engine-range miss) at the v22 hop.

---
*Phase: 01-angular-eol-upgrade*
*Completed: 2026-08-29*

## Self-Check: PASSED

- FOUND: `.planning/phases/01-angular-eol-upgrade/01-02-SUMMARY.md`
- FOUND: commit `101f1ad` (Angular 14 -> 15)
- FOUND: commit `414b89a` (Angular 15 -> 16)
- FOUND: commit `5906170` (Angular 16 -> 17)
