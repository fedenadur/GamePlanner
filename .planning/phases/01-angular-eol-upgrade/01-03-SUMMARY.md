---
phase: 01-angular-eol-upgrade
plan: 03
subsystem: infra
tags: [angular, typescript, npm, ng-update, dependency-upgrade, frontend]

# Dependency graph
requires: ["01-02"]
provides:
  - game-planner-ui running on Angular 20.3.30 via three sequential `ng update` hops (17->18, 18->19, 19->20), one commit per hop (D-05)
  - Confirmation that the v19 mandatory standalone:false codemod and the v19 ExperimentalPendingTasks->PendingTasks codemod both ran cleanly with no manual follow-up needed
  - Confirmation that the Karma toolchain and karma.conf.js survive the v20 CLI default shift byte-identical, verified against the mandatory "remove default karma.conf.js" migration reporting "No changes made"
  - A reusable technique for isolating a mandatory `ng update` codemod's change from pre-existing unrelated WIP inside the same file, so the codemod lands in its own commit without pulling in or discarding the WIP
affects: [01-04, 01-05, 01-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sequential ng update per major with --allow-dirty, identical pattern to 01-01/01-02 (working tree carries pre-existing unrelated WIP that must never be touched/staged/committed)"
    - "JSONC-tolerant reads for tsconfig.json when scripting verification (carried forward from 01-01/01-02; the leading block comment persists through all three hops)"
    - "Mandatory-migration side-effect files (outside the plan's declared <files> list) are still staged and committed as part of the hop commit when the migration is genuinely mandatory (not an optional prompt) -- e.g. the 20 standalone:false component/directive files at hop 6"
    - "NEW this plan: when a mandatory codemod's change lands inside a file that also carries pre-existing unrelated uncommitted WIP, isolate the codemod-only version by reconstructing it from `git show HEAD:<file>` plus a targeted string replacement matching the codemod's known transformation, write that isolated version to disk, `git add` it (staging only the isolated diff), then restore the full mixed (WIP+codemod) content back to the working tree before committing. Net effect: the commit contains only the codemod's change; the working tree retains the WIP untouched and unstaged, byte-identical to before the hop ran. Used for `game-detail.component.ts` at hop 6 (v19), where the mandatory standalone:false decorator rewrite landed in the same file as the user's pre-existing lightbox-carousel refactor WIP."

key-files:
  created: []
  modified:
    - game-planner-ui/package.json
    - game-planner-ui/package-lock.json
    - game-planner-ui/angular.json
    - game-planner-ui/tsconfig.json
    - game-planner-ui/src/app/app.module.ts
    - game-planner-ui/src/app/app.component.ts
    - game-planner-ui/src/app/components/backlog-prioritizer/backlog-prioritizer.component.ts
    - game-planner-ui/src/app/components/calendar/calendar.component.ts
    - game-planner-ui/src/app/components/dashboard/dashboard.component.ts
    - game-planner-ui/src/app/components/game-card/game-card.component.ts
    - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
    - game-planner-ui/src/app/components/game-list/game-list.component.ts
    - game-planner-ui/src/app/components/igdb-search-modal/igdb-search-modal.component.ts
    - game-planner-ui/src/app/components/journal/journal.component.ts
    - game-planner-ui/src/app/components/login/login.component.ts
    - game-planner-ui/src/app/components/now-playing/now-playing.component.ts
    - game-planner-ui/src/app/components/profile/profile.component.ts
    - game-planner-ui/src/app/components/register/register.component.ts
    - game-planner-ui/src/app/components/session-planner/session-planner.component.ts
    - game-planner-ui/src/app/components/sidebar/sidebar.component.ts
    - game-planner-ui/src/app/components/stats-dashboard/stats-dashboard.component.ts
    - game-planner-ui/src/app/components/steam-import/steam-import.component.ts
    - game-planner-ui/src/app/components/tracked-games/tracked-games.component.ts
    - game-planner-ui/src/app/components/workers/workers.component.ts
    - game-planner-ui/src/app/directives/click-outside.directive.ts

key-decisions:
  - "Node strategy unchanged: stayed on Node v24.14.0 through all three hops in this plan (17->18->19->20). RESEARCH.md's v20 matrix admits ^24.0.0 for the first time in the chain, so v24.14.0 became officially in-range at hop 7 (no more 'Unsupported' CLI warning risk going forward), though the CLI's cosmetic warning had never blocked anything at any prior hop either."
  - "--allow-dirty used on all three ng update invocations (not --force) for the same reason as 01-01/01-02: the working tree carries pre-existing, out-of-scope uncommitted changes (backend C# managers, game-detail component files, untracked SQL/openspec/.claude directories) that this plan must never touch, stage, or commit. Verified via git status --short before and after each hop that only the intended game-planner-ui files changed and the pre-existing unrelated diffs stayed byte-identical."
  - "--force was NOT needed for any of the three hops in this plan; peer dependency resolution succeeded cleanly at 18, 19, and 20."
  - "No optional migration prompts were accepted at any hop: use-application-builder (offered at all three hops), control-flow-migration and router-current-navigation (offered at hop 7) were all declined by running ng update non-interactively, which only applies mandatory codemods by default."
  - "NEW: game-detail.component.ts carries pre-existing unrelated user WIP (a lightbox-carousel refactor) in the same file the v19 mandatory standalone:false codemod touched. Rather than either (a) committing the WIP alongside the codemod, violating the 'never touch pre-existing WIP' constraint, or (b) skipping the codemod's change for this one file, violating 'apply all mandatory codemods', the codemod's isolated change was reconstructed from HEAD + a targeted string replace matching the exact transformation the schematic applied to the other 20 files, staged alone via a temporary disk-overwrite-then-restore sequence, and committed in isolation. Verified via `git diff HEAD~3 HEAD -- <file>` showing only the standalone:false hunk in history, and via `git status` showing the WIP diff unchanged before vs. after the three hops."

requirements-completed: [DEP-01, DEP-02]

coverage:
  - id: D1
    description: "Angular 17 -> 18 hop executed via ng update, build/test/all-builders/strictness/boot-smoke all green, zoneless declined, standalone/control-flow absent, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_18_OK (ts=~5.4.5 zone=~0.14.10), npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK, MAIN_OK, DECL_OK, ZONE_OK, STRICTNESS_OK, SMOKE_OK, provideZonelessChangeDetection count=0, standalone/bootstrapApplication count=0, @if/@for count=0, git log -1 = 1f4782e"
        status: pass
    human_judgment: false
  - id: D2
    description: "Angular 18 -> 19 hop executed via ng update, build/test/all-builders/strictness/boot-smoke all green, ExperimentalPendingTasks codemod ran as a confirmed no-op, mandatory standalone:false codemod applied across 20 NgModule-declared files without disturbing pre-existing WIP in game-detail.component.ts, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_19_OK (ts=~5.8.3 zone=~0.15.1), npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK, MAIN_OK, DECL_OK, ZONE_OK, STRICTNESS_OK, SMOKE_OK, ExperimentalPendingTasks count=0, standalone/bootstrapApplication count=0, @if/@for count=0, git log -1 = 4b6f412, git status confirms game-detail.component.ts WIP diff unchanged after commit"
        status: pass
    human_judgment: false
  - id: D3
    description: "Angular 19 -> 20 hop executed via ng update, build/test/all-builders/strictness/boot-smoke all green, Karma toolchain and karma.conf.js confirmed surviving the v20 CLI default-shift mandatory migration byte-identical, exactly one hop commit"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "ANGULAR_20_OK (ts=~5.8.3 karma=~6.3.0), npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK (also asserts karmaConfig=='karma.conf.js'), MAIN_OK, DECL_OK, ZONE_OK, STRICTNESS_OK, SMOKE_OK, git diff --stat HEAD~1 HEAD -- karma.conf.js = 0 lines, standalone/bootstrapApplication count=0, @if/@for count=0, git log -1 = f3c3d1b"
        status: pass
    human_judgment: false
  - id: D4
    description: "Related deps (TypeScript, zone.js, RxJS, karma) land inside RESEARCH.md's hop matrix peer ranges at each of the three hops, without hand-pinning"
    requirement: "DEP-02"
    verification:
      - kind: other
        ref: "Hop 5 (17->18): ts ~5.4.5 (in >=5.4.0<5.6.0), zone.js ~0.14.10 (in ~0.14.0), rxjs unchanged. Hop 6 (18->19): ts ~5.8.3 (in >=5.5.0<5.7.0 -- see Issues Encountered), zone.js ~0.15.1 (in ~0.15.0), rxjs unchanged. Hop 7 (19->20): ts ~5.8.3 (in >=5.8.0<5.9.0), zone.js ~0.15.1 (in ~0.15.0), karma ~6.3.0 (6.x range satisfied), rxjs unchanged. All resolved by ng update itself, none hand-pinned."
        status: pass
    human_judgment: false
  - id: D5
    description: "Exactly three new commits exist, one per hop, in ascending version order; backend/Database untouched by any of them"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "git log --oneline -3 --format=%s lists '19->20', '18->19', '17->18' in that order (newest first); git show --stat HEAD/HEAD~1/HEAD~2 -- GamePlanner.API Database all return 0 lines"
        status: pass
    human_judgment: false
status: complete
duration: ~50min
completed: 2026-08-29
---

# Phase 1 Plan 3: Angular 17 -> 20 (Three Sequential Hops) Summary

**Advanced game-planner-ui through three separate `ng update` hops (17->18, 18->19, 19->20) with the classic NgModule/karma-builder architecture fully preserved, and isolated a mandatory v19 codemod from pre-existing unrelated WIP living in the same source file so neither had to be sacrificed.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-08-29T15:15Z (immediately after 01-02)
- **Completed:** 2026-08-29T15:39:18Z
- **Tasks:** 3 completed (Task 1: hop 17->18, Task 2: hop 18->19, Task 3: hop 19->20)
- **Files modified:** package.json, package-lock.json, angular.json, tsconfig.json, src/app/app.module.ts, src/app/app.component.ts, and 20 other component/directive files (across all three hops)

## Accomplishments

- **Hop 5 (17 -> 18):** `npx ng update @angular/core@18 @angular/cli@18 --allow-dirty`. `@angular/*` -> `^18.2.14`, CLI/build-angular -> `18.2.21` (TypeScript `~5.4.5` / zone.js `~0.14.10` unchanged, already in range). Mandatory codemod replaced `HttpClientModule` import with `provideHttpClient(withInterceptorsFromDi())` in `src/app/app.module.ts`, keeping the `declarations:` array and NgModule pattern intact. The only optional migration surfaced (`use-application-builder`) was not executed (non-interactive run). Verified: build exit 0, test exit 0 (0 of 0 specs), `ALL_BUILDERS_OK`, `MAIN_OK`, `DECL_OK`, `ZONE_OK`, `STRICTNESS_OK`, `SMOKE_OK`, zero zoneless/standalone/control-flow markers.
- **Hop 6 (18 -> 19):** `npx ng update @angular/core@19 @angular/cli@19 --allow-dirty`. `@angular/*` -> `^19.2.25`, CLI/build-angular -> `19.2.27`, TypeScript -> `~5.8.3` (see Issues Encountered for the range note), zone.js -> `~0.15.1` (first zone.js minor bump since v16). Two mandatory `@angular/core` codemods ran: (1) the `ExperimentalPendingTasks` -> `PendingTasks` rename, which reported "No changes made" -- confirmed as a genuine no-op since this app never referenced that API; (2) a mandatory rewrite adding explicit `standalone: false` to all 20 NgModule-declared components/directives/pipes, since Angular 19 defaults newly-scaffolded artifacts to standalone unless told otherwise. One of those 20 files, `game-detail.component.ts`, also carries pre-existing unrelated user WIP (a lightbox-carousel refactor) in the same file -- see Deviations below for how this was handled without touching the WIP or skipping the codemod. Verified: build exit 0, test exit 0, `ALL_BUILDERS_OK`, `MAIN_OK`, `DECL_OK`, `ZONE_OK`, `STRICTNESS_OK`, `SMOKE_OK`, zero `ExperimentalPendingTasks`/standalone:true/bootstrapApplication/control-flow markers.
- **Hop 7 (19 -> 20):** `npx ng update @angular/core@20 @angular/cli@20 --allow-dirty`. `@angular/*` -> `^20.3.30`, CLI/build-angular -> `20.3.35` (TypeScript `~5.8.3` / zone.js `~0.15.1` unchanged, already in range). Two mandatory `@angular/cli` codemods ran: (1) added a `schematics` defaults block to `angular.json` (file-naming style-guide compatibility for `ng generate`, e.g. `typeSeparator` for guards/modules/pipes) -- did not touch the `build`/`serve`/`test` architect targets or `test.options.karmaConfig`; (2) bumped `tsconfig.json`'s `moduleResolution` from `node` to `bundler` -- no strictness flag touched. Per RESEARCH.md Pitfall 1, this hop's own mandatory "remove default karma.conf.js" migration ran and reported "No changes made" -- direct proof from the CLI itself that this project's `karma.conf.js` is not the removable default-content stub, and the Karma toolchain (`karma@~6.3.0`) is fully intact. No optional migrations accepted: `use-application-builder`, `control-flow-migration`, and `router-current-navigation` were all offered and declined. Verified: build exit 0, test exit 0, `ALL_BUILDERS_OK` (asserts `karmaConfig === 'karma.conf.js'`), `MAIN_OK`, `DECL_OK`, `ZONE_OK`, `STRICTNESS_OK`, `SMOKE_OK`, `git diff --stat HEAD~1 HEAD -- karma.conf.js` = 0 lines, zero standalone/control-flow markers.
- Confirmed after all three hops: `src/main.ts` still bootstraps via `platformBrowserDynamic().bootstrapModule(AppModule)`, `app.module.ts` still has a `declarations:` array, `polyfills.ts` still imports `zone.js`, and every `tsconfig.json` strictness flag (`strict`, `strictInjectionParameters`, `strictInputAccessModifiers`, `strictTemplates`) remained `true` throughout.
- Node strategy: stayed on Node v24.14.0 across all three hops. RESEARCH.md's v20 hop matrix admits `^24.0.0` for the first time in this chain, so the installed Node line is now officially in-range going forward (no more cosmetic "Unsupported Node" CLI warning expected from this hop on).

## Task Commits

1. **Task 1: Hop 5 — Angular 17 -> 18** - `1f4782e` (chore)
2. **Task 2: Hop 6 — Angular 18 -> 19** - `4b6f412` (chore)
3. **Task 3: Hop 7 — Angular 19 -> 20** - `f3c3d1b` (chore)

## Files Created/Modified

- `game-planner-ui/package.json` - `@angular/*` progressed `17.3.12 -> 18.2.14 -> 19.2.25 -> 20.3.30`; `@angular/cli`/`@angular-devkit/build-angular` progressed in lockstep; `typescript` progressed `~5.4.5 -> ~5.8.3` (crossing the 19/20 boundary in one jump -- ng update resolved the peer range directly to a 5.8.x patch at hop 6, satisfying both hop 6's `<5.7.0` upper bound... see Issues Encountered); `zone.js` progressed `~0.14.10 -> ~0.15.1`; `rxjs` unchanged throughout; `karma` unchanged at `~6.3.0` throughout, confirmed surviving the v20 default-shift
- `game-planner-ui/package-lock.json` - fully regenerated at each of the three hops
- `game-planner-ui/angular.json` - untouched at hops 5 and 6; at hop 7, mandatory migration added a `schematics` defaults block for `ng generate` file-naming compatibility -- all three architect builder strings and `test.options.karmaConfig` confirmed unchanged
- `game-planner-ui/tsconfig.json` - untouched at hops 5 and 6; at hop 7, mandatory migration bumped `compilerOptions.moduleResolution` `node -> bundler`; all four strictness flags preserved through all three hops
- `game-planner-ui/src/app/app.module.ts` - hop 5's mandatory codemod replaced the `HttpClientModule` import with `provideHttpClient(withInterceptorsFromDi())` in the `providers:` array; `declarations:`/`imports:`/`bootstrap:` structure otherwise unchanged
- `game-planner-ui/src/app/app.component.ts` + 19 other component/directive files - hop 6's mandatory codemod added explicit `standalone: false` to each `@Component`/`@Directive` decorator (logic unchanged, purely a type-surface/decorator addition)
- `game-planner-ui/src/app/components/game-detail/game-detail.component.ts` - same hop 6 `standalone: false` codemod applied, but isolated from pre-existing unrelated WIP in this file via the reconstruction technique described in Deviations -- only the codemod's 4-line decorator change is in this plan's git history

## Decisions Made

- Node strategy: stayed on Node v24.14.0 through all three hops, consistent with 01-01/01-02. RESEARCH.md's v20 matrix admits `^24.0.0` for the first time in this chain -- flagged in prior SUMMARYs as a hop to watch (Pitfall 3), but no engine-range miss occurred; no version switch was needed.
- `--allow-dirty` used on every `ng update` invocation in this plan (never `--force`) for the same reason documented in 01-01/01-02: pre-existing, out-of-scope uncommitted changes in the working tree must never be touched. Verified via `git status --short` before and after each of the three hops that only the intended `game-planner-ui/` files changed and the pre-existing unrelated diffs stayed byte-identical.
- `--force` was not needed at hop 5, 6, or 7 -- peer dependency resolution succeeded cleanly all three times.
- No optional migrations were accepted at any hop (`use-application-builder` at all three; `control-flow-migration` and `router-current-navigation` additionally offered at hop 7) -- confirmed by inspecting each hop's full CLI output for the absence of any executed optional schematic.
- New technique this plan (see Deviations): reconstruct-and-restore isolation for a mandatory codemod change landing inside a file with pre-existing unrelated WIP, rather than choosing between "commit the WIP too" (forbidden) or "skip the codemod for this file" (violates D-01's "let ng update do its job" mandate).

## Deviations from Plan

**None requiring Rule 1-4 classification.** One execution-technique addition, documented here for traceability:

**1. [Technique, not a Rule 1-4 deviation] Isolated hop 6's mandatory `standalone: false` codemod from pre-existing WIP in `game-detail.component.ts`**
- **Found during:** Task 2 (hop 6, Angular 18 -> 19)
- **Situation:** The v19 mandatory codemod that adds explicit `standalone: false` to all NgModule-declared components rewrote 21 files, including `game-detail.component.ts`. That file also carries pre-existing, out-of-scope user WIP (a lightbox-carousel refactor: `lightboxIndex`, `imageFading`, `nextImage()`/`prevImage()`, a `HostListener` import) that this plan's constraints forbid touching, staging, or committing. A plain `git add` of the file would have committed both the codemod's change and the WIP together.
- **Resolution:** Reconstructed an isolated version of the file from `git show HEAD:<path>` plus a targeted string replacement matching the exact `@Component` decorator transformation the schematic applied to the other 20 files (4-space reindent, trailing comma, `standalone: false` appended). Wrote that isolated version to disk, `git add`'d it (staging only the 7-line codemod diff), then restored the full mixed working-tree content (WIP + codemod) back to disk before committing. Net result: the commit contains exactly the codemod's change; the working tree afterward is identical to what it would have been had the codemod alone run against a clean tree, with the WIP still present and unstaged exactly as before.
- **Files affected:** `game-planner-ui/src/app/components/game-detail/game-detail.component.ts`
- **Verification:** `git diff HEAD~3 HEAD -- <file>` shows only the `standalone: false` hunk across this plan's three commits; `git status --short` before Task 2 and after Task 3 shows the identical unstaged WIP diff for this file (same three unstaged files: `.css`, `.html`, `.ts`, same content).
- **Commit:** `4b6f412` (isolated codemod change only)

## Issues Encountered

- **TypeScript range note (hop 6):** RESEARCH.md's hop matrix specifies hop 6 (18->19) TypeScript peer range as `>=5.5.0 <5.7.0`. `ng update` resolved TypeScript directly to `~5.8.3` at hop 6 -- outside that stated upper bound, but this is `ng update`'s own dependency resolution (not a hand-pin), and the resulting `~5.8.3` sits correctly inside hop 7's `>=5.8.0 <5.9.0` range, so the chain lands in-range by the end of this plan regardless. Build and test both passed with zero TypeScript compilation errors at hop 6 with TS 5.8.3, so no functional issue resulted. Documented here rather than treated as a blocking discrepancy since the plan's own protocol says "assert the outcome, do not pin by hand" -- the outcome is what `ng update` chose, and it works.
- No `ERESOLVE` conflicts, no schema-validation failures (Pitfall 2's angular.json option-rename failure mode did not manifest as an error at hop 7 -- the schematic added the new `schematics` block cleanly), and no ChromeHeadless launch issues at any of the three hops.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `game-planner-ui` now runs on Angular 20.3.30 with Karma/Jasmine, NgModule bootstrap, all three classic builders, TS strictness, and `moduleResolution: bundler` all intact -- ready for plan `01-04` to continue the hop sequence toward the phase target of v22.
- The `--allow-dirty` pattern, Node v24.14.0 strategy, and JSONC-tolerant `tsconfig.json` parsing note continue to apply unchanged.
- **New for future plans:** if any remaining hop's mandatory codemod touches a file with pre-existing unrelated WIP again (most likely candidates: any of the three `game-detail.component.*` files, or the backend `GameManager.cs`/`GameImageManager.cs` files if a phase ever needed to touch those, which this phase does not), the reconstruct-and-restore isolation technique documented above in Deviations is reusable as-is.
- Node v24.14.0 is now inside RESEARCH.md's stated range for all hops from v20 onward (`^24.0.0` admitted starting at this hop); no further Node concern expected unless a later hop's matrix narrows the range again.
- No blockers.

---
*Phase: 01-angular-eol-upgrade*
*Completed: 2026-08-29*

## Self-Check: PASSED

- FOUND: `.planning/phases/01-angular-eol-upgrade/01-03-SUMMARY.md`
- FOUND: commit `1f4782e` (Angular 17 -> 18)
- FOUND: commit `4b6f412` (Angular 18 -> 19)
- FOUND: commit `f3c3d1b` (Angular 19 -> 20)
