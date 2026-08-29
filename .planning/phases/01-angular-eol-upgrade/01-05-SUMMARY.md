---
phase: 01-angular-eol-upgrade
plan: 05
subsystem: infra
tags: [angular, typescript, npm, ng-update, jasmine, karma, rxjs, dependency-upgrade, frontend]

# Dependency graph
requires:
  - phase: 01-04
    provides: game-planner-ui running on Angular 21.2.22 via hop 8 (20->21), with Node runtime upgraded to v24.19.0 satisfying Angular 22's engine requirement
provides:
  - game-planner-ui running on Angular 22.1.4 via the final hop (21->22), crossing the TypeScript 5.9 -> 6.0 major boundary with zero suppressed type errors and all 8 protected strictness flags intact
  - Jasmine/Karma test harness, RxJS and @types/node bumped to Angular-22-compatible versions, with jasmine-core deliberately pinned one major back (6.3.0, not ecosystem-latest 7.0.2) after a confirmed harness-boot incompatibility with zone.js
  - Nine sequential single-major ng update hops complete (13->14 through 21->22), no version skipped -- DEP-01 and DEP-02 fully satisfied
affects: [01-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Final hop of the sequential ng-update-per-major pattern used in 01-01 through 01-04, now complete"
    - "NEW this plan: isolated a mandatory ng-update codemod's changes from pre-existing unrelated WIP spanning THREE files in one component (game-detail.component.ts/.html/.css), not just one file as in 01-04's precedent -- reconstructed all three from HEAD, applied only the codemod delta to the .ts file (the only one the codemod touched), built/tested against the fully-isolated HEAD+codemod state, committed, then restored the full WIP+codemod merged state to the working tree unstaged"
    - "NEW this plan: verification scripts assuming `require('./tsconfig.json')` works fail in this repo because tsconfig.json carries a leading /* ... */ block comment (present since the initial commit, not introduced by this hop) that breaks strict JSON.parse -- worked around by stripping /\\*[\\s\\S]*?\\*/ before parsing rather than skipping the strictness assertion"
    - "NEW this plan: RESEARCH.md's assumption A1 (jasmine-core 4.x -> 7.x may not boot cleanly against zone.js) was confirmed true -- ecosystem-latest jasmine-core 7.0.2 threw 'Cannot assign to read only property describe' when zone.js's zone-testing patch tried to monkeypatch it; resolved per the plan's own documented fallback by pinning jasmine-core (and @types/jasmine) back one major to 6.3.0/6.0.0, which boots cleanly"

key-files:
  created: []
  modified:
    - game-planner-ui/package.json
    - game-planner-ui/package-lock.json
    - game-planner-ui/tsconfig.json
    - game-planner-ui/tsconfig.app.json
    - game-planner-ui/tsconfig.spec.json
    - game-planner-ui/src/app/app.module.ts
    - game-planner-ui/src/app/app.component.ts
    - game-planner-ui/src/app/components/game-detail/game-detail.component.ts
    - game-planner-ui/src/app/components/session-planner/session-planner.component.ts
    - game-planner-ui/src/app/components/game-list/game-list.component.ts
    - game-planner-ui/src/app/components/steam-import/steam-import.component.ts
    - game-planner-ui/src/app/components/login/login.component.ts
    - game-planner-ui/src/app/components/register/register.component.ts
    - game-planner-ui/src/app/components/profile/profile.component.ts
    - game-planner-ui/src/app/components/workers/workers.component.ts
    - game-planner-ui/src/app/components/calendar/calendar.component.ts
    - game-planner-ui/src/app/components/now-playing/now-playing.component.ts
    - game-planner-ui/src/app/components/backlog-prioritizer/backlog-prioritizer.component.ts
    - game-planner-ui/src/app/components/journal/journal.component.ts
    - game-planner-ui/src/app/components/stats-dashboard/stats-dashboard.component.ts
    - game-planner-ui/src/app/components/dashboard/dashboard.component.ts
    - game-planner-ui/src/app/components/tracked-games/tracked-games.component.ts
    - game-planner-ui/src/app/components/game-card/game-card.component.ts
    - game-planner-ui/src/app/components/igdb-search-modal/igdb-search-modal.component.ts
    - game-planner-ui/src/app/components/sidebar/sidebar.component.ts

key-decisions:
  - "Task 1 (hop 9, 21->22): @angular/* -> ^22.1.4, CLI/build-angular -> 22.1.6, compiler-cli -> 22.1.4, typescript ~5.9.3 -> ~6.0.3 (within Angular 22's required >=6.0.0 <6.1.0 range, NOT the ecosystem-latest 7.x), zone.js unchanged ~0.15.1, rxjs unchanged at this step (~7.5.0, bumped separately in Task 2). Declined both offered optional migrations (migrate-karma-to-vitest, use-application-builder) -- neither ran, matching D-03."
  - "Mandatory codemods accepted: ChangeDetectionStrategy.Eager added to all 19 pre-existing components (runtime-neutral safety net per RESEARCH.md, since Angular 22 only changes the default for NEWLY generated components); withXhr() added to provideHttpClient() in app.module.ts; tsconfig.app.json/tsconfig.spec.json gained an angularCompilerOptions.extendedDiagnostics block suppressing nullishCoalescingNotNullable/optionalChainNotNullable. This last one is NOT one of the 8 protected strictness flags named in the plan's prohibitions (strict, noImplicitOverride, noPropertyAccessFromIndexSignature, noImplicitReturns, noFallthroughCasesInSwitch, strictInjectionParameters, strictInputAccessModifiers, strictTemplates) -- it is a separate, narrower extended-diagnostics category, and all 8 protected flags remain true (verified)."
  - "Genuine TypeScript 6.0 fix (Rule 1/3, not a mandatory codemod): npm run build failed with TS5101 errors on tsconfig.json's baseUrl and downlevelIteration, both flagged as deprecated/incompatible with TypeScript 7.0. Removed both: baseUrl was safe to remove because the codebase uses only relative imports (grep confirmed zero non-relative, non-@angular imports -- CONVENTIONS.md already documents baseUrl as unused for path mapping); downlevelIteration is a no-op under this project's target:ES2022 (native iteration support, no transpilation needed). Neither removal touches the 8 protected strictness flags and neither loosens type-checking -- both are inert/obsolete compiler switches under this project's actual configuration."
  - "Isolated the mandatory ChangeDetectionStrategy codemod from pre-existing unrelated WIP in game-detail.component.ts/.html/.css (the lightbox-carousel refactor spanning all three sibling files, not just the .ts as in 01-04's single-file precedent): reconstructed all three files from HEAD, applied only the two-line codemod delta to the .ts file (import + changeDetection property; the codemod never touches .html/.css), ran the full build+test+boot verification against this isolated HEAD+codemod-only state to confirm the hop's changes were self-sufficient, committed, then restored the full merged (codemod+WIP) content to all three files -- confirmed via `git diff HEAD` that the post-restore working tree shows exactly the original WIP delta with zero codemod artifacts leaking into the unstaged diff."
  - "Task 2 (supporting-dependency bump): jasmine-core, karma, karma-jasmine, karma-chrome-launcher, karma-jasmine-html-reporter, karma-coverage, @types/jasmine all installed at @latest first (jasmine-core 7.0.2). Test harness failed to boot: Chrome threw 'Uncaught TypeError: Cannot assign to read only property describe' from zone.js's zone-testing.js patch, confirming RESEARCH.md's assumption A1 (untested jasmine-core 4.x->7.x compatibility). Per the plan's own documented fallback, pinned jasmine-core back one major to 6.3.0 and @types/jasmine to 6.0.0 (tracking the same major) -- harness then booted cleanly (0 of 0 specs, exit 0). No --force, no legacy-peer-deps, no harness deletion."
  - "rxjs bumped ~7.5.0 -> ^7.8.2 (latest 7.x; confirmed via npm view that no rxjs@8 exists yet, so no risk of accidentally crossing Angular 22's peer boundary). @types/node bumped ^12.11.1 -> ^24.13.3, matching the installed Node 24.19.0 runtime's major. karma landed at ^6.4.4, still within @angular-devkit/build-angular@22's declared ^6.3.0 peer range."
  - "Verification-tooling adaptation (not a plan deviation): the plan's literal `node -e \"require('./tsconfig.json')\"` and `require('./tsconfig.spec.json')` assertions fail in this repo because both files carry a leading /* ... */ block comment present since the project's initial commit (pre-dates this entire phase). Worked around by stripping the comment via regex before JSON.parse in the verification script -- the substance of the check (all 8 strictness flags still true) was fully verified, just via a parser that tolerates the repo's actual file format."

requirements-completed: [DEP-01, DEP-02]

coverage:
  - id: D1
    description: "Angular 21 -> 22 hop (hop 9, final) executed via ng update: build/test/all-builders/strictness/boot-smoke/route-shell all green, TypeScript 6.0.3 installed (not 7.x), zone.js/NgModule/structural-directives all intact, exactly nine hop commits exist total (13->14 through 21->22)"
    requirement: "DEP-01"
    verification:
      - kind: other
        ref: "commit 24fa1e1 -- ANGULAR_22_OK ts=~6.0.3 zone=~0.15.1, npm run build exit 0, npm test exit 0 (0 of 0), ALL_BUILDERS_OK, STRICTNESS_OK (comment-tolerant parse), SMOKE_OK, ROUTE_SHELL_OK (curl on /games/123 and /), STANDALONE_COUNT=0, CONTROLFLOW_COUNT=0, NGIF_COUNT=225, ZONELESS_COUNT=0, TSIGNORE_COUNT=0, git log -1 = 24fa1e1, git show --stat HEAD -- GamePlanner.API Database = 0 lines, git log grep count of hop commits = 9"
        status: pass
    human_judgment: false
  - id: D2
    description: "Supporting dependencies (Jasmine/Karma family, @types/jasmine, @types/node, rxjs) bumped to Angular-22-compatible versions with the Karma test harness confirmed booting; jasmine-core pinned back one major after a genuine boot failure with the ecosystem-latest version, recorded per RESEARCH.md assumption A1's documented fallback"
    requirement: "DEP-02"
    verification:
      - kind: other
        ref: "commit 7e74f69 -- SUPPORT_DEPS_OK jasmine-core=^6.3.0 karma=^6.4.4 types-jasmine=^6.0.0 rxjs=^7.8.2, npm run build exit 0, npm test exit 0 (0 of 0, Karma v6.4.4 server + Chrome Headless 151 connect+run clean after the jasmine-core pin-back), KARMA_BUILDER_OK, SPEC_TYPES_OK, RESOLVE_OK, karma.conf.js byte-identical (0 diff lines), no legacy-peer-deps in .npmrc, npx ng version reports Angular 22.x with no UNMET PEER DEPENDENCY line, git log -1 = 7e74f69"
        status: pass
    human_judgment: false
  - id: D3
    description: "The /games/:id route resolves its id param after Angular 22's paramsInheritanceStrategy default flip, confirmed at the app-shell level; full browser-rendered visual confirmation with live backend data was not performed in this session"
    verification:
      - kind: other
        ref: "curl http://localhost:4200/games/123 returns the app-root shell (ROUTE_SHELL_OK) both immediately after the hop 9 commit and again after the Task 2 dependency bump; git diff confirms GameDetailComponent's ngOnInit/ActivatedRoute.paramMap subscription logic is byte-identical before and after this hop (only the WIP lightbox refactor and the ChangeDetectionStrategy codemod touched the file, neither touches route-param handling); RESEARCH.md independently verified this app's routes are all flat/top-level with no nesting, so the changed default (empty-only -> always inheritance) has no parent-to-child path to affect"
        status: pass
    human_judgment: true
    rationale: "GamePlanner.API was not running during this session (backend untouched per this frontend-only phase's scope), so a full browser load of /games/<real id> rendering actual game data could not be captured. The shell-level curl check and the unchanged-route-logic diff give strong confidence, but a human should do one visual pass on /games/:id in a real browser with the backend running before treating the router-flip risk as fully closed."

duration: ~65min
completed: 2026-08-29
status: complete
---

# Phase 1 Plan 5: Angular 21 -> 22 (final hop) + TypeScript 6.0 + Supporting Dependencies Summary

**Completed the nine-hop Angular EOL upgrade chain by landing Angular 22.1.4 with TypeScript 6.0.3, then separately bumped the Jasmine/Karma/RxJS/@types-node supporting dependencies -- discovering and resolving a real jasmine-core 7.x boot incompatibility with zone.js along the way, exactly as RESEARCH.md's assumption A1 warned it might.**

## Performance

- **Duration:** ~65 min
- **Started:** 2026-08-29T17:53Z (approx, prior to background ng update invocation)
- **Completed:** 2026-08-29T18:10Z
- **Tasks:** 2 completed (Task 1: hop 9, 21->22 + TS 6.0; Task 2: supporting-dependency bump)
- **Files modified:** 27 total (25 in Task 1's hop commit, 2 in Task 2's dependency commit)

## Accomplishments

- **Task 1 (Hop 9, 21 -> 22, final):** `npx ng update @angular/core@22 @angular/cli@22 --allow-dirty`. `@angular/*` -> `^22.1.4`, CLI -> `22.1.6`, build-angular -> `22.1.6`, compiler-cli -> `22.1.4`, `typescript` `~5.9.3 -> ~6.0.3` (within Angular 22's required `>=6.0.0 <6.1.0` range -- NOT the ecosystem-latest 7.x, which would have broken the peer range). Both offered optional migrations (`migrate-karma-to-vitest`, `use-application-builder`) were declined by not invoking them; the Karma builder trio survived unchanged. Mandatory codemods: `ChangeDetectionStrategy.Eager` added to all 19 pre-existing components (a runtime-neutral safety net, since Angular 22 only changes the *default* for newly-generated components); `withXhr()` added to `provideHttpClient()` in `app.module.ts`; an `angularCompilerOptions.extendedDiagnostics` suppression block added to `tsconfig.app.json`/`tsconfig.spec.json` for `nullishCoalescingNotNullable`/`optionalChainNotNullable` (a narrower diagnostic category, distinct from and not overlapping with the 8 protected strictness flags).
- **Genuine TypeScript 6.0 fix:** `npm run build` failed with TS5101 on `tsconfig.json`'s `baseUrl` and `downlevelIteration`, both flagged deprecated/TS7-incompatible. Removed both -- `baseUrl` is unused (all imports in this codebase are relative, confirmed by grep and by CONVENTIONS.md), and `downlevelIteration` is a no-op under this project's `target: ES2022`. Neither removal touches any of the 8 protected strictness flags (all verified still `true`).
- **Multi-file WIP isolation (new technique):** the mandatory `ChangeDetectionStrategy` codemod touched `game-detail.component.ts`, which carries a pre-existing uncommitted lightbox-carousel WIP refactor spanning three sibling files (`.ts`, `.html`, `.css` -- unlike 01-04's single-file precedent). Reconstructed all three from `HEAD`, applied only the codemod's two-line delta to the `.ts` file, ran the full build/test/boot verification against this isolated state, committed, then restored the merged (codemod + WIP) content to all three files. `git diff HEAD` post-restore confirms the working tree shows exactly the original WIP delta with zero codemod artifacts bleeding into the unstaged diff.
- **Verified:** build exit 0, test exit 0 (0 of 0 specs), `ALL_BUILDERS_OK`, `STRICTNESS_OK` (all 8 flags true, verified via a comment-tolerant JSON parse -- see Decisions), `SMOKE_OK`/`ROUTE_SHELL_OK` (dev server boots, `/games/123` shell serves), zero standalone/control-flow/zoneless/ts-ignore markers, exactly nine hop commits total.
- **Task 2 (supporting-dependency bump):** `jasmine-core`, `karma`, `karma-jasmine`, `karma-chrome-launcher`, `karma-jasmine-html-reporter`, `karma-coverage`, `@types/jasmine` installed at `@latest` first (`jasmine-core` landed at `7.0.2`). The Karma test harness then **failed to boot**: Chrome Headless threw `Uncaught TypeError: Cannot assign to read only property 'describe'` from zone.js's `zone-testing.js` patch attempting to monkeypatch the new jasmine-core's `Env` prototype. This is exactly RESEARCH.md's assumption A1 (the 4.x -> 7.x leap was untested). Per the plan's documented fallback, pinned `jasmine-core` back one major to `6.3.0` and `@types/jasmine` to `6.0.0` (same major family) -- harness then booted cleanly (`0 of 0` specs, exit 0). `rxjs` bumped to `^7.8.2` (latest 7.x; confirmed no `rxjs@8` exists yet). `@types/node` bumped to `^24.13.3`, matching the installed Node `24.19.0` runtime. `karma` landed at `^6.4.4`, still inside `@angular-devkit/build-angular@22`'s `^6.3.0` peer range.
- **Verified:** `SUPPORT_DEPS_OK`, build exit 0, test exit 0, `KARMA_BUILDER_OK`, `SPEC_TYPES_OK`, `RESOLVE_OK`, `karma.conf.js` byte-identical, no `legacy-peer-deps` anywhere, `npx ng version` shows no `UNMET PEER DEPENDENCY`.

## Task Commits

1. **Task 1: Hop 9 — Angular 21 -> 22 + TypeScript 6.0** - `24fa1e1` (chore)
2. **Task 2: Jasmine/Karma/RxJS/@types-node supporting-dependency bump** - `7e74f69` (chore)

## Files Created/Modified

- `game-planner-ui/package.json` / `package-lock.json` - Angular `21.2.22 -> 22.1.4/22.1.6`; `typescript` `~5.9.3 -> ~6.0.3`; then (Task 2) `jasmine-core ~4.0.0 -> ^6.3.0`, `karma ~6.3.0 -> ^6.4.4`, `karma-jasmine/-chrome-launcher/-jasmine-html-reporter/-coverage` bumped, `@types/jasmine ~3.10.0 -> ^6.0.0`, `rxjs ~7.5.0 -> ^7.8.2`, `@types/node ^12.11.1 -> ^24.13.3`
- `game-planner-ui/tsconfig.json` - removed deprecated `baseUrl` and `downlevelIteration` compiler options (genuine TS 6.0 fix, not a strictness change)
- `game-planner-ui/tsconfig.app.json` / `tsconfig.spec.json` - mandatory `extendedDiagnostics` suppression for `nullishCoalescingNotNullable`/`optionalChainNotNullable`
- `game-planner-ui/src/app/app.module.ts` - mandatory `withXhr()` codemod on `provideHttpClient()`
- `game-planner-ui/src/app/app.component.ts` and 18 other component `.ts` files - mandatory `ChangeDetectionStrategy.Eager` codemod (import + decorator property; runtime-neutral for pre-existing components)

## Decisions Made

See `key-decisions` in frontmatter for full detail. Summary:
- TypeScript pinned to `~6.0.3` (not ecosystem-latest 7.x) to stay inside Angular 22's required range.
- Both optional v22 migrations (Vitest, application-builder) declined; classic Karma/browser/dev-server trio preserved.
- `baseUrl`/`downlevelIteration` removed from `tsconfig.json` as a genuine TS 6.0 compatibility fix (neither is a protected strictness flag; neither is used by this codebase's actual import/target configuration).
- Multi-file WIP isolation technique extended from 01-04's single-file precedent to three sibling files in one component.
- `jasmine-core`/`@types/jasmine` deliberately pinned one major back from ecosystem-latest after confirming a genuine harness-boot failure -- exactly the outcome RESEARCH.md's assumption A1 flagged as unverified risk.
- Verification scripts adapted to tolerate `tsconfig*.json`'s pre-existing leading block comment (present since initial commit) rather than skip the strictness assertion.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1/3 - Bug/Blocking] Removed deprecated `baseUrl` and `downlevelIteration` from tsconfig.json**
- **Found during:** Task 1 (hop 9, Angular 21 -> 22, TypeScript 6.0 jump)
- **Issue:** `npm run build` failed with `TS5101: Option 'baseUrl' is deprecated...` and the same for `downlevelIteration`, both flagged as incompatible with the upcoming TypeScript 7.0.
- **Fix:** Removed both options. Verified `baseUrl` was unused for path-mapped imports (all imports in this codebase are relative, per grep and CONVENTIONS.md) and `downlevelIteration` was a no-op under this project's `target: ES2022`. Neither of the 8 protected strictness flags was touched.
- **Files modified:** `game-planner-ui/tsconfig.json`
- **Verification:** `npm run build` exits 0; `STRICTNESS_OK` assertion (comment-tolerant parse) confirms all 8 protected flags still `true`.
- **Committed in:** `24fa1e1` (Task 1 commit)

**2. [Fallback per RESEARCH.md assumption A1, plan-documented, not a rule deviation] Pinned jasmine-core/@types/jasmine back one major**
- **Found during:** Task 2 (supporting-dependency bump)
- **Situation:** `jasmine-core@latest` (7.0.2) broke the Karma test harness boot -- `zone.js`'s `zone-testing.js` patch threw `Cannot assign to read only property 'describe'` when attempting to monkeypatch the new jasmine `Env` shape.
- **Resolution:** Pinned `jasmine-core` to `6.3.0` and `@types/jasmine` to `6.0.0` (same major family), exactly as the plan's own fallback instructions specified. Harness then booted cleanly.
- **Files affected:** `game-planner-ui/package.json`, `package-lock.json`.
- **Verification:** `npm test -- --watch=false --browsers=ChromeHeadless` exits 0, `0 of 0` specs, no plugin/connection errors.
- **Committed in:** `7e74f69` (Task 2 commit)

**3. [Technique, not a Rule 1-4 deviation] Multi-file WIP isolation for the ChangeDetectionStrategy codemod**
- **Found during:** Task 1 (hop 9)
- **Situation:** The mandatory codemod touched `game-detail.component.ts`, which carries pre-existing uncommitted WIP (a lightbox-carousel refactor) spanning three sibling files (`.ts`, `.html`, `.css`) -- a wider blast radius than 01-04's single-file precedent.
- **Resolution:** Reconstructed all three files from `HEAD`, applied only the codemod's two-line delta to the `.ts` file (the only one the codemod touches), verified build/test/boot against this isolated state, committed, then restored the full merged content to all three files. Confirmed via `git diff HEAD` that the post-commit working tree shows exactly the original WIP delta.
- **Files affected:** `game-planner-ui/src/app/components/game-detail/game-detail.component.ts` (committed with codemod-only delta); `.html`/`.css` untouched by the commit, WIP restored unstaged afterward.
- **Verification:** `git diff HEAD -- game-detail.component.ts` post-restore shows only the pre-existing lightbox WIP; `git status --short` after both commits matches the exact pre-plan working-tree state (only the unrelated backend/game-detail WIP and untracked dirs remain).
- **Commit:** `24fa1e1` (isolated codemod delta only)

---

**Total deviations:** 1 auto-fixed bug (Rule 1/3, tsconfig deprecated options), 1 documented plan-fallback (jasmine-core pin-back, not a rule violation -- explicitly anticipated and instructed by the plan itself), plus 1 non-rule execution technique (multi-file WIP isolation).
**Impact on plan:** All necessary to land a genuinely green, non-suppressed build on TypeScript 6.0 and a genuinely booting test harness. No scope creep; no strictness flags loosened; no WIP lost or mixed into the hop commits.

## Issues Encountered

- **tsconfig.json/tsconfig.spec.json JSON.parse incompatibility with verification scripts (pre-existing, not introduced by this plan):** both files carry a leading `/* ... */` block comment present since the project's initial commit. The plan's literal `node -e "require('./tsconfig.json')"` verification pattern fails on this repo's actual file format (strict `JSON.parse` rejects comments). Worked around by stripping the comment via regex before parsing in the verification script -- the underlying assertion (all 8 strictness flags still `true`) was fully and correctly verified, just via a parser tolerant of this repo's pre-existing file format. Not a deviation in the code itself, only in how the check was executed.
- **`src/main.ts` MAIN_OK grep mismatch (pre-existing, not introduced by this plan):** the plan's literal grep pattern `bootstrapModule(AppModule)` (with the closing paren immediately after `AppModule`) does not match this repo's `main.ts`, because an earlier hop's mandatory codemod (documented in 01-04-SUMMARY.md) already added `{ applicationProviders: [provideZoneChangeDetection()] }` as a second argument, making the literal text `bootstrapModule(AppModule, { ... })`. Confirmed via `git diff HEAD -- src/main.ts` that this hop did not touch `main.ts` at all, and confirmed via grep that the NgModule bootstrap pattern (`bootstrapModule(AppModule` with no `bootstrapApplication` anywhere) is fully intact -- this is a stale literal-match assumption in the verify script from earlier in the phase, not a regression.
- **Backend not running during this session:** `GamePlanner.API` was not started (out of scope for this frontend-only phase; also has pre-existing unrelated uncommitted WIP that must not be touched). The `/games/:id` route-param resolution after the v22 `paramsInheritanceStrategy` flip was therefore verified at the app-shell level (curl confirms the route resolves to the app shell, and `GameDetailComponent`'s route-param-reading logic is byte-identical before/after this hop) rather than with a full browser render of live game data. Flagged as `D3` in the coverage block with `human_judgment: true` for a follow-up visual spot-check.

## User Setup Required

None for this plan. A follow-up recommendation (not blocking): with the backend running, load `http://localhost:4200/games/<a real game id>` in a browser once to visually confirm `GameDetailComponent` renders real data for that id -- RESEARCH.md's flat-route-topology finding and the unchanged route-handling code give strong confidence this will work, but no browser-based visual confirmation was captured in this non-interactive session.

## Next Phase Readiness

- `game-planner-ui` is now on Angular 22.1.4 with TypeScript 6.0.3, zone.js 0.15.1, RxJS 7.8.2, and a fully Angular-22-compatible Jasmine/Karma test harness (with `jasmine-core` deliberately pinned to 6.3.0, not ecosystem-latest 7.x).
- Nine sequential hop commits exist in git history (13->14 through 21->22), one per major, no version skipped -- DEP-01 satisfied.
- DEP-02 satisfied: TypeScript, zone.js, RxJS, and the entire Jasmine/Karma family plus `@types/jasmine`/`@types/node` all land on Angular-22-compatible versions with the harness confirmed booting.
- Classic browser/dev-server/karma builder trio, NgModule architecture, structural directives, and zone.js change detection all preserved unchanged through all nine hops (D-03, D-06).
- Ready for plan `01-06`: RESEARCH.md flagged a final `npm audit` pass against the completed dependency set as the next step, plus any residual EOL-upgrade wrap-up (bundle-budget warning noted but not blocking, pre-existing since early in the phase; jasmine-core pin-back to document/monitor if the ecosystem stabilizes a working 7.x combination later).
- No blockers. One follow-up recommendation: a human visual spot-check of `/games/:id` with the backend running (see User Setup Required above).

---
*Phase: 01-angular-eol-upgrade*
*Completed: 2026-08-29*
