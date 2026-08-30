---
phase: 01-angular-eol-upgrade
reviewed: 2026-08-30T01:31:08Z
depth: standard
files_reviewed: 29
files_reviewed_list:
  - game-planner-ui/angular.json
  - game-planner-ui/package-lock.json
  - game-planner-ui/package.json
  - game-planner-ui/src/app/app.component.ts
  - game-planner-ui/src/app/app.module.ts
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
  - game-planner-ui/src/app/guards/auth.guard.ts
  - game-planner-ui/src/main.ts
  - game-planner-ui/src/test.ts
  - game-planner-ui/tsconfig.app.json
  - game-planner-ui/tsconfig.json
  - game-planner-ui/tsconfig.spec.json
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: issues_found
---

# Phase 1: Code Review Report

**Reviewed:** 2026-08-30T01:31:08Z
**Depth:** standard
**Files Reviewed:** 29
**Status:** issues_found

## Summary

Reviewed the full Angular 13→22 EOL upgrade diff (9 sequential `ng update` hops) across all touched `.ts` config and component files, `package.json`/`package-lock.json`, and the three `tsconfig*.json` files. Verified independently rather than trusting the phase SUMMARY claims:

- `npx tsc -p tsconfig.app.json --noEmit` — **0 errors**.
- `npm run build` — **exit 0** (only the pre-existing bundle-budget warning and the expected `@angular-devkit/build-angular:browser` deprecation notice, both already documented in `01-05/01-06-SUMMARY.md`).
- `package-lock.json`'s root `dependencies`/`devDependencies` are byte-identical to `package.json`'s — no drift.
- Every touched package's resolved version falls inside Angular 22.1.4's declared peer range (`zone.js ~0.15.0||~0.16.0`, `rxjs ^6.5.3||^7.4.0`, `typescript >=6.0.0 <6.1.0`), confirmed by reading the actual peer-dependency blocks in `package-lock.json`, not just the SUMMARY's table.
- `npm ls --all` shows only `UNMET OPTIONAL DEPENDENCY` entries for unused optional Angular subpackages (SSR, localize, service-worker, ng-packagr platform binaries) — expected noise, not a real dependency gap.
- The `ChangeDetectionStrategy.Eager` value used on every one of the 19 reviewed components is a real, non-deprecated Angular 22 enum member (`Eager = 1`, replacing the deprecated `Default = 1`) — confirmed against `@angular/core`'s shipped `.d.ts`, not assumed.
- `src/test.ts`'s removal of the manual `require.context(...)` spec-loader is correctly superseded by the karma builder's `find-tests-plugin`, which auto-discovers specs from the `tsconfig.spec.json` `include` glob — confirmed by inspecting `node_modules/@angular-devkit/build-angular/src/builders/karma/find-tests-plugin.js`, not just trusting the diff looks intentional.
- The decision to keep the classic `browser`/`dev-server`/`karma` builder trio (declining the `use-application-builder` migration at every hop) is a deliberate, explicitly-tested decision (D-03) recorded in `01-01-PLAN.md` through `01-06-SUMMARY.md`, verified by a builder-string assertion at each hop and a full Playwright UAT pass — not an oversight.

No BLOCKER-tier defects found: no incorrect migration artifacts, no broken imports, no type-safety regressions, no peer-dependency mismatches. The two WARNING findings below are legitimate follow-up items surfaced by tracing what the mandatory codemods actually changed, not restatements of already-documented decisions.

## Warnings

### WR-01: `extendedDiagnostics` suppression silently disables two null-safety template checks project-wide

**File:** `game-planner-ui/tsconfig.app.json:15-21`, `game-planner-ui/tsconfig.spec.json:18-24`
**Issue:** The 21→22 hop's mandatory codemod added:
```json
"angularCompilerOptions": {
  "extendedDiagnostics": {
    "checks": {
      "nullishCoalescingNotNullable": "suppress",
      "optionalChainNotNullable": "suppress"
    }
  }
}
```
This is a blanket, app-wide suppression of two Angular template extended diagnostics that specifically exist to catch dead/misleading null-guard code (`?.` or `??` used on an expression the compiler can now prove is never null/undefined — a signal that either the guard is dead code or a type is wrong somewhere upstream). The codemod was accepted automatically because TypeScript 6.0's narrower nullability inference started flagging existing `?.`/`??` usages across the 19 components (e.g. `this.game?.aggregatedRating`, `this.profile.snapshot?.profileImageUrl`, `this.hoverRating ?? this.journal.userRating ?? 0` patterns seen throughout `game-detail.component.ts`, `journal.component.ts`, `app.component.ts`) that previously compiled without complaint. Suppressing the check silences the compiler rather than confirming whether each flagged expression's null guard is legitimately defensive or now provably dead code — a masked signal, not a fix.
**Fix:** Track this as a follow-up cleanup item: temporarily re-enable both checks (`"warning"` first, not `"error"`, to avoid a hard build break), triage the resulting diagnostic list component-by-component, and either remove the now-provably-unnecessary guards or leave a narrower per-file/per-line suppression with a comment explaining why the guard is still needed (e.g. runtime data from an untyped HTTP response can still be null despite the DTO's static type). Do not carry a project-wide suppression forward indefinitely — it will also hide *newly introduced* null-safety bugs in future template code, not just the pre-existing ones the migration surfaced.

### WR-02: Production build pipeline is pinned to a builder Angular has publicly deprecated

**File:** `game-planner-ui/angular.json:41-42, 92-101, 109-110`
**Issue:** `npm run build` prints, on every invocation:
```
The "@angular-devkit/build-angular:browser" builder is deprecated as part of Angular's Webpack support deprecation. Use "@angular/build:application" instead.
```
This is a deliberate, well-documented scope decision for this phase (D-03: no `use-application-builder` migration accepted at any hop, to avoid conflating a build-pipeline replacement with the version-bump-only goal of this phase) — not a phase-1 defect, and it was verified working end-to-end (build, karma, and a full Playwright UAT pass all green on this exact config). Flagging it here only because Angular's own CLI output states this builder's Webpack support is being phased out, which means this phase's frontend is now shipping on a build pipeline with a defined expiration date that isn't tracked anywhere in `ROADMAP.md`/`STATE.md` outside this phase's own planning artifacts.
**Fix:** No action required for phase 1 to close out. Before the next Angular major hop (23+) is attempted, budget a dedicated migration step for `ng update` with the `use-application-builder` schematic accepted — do not let it get silently bundled into a future version-bump-only phase, since (per this phase's own `01-02-PLAN.md` risk log) an accidentally-accepted builder switch changes the production build pipeline, not just dependency versions.

## Info

### IN-01: `app.module.ts`'s `@NgModule` decorator has codemod-mangled formatting

**File:** `game-planner-ui/src/app/app.module.ts:29-56`
**Issue:** The `ng update` codemod that rewrote `HttpClientModule` → `provideHttpClient(withXhr(), withInterceptorsFromDi())` reformatted the surrounding `@NgModule({...})` block with irregular indentation (e.g. `bootstrap: [AppComponent], imports: [BrowserModule,` on one line, mismatched nesting elsewhere) that doesn't match the project's stated 2-space, prettier-consistent style used everywhere else in the codebase.
**Fix:** Run the file through the project's normal formatter (or manually re-indent the decorator) in a follow-up cosmetic commit — purely a readability issue, no functional impact (confirmed via `tsc --noEmit` and a successful build).

### IN-02: `main.ts`'s `bootstrapModule` options object has a trailing-comma artifact from the codemod

**File:** `game-planner-ui/src/main.ts:11`
**Issue:** `platformBrowserDynamic().bootstrapModule(AppModule, { applicationProviders: [provideZoneChangeDetection()], })` — the mandatory codemod left a trailing comma before the closing `}` inside an otherwise single-line object literal, which is inconsistent with the rest of the file's style and slightly harder to scan than `{ applicationProviders: [provideZoneChangeDetection()] }`.
**Fix:** Strip the trailing comma in a follow-up cosmetic pass; no functional effect.

---

_Reviewed: 2026-08-30T01:31:08Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
