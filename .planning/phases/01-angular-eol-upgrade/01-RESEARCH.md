# Phase 1: Angular EOL Upgrade - Research

**Researched:** 2026-08-28
**Domain:** Angular framework major-version upgrade (13.3.0 → current LTS), sequential `ng update` hops, Node/TypeScript/RxJS/zone.js/Jasmine/Karma compatibility
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Target version**
- **D-01:** Upgrade all the way to whatever Angular version is under active LTS support at execution time (not a fixed stop like v18) — re-confirm the exact current LTS number via angular.dev when the phase is executed. Maximizes the security-patch runway, which is the point of this milestone.
- **D-02:** If a hop requires a newer Node.js than what's installed, upgrade Node locally as part of this phase rather than stopping to ask. — **Reversibility:** reversible — local dev-machine tooling change, no shared/deployed impact.

**Modernization scope**
- **D-03:** Pure dependency bump only. Keep NgModules and `*ngIf`/`*ngFor` structural directives as-is; do not opt into `ng update`'s optional migrations to standalone components or the new `@if`/`@for` control-flow syntax. Only accept migrations `ng update` applies automatically/mandatorily (e.g. codemods for removed APIs). Minimizes diff and risk to match the "no functional regressions" bar for a hardening milestone. — **Reversibility:** reversible — nothing here is a one-way architectural commitment; a later phase can adopt standalone/new syntax independently.

**Per-hop verification & commits**
- **D-04:** After each major-version hop (13→14→15→...), run a build + boot smoke check before moving to the next hop. Reserve the full manual walkthrough of every user workflow (per ROADMAP.md Phase 1 success criterion 3) for the final target version.
- **D-05:** One git commit per successful hop, not one commit for the whole phase — preserves bisectability if a regression surfaces later that traces back to a specific hop. This matches the existing project decision (STATE.md) that Phase 1 is isolated for bisectability.

**Zoneless change detection**
- **D-06:** Keep zone.js — do not adopt Angular's (experimental) zoneless change detection in this phase. Just bump zone.js to the version matching the target Angular release. Adopting zoneless would touch app-wide change-detection behavior, contradicting the "pure dependency bump" / "no functional regressions" decisions above. — **Reversibility:** reversible — zoneless is an opt-in mode; staying on zone.js now doesn't block adopting it later.

### Claude's Discretion
None — all four discussed areas resolved to explicit user decisions above.

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope. Standalone components and `@if`/`@for` control-flow migration were explicitly discussed and deferred as out-of-scope for this phase (see D-03), not lost — a future phase could pick them up if desired.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-------------------|
| DEP-01 | Angular upgraded sequentially from 13.3.0 to current LTS (13→...→target, no version skipping) | Full hop-by-hop matrix (13→14→...→22) with exact `ng update` commands, Node/TypeScript requirements per hop — see Code Examples; target-version determination — see Summary and Open Questions Q1 |
| DEP-02 | Related frontend dependencies (TypeScript, RxJS, zone.js, Jasmine, Karma) upgraded to versions compatible with the target Angular | Full compatibility table for TypeScript/RxJS/zone.js per hop (npm-registry-verified) — see Standard Stack and Code Examples; Jasmine/Karma version guidance and the "Karma still works at v22" finding — see Standard Stack (Supporting) and Pitfall 1 |
</phase_requirements>

## Summary

The frontend must move from Angular 13.3.0 through every intermediate major version — 14, 15, 16, 17, 18, 19, 20, 21, 22 — with no version skipped. Angular 22.1.4 is the current stable release (released 2026-06-03) and is presently in its "Active" support window (ends ~2027-06, followed by LTS through ~2028-06); Angular 21 and Angular 20 are the two versions currently in LTS (critical-fixes-only) status. Per D-01's intent ("maximize the security-patch runway"), the target is **Angular 22** — it is the latest stable release and offers the longest remaining support window of any release; stopping at v21 or v20 would mean landing on a version already past its active-development phase. `ng update` is the only supported upgrade mechanism (per hop, not skipping majors) — hand-editing `package.json` version numbers does not run the code-mod schematics that make each hop safe, and un-run mandatory migrations are the single most common source of silent breakage in this kind of upgrade.

RxJS requires no major-version bump anywhere in this range — `^6.5.3 || ^7.4.0` is the peer-dependency range at every hop from 14 through 22, confirmed directly against the npm registry, so the project's existing `rxjs@~7.5.0` only needs a minor bump to the latest 7.x. zone.js, by contrast, bumps four times across the range (0.11.4 → 0.12.0 → 0.13.0 → 0.14.0 → 0.15.0 → 0.16.x) and TypeScript bumps at nearly every hop, with the final hop to v22 requiring TypeScript 6.0.x — a new major TypeScript version. Node.js must reach at least 22.22.3, 24.15.0, or 26.0.0 to run Angular 22; the currently installed Node v24.14.0 is one patch version short of the nearest satisfying range and must be bumped per D-02.

The single highest-value finding from this research: **Karma is not actually removed at Angular 22** — despite widespread blog commentary saying "Karma support removed in Angular 22," direct inspection of the npm registry shows `@angular-devkit/build-angular@22.0.0` still declares `karma: ^6.3.0` as a peer dependency and the classic `@angular-devkit/build-angular:karma` builder still exists and works. What changed is only the *default* for brand-new projects (`ng new` now scaffolds Vitest) and the optional `use-application-builder` migration prompt — both of which this phase should decline, since neither is a mandatory `ng update` migration. As long as `@angular-devkit/build-angular` stays in `devDependencies` (it already is) and `angular.json`'s `test` target keeps `"builder": "@angular-devkit/build-angular:karma"` (it already does), Jasmine/Karma keep working unmodified through v22, satisfying D-06 and success criterion 2 without any test-runner migration.

**Primary recommendation:** Run `ng update @angular/core@N @angular/cli@N` sequentially for N = 14, 15, 16, 17, 18, 19, 20, 21, 22 (one commit per hop per D-05), decline every optional migration prompt (standalone conversion, control-flow syntax, application builder, Vitest), let mandatory migrations run automatically, bump Node to at least 24.15.0 before the v22 hop (or 22.22.3 if downgrading is preferred), and manually verify `angular.json`'s `test` builder still reads `@angular-devkit/build-angular:karma` after each hop that touches build tooling (v17, v20, v21 are the hops where CLI schematic defaults shifted).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Framework/runtime version | Browser / Client (build+runtime) | — | Angular core, compiler, and CLI all live in the frontend build pipeline; no backend surface is touched |
| Change detection (zone.js) | Browser / Client | — | zone.js patches browser APIs (setTimeout, DOM events) to trigger Angular's runtime; purely a client-side concern |
| Routing behavior (paramsInheritanceStrategy default change at v22) | Browser / Client | — | Angular Router runs entirely client-side; no server-side rendering is in use in this app |
| Test tooling (Karma/Jasmine) | Build tooling (dev-only) | — | Runs in a headless/real browser launched by the Angular CLI at build/test time; never ships to production or touches the API |
| Node.js/TypeScript toolchain | Build tooling (dev-only) | — | Compile-time only; does not affect the deployed Angular bundle's runtime behavior |

No capability in this phase touches the API / Backend, CDN / Static, or Database / Storage tiers — this matches the CONTEXT.md decision that Phase 1 is isolated from `GamePlanner.API`.

## Standard Stack

### Core

| Library | Version (target, v22 hop) | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `@angular/core`, `@angular/common`, `@angular/compiler`, `@angular/forms`, `@angular/platform-browser`, `@angular/platform-browser-dynamic`, `@angular/router`, `@angular/animations` | `^22.0.0` (22.1.4 confirmed latest) [VERIFIED: npm registry] | Angular framework packages | Already the app's framework; no alternative considered — this is a version bump, not a framework migration |
| `@angular/cli`, `@angular-devkit/build-angular`, `@angular/compiler-cli` | `^22.0.0` [VERIFIED: npm registry] | Build tooling, CLI schematics, AOT compiler | Must track `@angular/core` major exactly — CLI and core are released in lockstep |
| `typescript` | `>=6.0.0 <6.1.0` at v22 (per `@angular/core@22.0.0` peer range) [VERIFIED: npm registry] | Language/compiler | Angular enforces a narrow TS range per major; TS 6.0.x is a new major version relative to the app's current 4.6.2 |
| `rxjs` | `^7.4.0` minimum, use latest 7.x (7.8.2 confirmed) [VERIFIED: npm registry] | Reactive streams, HttpClient, Router observables | Peer range `^6.5.3 \|\| ^7.4.0` is unchanged from Angular 14 through Angular 22 — no RxJS major bump forced by this upgrade |
| `zone.js` | `~0.15.0 \|\| ~0.16.0` at v22 (latest 0.16.2 confirmed) [VERIFIED: npm registry] | Change detection trigger via monkey-patched browser APIs | Required because D-06 keeps zone.js-based change detection (no zoneless adoption) |
| `tslib` | latest 2.x (already `^2.3.0`, compatible) | TypeScript helper functions | Unaffected by this upgrade — no action needed unless `ng update` bumps it automatically |

### Supporting (test tooling — kept per D-02/D-06, not migrated to Vitest)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `karma` | `^6.3.0` (still the peer range declared by `@angular-devkit/build-angular@22.0.0`) [VERIFIED: npm registry] | Test runner | Required peer of `@angular-devkit/build-angular:karma` builder at every version through v22 |
| `jasmine-core` | latest 7.x (7.0.2 confirmed on npm; app currently pinned to `~4.0.0`) [ASSUMED — jump from 4.x to 7.x is a real major-version leap, not verified against a compatibility matrix; treat as needing its own smoke check] | Test framework/assertion library | Not an Angular peer dependency — versioned independently. Since there are zero `.spec.ts` files, a mismatch would surface as a Karma/Jasmine boot failure, not a test-content failure |
| `karma-jasmine` | latest 5.x (5.1.0 confirmed) [ASSUMED] | Karma↔Jasmine adapter | Same independence caveat as jasmine-core |
| `karma-chrome-launcher` | latest 3.2.0 [ASSUMED] | Launches Chrome for Karma | Unaffected by Angular version |
| `karma-jasmine-html-reporter` | latest 2.3.0 [ASSUMED] | HTML test reporter | Unaffected by Angular version |
| `karma-coverage` | latest 2.2.1 [ASSUMED] | Coverage reporting | Unaffected by Angular version |
| `@types/jasmine` | latest 6.x (6.0.0 confirmed) [ASSUMED] | TypeScript types for Jasmine | Must track `jasmine-core` major, not Angular |
| `@types/node` | latest matching installed Node major (26.x confirmed on npm; align to whatever Node major is actually installed) [ASSUMED] | TypeScript types for Node APIs | Should roughly track the installed Node.js major version |

**Note on test-tooling versions:** Unlike `@angular/core`/`@angular/cli`/`zone.js`/`typescript`, the Jasmine/Karma family is **not** gated by Angular's own peer-dependency ranges (only `karma` itself is, via `@angular-devkit/build-angular`). `ng update` will not automatically pick "correct" versions for `jasmine-core`, `karma-jasmine`, etc. — the planner should treat these as an independent `npm install <pkg>@latest` step, verified by running `npm test` (or `ng test --watch=false`) after the bump, not by trusting `ng update`'s output.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Stop at Angular 22 (latest stable) | Stop at Angular 21 (in LTS now) or Angular 20 (in LTS now) | Both are valid "current LTS" readings of D-01, but both give a shorter total support runway than v22 and would still require the same number of upgrade hops minus one or two — negligible effort savings for meaningfully less patch runway. Recommend v22. |
| Keep `@angular-devkit/build-angular:karma` builder | Migrate to `@angular/build:unit-test` + Vitest via `ng generate @angular/core:karma-to-vitest` | Explicitly out of scope — D-02/D-06 keep Jasmine/Karma; the Vitest migration is optional, not mandatory, and changes the test command entirely, which would violate D-03's "pure dependency bump" framing |
| Keep classic webpack-based `@angular-devkit/build-angular:browser`/`:dev-server` build targets | Opt into the new esbuild/Vite `application` builder via `ng update @angular/cli --name use-application-builder` | This is an **optional, prompted** migration starting at v17; declining it keeps the diff minimal per D-03. Revisit in a future phase if build-performance work is ever prioritized. |

**Installation (representative final-state target versions — actual bump happens incrementally, one major per hop):**
```bash
# Executed once per hop, not all at once — see Architecture Patterns below
ng update @angular/core@<N> @angular/cli@<N>
# after the final v22 hop, independently bump test tooling:
npm install --save-dev jasmine-core@latest karma@latest karma-jasmine@latest \
  karma-chrome-launcher@latest karma-jasmine-html-reporter@latest karma-coverage@latest \
  @types/jasmine@latest
```

**Version verification performed this session (npm registry, 2026-08-28):**
- `@angular/core` latest: `22.1.4`
- `zone.js` latest: `0.16.2`
- `rxjs` latest: `7.8.2`
- `karma` latest: `6.4.4`
- `jasmine-core` latest: `7.0.2`
- `karma-jasmine` latest: `5.1.0`
- `typescript` latest (ecosystem-wide, not Angular-constrained): `7.0.2` — **do not install this**; Angular 22 requires the narrower `>=6.0.0 <6.1.0` range

## Package Legitimacy Audit

This phase installs **no new packages** — every dependency listed above already exists in `game-planner-ui/package.json` today and is only being version-bumped. The Package Legitimacy Gate protocol (SLOP/SUS/OK verdicts) is designed to catch hallucinated or slopsquatted *new* package names; it does not meaningfully apply to a same-name version bump of packages already in production use for this app. For completeness, all packages named above were cross-checked directly against the npm registry (`npm view <pkg> version`) in this session and confirmed to exist with active, recent version numbers — none show signs of being abandoned, renamed, or hijacked.

| Package | Registry | Verdict | Disposition |
|---------|----------|---------|-------------|
| `@angular/core`, `@angular/cli`, `@angular-devkit/build-angular`, `zone.js`, `rxjs`, `typescript`, `karma`, `jasmine-core`, `karma-jasmine`, `karma-chrome-launcher`, `karma-jasmine-html-reporter`, `karma-coverage`, `@types/jasmine`, `@types/node` | npm | OK (pre-existing deps, version bump only) | Approved — no `checkpoint:human-verify` needed |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  Developer machine (local dev environment)                       │
│                                                                    │
│  ┌───────────────┐      ┌──────────────────────────────────┐    │
│  │ Node.js        │──▶  │ ng update @angular/core@N          │    │
│  │ (must be       │      │ ng update @angular/cli@N           │    │
│  │  ≥22.15/24.15/ │      │  1. resolves compatible peer deps  │    │
│  │  26.0 by v22)  │      │  2. bumps package.json + lockfile  │    │
│  └───────────────┘      │  3. runs MANDATORY migration        │    │
│                          │     schematics (auto code-mods)     │    │
│                          │  4. prompts OPTIONAL migrations      │    │
│                          │     (decline: standalone, @if/@for, │    │
│                          │      application builder, Vitest)   │    │
│                          └───────────────┬────────────────────┘    │
│                                          │                         │
│                                          ▼                         │
│                          ┌──────────────────────────────────┐     │
│                          │ npm run build (ng build)           │     │
│                          │  → compile error? fix, re-run       │     │
│                          └───────────────┬────────────────────┘     │
│                                          │                         │
│                                          ▼                         │
│                          ┌──────────────────────────────────┐     │
│                          │ npm test (ng test, Karma+Jasmine)  │     │
│                          │  → verify angular.json test        │     │
│                          │    builder still =                 │     │
│                          │    "@angular-devkit/build-angular  │     │
│                          │     :karma"                         │     │
│                          └───────────────┬────────────────────┘     │
│                                          │                         │
│                                          ▼                         │
│                          ┌──────────────────────────────────┐     │
│                          │ ng serve → manual smoke check       │     │
│                          │  (boot only, per D-04 for           │     │
│                          │   intermediate hops)                │     │
│                          └───────────────┬────────────────────┘     │
│                                          │                         │
│                                          ▼                         │
│                          ┌──────────────────────────────────┐     │
│                          │ git commit "chore: Angular N"       │     │
│                          │  (one commit per hop, per D-05)     │     │
│                          └───────────────┬────────────────────┘     │
│                                          │  repeat for next hop     │
│                                          ▼                         │
│                          ┌──────────────────────────────────┐     │
│                          │ FINAL hop only (v22): full manual   │     │
│                          │ walkthrough of every user workflow  │     │
│                          │ (login, backlog CRUD, session       │     │
│                          │ tracking, journal, IGDB/Steam/HLTB  │     │
│                          │ sync views) — success criterion 3   │     │
│                          └──────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────┘
```

### Recommended Project Structure

No structural changes to `game-planner-ui/src/` — this phase is dependency-only. The only files expected to change outside `package.json`/`package-lock.json`:
```
game-planner-ui/
├── angular.json          # verify "test" builder unchanged after each hop; mandatory
│                          #   migrations may touch build-target option names
├── tsconfig.json          # TS compiler target/lib options may be bumped by
│                          #   mandatory migrations at TS-version-jump hops
├── tsconfig.spec.json     # same tsConfig risk as above, spec-side
├── karma.conf.js          # should remain unchanged (uses stock schematic config
│                          #   referencing @angular-devkit/build-angular plugin)
├── src/polyfills.ts        # zone.js import line — verify it still resolves after
│                          #   each zone.js version bump
└── src/main.ts             # bootstrapModule call — unaffected since standalone
                           #   bootstrap is explicitly out of scope (D-03)
```

### Pattern 1: Sequential `ng update` per major version (no skipping)

**What:** Run `ng update @angular/core@<next-major> @angular/cli@<next-major>` once per major version, in strict sequence: 14 → 15 → 16 → 17 → 18 → 19 → 20 → 21 → 22. Never jump two majors in one command.
**When to use:** Every hop in this phase.
**Example:**
```bash
# Hop 1: 13 → 14
ng update @angular/core@14 @angular/cli@14
npm run build
npm test -- --watch=false
ng serve  # manual boot smoke check (D-04)
git add -A && git commit -m "chore(deps): upgrade Angular 13 -> 14"

# Hop 2: 14 → 15 (repeat pattern)
ng update @angular/core@15 @angular/cli@15
# ... same verification sequence ...
```
Source: Angular's official update guide (angular.dev/update-guide) and the versioning policy (angular.dev/reference/releases), both confirm one-major-at-a-time is required — `ng update` will refuse to jump more than one major version past what's currently installed.

### Pattern 2: `--force` for early, larger dependency-tree hops

**What:** Early hops (13→14, 14→15) are more likely to hit `npm` peer-dependency resolution conflicts than later ones, because the dependency tree (RxJS majors historically, `zone.js` betas, etc.) had more churn in that era. If `ng update` reports unresolvable peer conflicts but the target versions are otherwise correct per the table below, `ng update @angular/core@<N> @angular/cli@<N> --force` skips the peer-dependency compatibility check (not the actual install) and still runs the migration schematics.
**When to use:** Only when `ng update` itself refuses to proceed due to peer-dependency version mismatches it can't auto-resolve — not as a default flag on every hop.
**Confidence:** [ASSUMED] — this is standard, widely-documented `ng update` behavior across major versions, but the exact hops where `--force` becomes necessary depend on the installed `npm` version and cannot be predicted with certainty without running the upgrade. Treat as a fallback, not a default.

### Pattern 3: Decline all optional migrations to honor D-03

**What:** At several hops (15, 17, 18, 20, 21) `ng update` will interactively prompt for optional migrations: standalone-component conversion, `@if`/`@for` control-flow syntax conversion, the esbuild/Vite `application` builder switch, and (at 21) the Vitest test-runner switch. Answer "No" / decline every one of these.
**When to use:** Every optional prompt, every hop, per D-03 and D-06.
**Example:**
```bash
# If running non-interactively (CI or scripted), pass no migration flags —
# ng update only auto-applies MANDATORY migrations without a flag.
# Do NOT run, e.g.:
#   ng update @angular/cli --name=use-application-builder      # DECLINE
#   ng generate @angular/core:standalone                        # DECLINE
#   ng generate @angular/core:control-flow                       # DECLINE
#   ng generate @angular/core:karma-to-vitest                     # DECLINE
```
Source: angular.dev migration schematic docs describe these specifically as "optional migrations" run via separate named schematics, distinct from the mandatory migrations bundled automatically into `ng update @angular/core@N`.

### Anti-Patterns to Avoid

- **Hand-editing `package.json` version numbers instead of running `ng update`:** Skips the migration schematics entirely (e.g., the v22 `Default` → `Eager` change-detection-strategy rewrite, or the pending-tasks rename at v19). The build may still succeed but runtime behavior can silently diverge from what `ng update` would have produced — directly risks the "no functional regressions" bar (success criterion 3).
- **Skipping a major version (e.g., 13 → 16 directly):** Angular's CLI is explicitly designed to migrate one major at a time; migration schematics for skipped majors never run, and this is a well-documented source of upgrade failures. D-01 already forbids this.
- **Running `npm install` with `--legacy-peer-deps` or `--force` as a blanket habit to silence every warning:** Masks real incompatibilities (e.g., installing a `karma` version outside the `^6.3.0` range `@angular-devkit/build-angular` expects) rather than resolving them. Reserve force-resolution for genuinely known-safe conflicts (Pattern 2 above).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Migrating deprecated/removed Angular APIs at each major bump | Manual find-and-replace across the codebase | `ng update`'s bundled mandatory migration schematics | These are officially maintained code-mods, tested against the exact API surface removed/changed at each version; manual replacement risks missing edge cases the schematic handles (e.g., the v22 `ChangeDetectionStrategy.Default` → `Eager` rewrite, which is what keeps this upgrade non-breaking per D-03) |
| Determining "what's the current Angular LTS" | A fixed number baked into a plan written today | Re-derive from `angular.dev/reference/releases` at execution time (already done this session: v22 confirmed current stable, in Active support) | Angular ships a new major roughly every 6 months; by the time this phase executes, a number written weeks earlier could already be one version behind |
| Restoring Karma after `ng update` appears to remove it | Migrating to Vitest to "fix" a perceived removal | Verify `@angular-devkit/build-angular` stayed in `devDependencies` and `angular.json`'s `test.builder` is still `@angular-devkit/build-angular:karma` — it works unmodified through v22 (confirmed via npm registry, not blog commentary) | Multiple blog posts claim "Karma removed in Angular 22"; this is misleading — only the *CLI default for new projects* changed. Falling for this claim would trigger an unwanted, out-of-scope test-runner migration |

**Key insight:** Nearly every "gotcha" in a multi-major Angular upgrade is already solved by `ng update`'s own migration schematics, provided they're allowed to run (i.e., the upgrade goes through `ng update`, not manual `package.json` edits) and the *optional* ones are correctly declined rather than accidentally accepted at an interactive prompt.

## Common Pitfalls

### Pitfall 1: Believing "Karma is removed at v22" and unintentionally migrating to Vitest

**What goes wrong:** Widespread 2025-2026 blog commentary states flatly that "Karma support is removed in Angular 22" (some say v21, some v22). A planner or executor trusting this at face value might pre-emptively run `ng generate @angular/core:karma-to-vitest`, converting the test setup and violating D-02/D-06/D-03 for no reason.
**Why it happens:** The CLI *default* for `ng new` did switch to Vitest starting at v21, and `@angular/build` (the new esbuild-native package, distinct from `@angular-devkit/build-angular`) never shipped a Karma plugin. Commentary conflates "new-project default changed" with "old builder removed."
**How to avoid:** Verified directly against the npm registry this session: `@angular-devkit/build-angular@22.0.0` still lists `karma: ^6.3.0` as a peer dependency, and the classic `:karma` builder target still exists in that package. Since this app already uses `@angular-devkit/build-angular` (not `@angular/build`) and already has `"builder": "@angular-devkit/build-angular:karma"` in `angular.json`, no test-runner migration is needed at any hop through v22.
**Warning signs:** `ng update` output mentioning "Vitest" or "application builder" migrations available; any prompt offering to convert `angular.json`'s `test` target — decline it.

### Pitfall 2: `angular.json` build-target option renames at the esbuild/application-builder-related hops (v17, v20, v21)

**What goes wrong:** Even without opting into the new `application` builder, some mandatory migrations at v17+ rename or restructure `angular.json` option keys (e.g., asset/style glob option shapes). If declined optional migrations still leave stale option names behind from a partially-applied earlier state, `ng build`/`ng test` can fail with a confusing schema-validation error rather than a compile error.
**Why it happens:** CLI schema validation is strict, and `angular.json`'s schema itself is versioned alongside `@angular/cli`.
**How to avoid:** After every hop, run `ng build` and `ng test -- --watch=false` immediately — don't batch multiple hops before verifying. Per D-04 this is already the required verification cadence; the CONTEXT.md decision incidentally also catches this exact class of failure early.
**Warning signs:** `ng build`/`ng test` errors referencing "not compatible with the current schema" or unknown option names in `angular.json`.

### Pitfall 3: Node.js version just barely short of the v22 requirement

**What goes wrong:** The installed Node.js is `v24.14.0`. Angular 22's engine range is `^22.22.3 || ^24.15.0 || ^26.0.0` — `24.14.0` does **not** satisfy `^24.15.0` (needs `>=24.15.0 <25.0.0`). This is easy to miss because it looks "close enough."
**Why it happens:** `npm`'s engine check is a warning by default (not a hard failure) unless `--engine-strict` is set, so the mismatch may not surface as an error until something in Angular 22's tooling does its own runtime Node-version assertion (Angular CLI does perform its own stricter checks in recent majors, separate from npm's engines warning).
**How to avoid:** Per D-02, upgrade Node locally before/during the v22 hop. The path of least disruption is bumping within the installed major: `24.14.0 → 24.15.0+` (a Node LTS patch/minor bump), rather than jumping to Node 26.
**Warning signs:** `ng update`/`ng build` at the v22 hop erroring with an unsupported-engine message, or CLI refusing to run.

### Pitfall 4: `paramsInheritanceStrategy` default flip at v22 (router)

**What goes wrong:** Angular 22 changes the Router's default `paramsInheritanceStrategy` from `'emptyOnly'` to `'always'`, with **no automatic migration provided** — child routes now inherit params/data/resolved-values from every ancestor route by default. Any code relying on a child route *not* seeing a parent's route data could silently start receiving unexpected values.
**Why it happens:** This is an intentional, documented breaking change in Angular 22's router (confirmed via Angular 22 release commentary), shipped without a code-mod because the "fix" (adding `paramsInheritanceStrategy: 'emptyOnly'` to router config) is a value judgment, not a mechanical rewrite.
**How to avoid:** Verified this session by reading `game-planner-ui/src/app/app-routing.module.ts` directly: every route in this app is a flat, top-level route (`/games`, `/games/:id`, `/planner`, `/journal`, etc.) — there are no nested/child routes with shared `RouterModule.forChild` configs, so no route in this app can currently inherit a parent's params/data at all. This pitfall's practical risk for this specific app is therefore **low**, but the final-hop manual walkthrough (success criterion 3) should still specifically exercise the `/games/:id` route (the one parameterized route) to confirm `ActivatedRoute.params`/`snapshot.params` still resolve `id` correctly.
**Warning signs:** `GameDetailComponent` failing to read the `:id` route param after the v22 hop (would indicate an unrelated regression, since this app has no route nesting for the strategy change to affect).

### Pitfall 5: TypeScript major-version jump at the final hop (5.9.x → 6.0.x)

**What goes wrong:** Angular 22 requires TypeScript `>=6.0.0 <6.1.0` — TypeScript 6.0 is a new major version relative to every prior hop in this sequence (which stay within TS 4.x/5.x). New TS majors can introduce stricter type-checking that surfaces previously-silent type errors in application code, even with no framework-level code changes.
**Why it happens:** TypeScript itself ships breaking changes on its own major-version cadence, independent of Angular; Angular simply requires the app to track along.
**How to avoid:** Treat the v21→v22 hop's `npm run build` as the point most likely to surface new TS compile errors unrelated to Angular itself. Budget extra time for this specific hop; don't assume a clean build at v21 guarantees a clean build at v22.
**Warning signs:** New TS2xxx-series compiler errors appearing only at the final hop, in files that didn't change.

### Pitfall 6: `ng update`'s peer-dependency resolver refusing early hops (13→14, 14→15) without `--force`

**What goes wrong:** `npm`'s stricter peer-dependency resolution (since npm 7+) can cause `ng update` to report unresolvable peer conflicts even when the target Angular versions are mutually compatible, particularly on the earliest hops where the installed `package-lock.json` still reflects Angular 13-era dependency shapes.
**Why it happens:** `ng update` internally uses `npm`'s (or the detected package manager's) dependency resolution; transitional states between two mutually-incompatible-looking but actually-fine version ranges can trip this.
**How to avoid:** If `ng update @angular/core@14 @angular/cli@14` fails with peer-dependency errors (not compile errors), retry with `--force`. This is documented, standard Angular CLI guidance for major upgrades — see Architecture Patterns, Pattern 2.
**Warning signs:** `ng update` exits with `npm ERR! ERESOLVE` or a peer-dependency conflict message before running any migration schematics.

## Code Examples

### Full hop-by-hop version and command matrix

```
Hop | From → To | ng update command                          | Node.js required            | TypeScript required   | zone.js required        | RxJS required (unchanged)
----|-----------|---------------------------------------------|------------------------------|------------------------|--------------------------|---------------------------
 1  | 13 → 14   | ng update @angular/core@14 @angular/cli@14   | ^14.15.0 || ^16.10.0          | >=4.6.2 <4.9.0          | ~0.11.4                  | ^6.5.3 || ^7.4.0
 2  | 14 → 15   | ng update @angular/core@15 @angular/cli@15   | ^14.20.0 || ^16.13.0 || ^18.10.0 | >=4.8.2 <5.0.0        | ~0.11.4 || ~0.12.0       | ^6.5.3 || ^7.4.0
 3  | 15 → 16   | ng update @angular/core@16 @angular/cli@16   | ^16.14.0 || ^18.10.0          | >=4.9.3 <5.2.0          | ~0.13.0                  | ^6.5.3 || ^7.4.0
 4  | 16 → 17   | ng update @angular/core@17 @angular/cli@17   | ^18.13.0 || ^20.9.0           | >=5.2.0 <5.5.0          | ~0.14.0                  | ^6.5.3 || ^7.4.0
 5  | 17 → 18   | ng update @angular/core@18 @angular/cli@18   | ^18.19.1 || ^20.11.1 || ^22.0.0 | >=5.4.0 <5.6.0        | ~0.14.0                  | ^6.5.3 || ^7.4.0
 6  | 18 → 19   | ng update @angular/core@19 @angular/cli@19   | ^18.19.1 || ^20.11.1 || ^22.0.0 | >=5.5.0 <5.7.0        | ~0.15.0                  | ^6.5.3 || ^7.4.0
 7  | 19 → 20   | ng update @angular/core@20 @angular/cli@20   | ^20.19.0 || ^22.12.0 || ^24.0.0 | >=5.8.0 <5.9.0        | ~0.15.0                  | ^6.5.3 || ^7.4.0
 8  | 20 → 21   | ng update @angular/core@21 @angular/cli@21   | ^20.19.0 || ^22.12.0 || ^24.0.0 | >=5.9.0 <6.0.0        | ~0.15.0                  | ^6.5.3 || ^7.4.0
 9  | 21 → 22   | ng update @angular/core@22 @angular/cli@22   | ^22.22.3 || ^24.15.0 || ^26.0.0 | >=6.0.0 <6.1.0        | ~0.15.0 || ~0.16.0       | ^6.5.3 || ^7.4.0
```
Source: [VERIFIED: npm registry] — `npm view @angular/core@<version> peerDependencies` and `npm view @angular-devkit/build-angular@<version> peerDependencies`, run directly against the live npm registry this session (2026-08-28). Node.js ranges cross-checked against [CITED: angular.dev/reference/versions].

### Verifying the Karma builder survived a hop

```bash
# Run after every hop that touches build tooling (all of them, but especially 17/20/21):
node -e "const j=require('./angular.json'); \
  const p=j.projects[Object.keys(j.projects)[0]]; \
  console.log(p.architect.test.builder)"
# Expected output, unchanged through every hop:
#   @angular-devkit/build-angular:karma
```

### Notable *mandatory* migrations to expect (no action needed beyond letting them run)

```
v19: PendingTasks rename (ExperimentalPendingTasks -> PendingTasks) — auto-migrated
v22: ChangeDetectionStrategy.Default -> ChangeDetectionStrategy.Eager rewrite for any
     component with no explicit strategy or explicit Default — auto-migrated by
     ng update; this is what keeps runtime change-detection behavior identical
     despite OnPush becoming the default for newly *generated* components
     (this app is not generating new components in this phase, so this migration
     is largely a no-op safety net, not an active concern)
```
Source: [CITED: multiple Angular v19/v22 release-note summaries, cross-checked against the official Angular v22.0.0 GitHub release] — WebSearch-sourced commentary, not a raw angular.dev fetch of the changelog (that fetch failed to return usable content this session); treat the *existence* of these two migrations as MEDIUM confidence, corroborated by multiple independent sources, but the exact mechanics are not independently re-verified against Angular's source.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| Karma + Jasmine as the only in-box unit test runner | Vitest as the CLI default for `ng new`, Karma/Jasmine still fully supported as an explicit opt-in | v21 (Vitest became the `ng new` default) | No impact on this phase — this app keeps Karma/Jasmine per D-02/D-06; only relevant if the app scaffolds *new* projects, which it doesn't |
| Webpack-based `@angular-devkit/build-angular:browser`/`:karma` builders | esbuild/Vite-based `application`/`unit-test` builders (`@angular/build`) | v17 (optional), progressively defaulted for new projects through v21 | No impact on this phase — the migration to the new builder family is optional and explicitly declined per D-03 |
| `ChangeDetectionStrategy.Default` as the unnamed default for components with no explicit strategy | `ChangeDetectionStrategy.Eager` (renamed, same behavior) is the new name for that same "check everything" mode; `OnPush` is the default only for newly-*generated* components | v21.2 (deprecation) / v22 (rename + new-component default) | Auto-migrated by `ng update`; no behavior change for this app's existing components |
| Router `paramsInheritanceStrategy: 'emptyOnly'` (default) | `paramsInheritanceStrategy: 'always'` (default) | v22 | No automatic migration; low risk for this app specifically since it has no nested routes (confirmed by reading `app-routing.module.ts`) |
| Zone.js-based change detection as the only option | `provideZonelessChangeDetection()` (stable since v20.2), default for *new* projects since v21 | v18 (developer preview) → v20.2 (stable) → v21 (new-project default) | No impact — D-06 explicitly keeps zone.js; existing (non-`ng new`) projects are never auto-migrated to zoneless |

**Deprecated/outdated:**
- Angular versions 2 through 19 are all EOL as of this research date — this app's starting point (13.3.0) has been unsupported/unpatched since well before this milestone began, which is the entire premise of Phase 1.
- HammerJS integration is deprecated (flagged for removal in a future major beyond 21) — not used by this app (no gesture-based UI), no action needed.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `jasmine-core`, `karma-jasmine`, `karma-chrome-launcher`, `karma-jasmine-html-reporter`, `karma-coverage`, `@types/jasmine` latest versions (7.0.2 / 5.1.0 / 3.2.0 / 2.3.0 / 2.2.1 / 6.0.0 respectively) are safe to bump to directly, in one step, at the end of the sequence | Standard Stack (Supporting) | If a version has a genuine incompatibility with Karma 6.x or Jasmine 7.x internals not caught by this research, `npm test` could fail after the final hop; low blast radius since there are zero `.spec.ts` files to actually break — worst case is a boot failure, immediately visible and fixable by pinning back one version |
| A2 | Angular's `--force` flag is the correct remedy for peer-dependency resolver failures on the earliest hops (13→14, 14→15) | Architecture Patterns, Pattern 2 / Pitfall 6 | If `--force` masks a genuine incompatibility rather than a resolver false-positive, the migration schematics could still run against an inconsistent dependency tree; mitigated by the D-04 requirement to build+smoke-check after every hop, which would surface this immediately |
| A3 | The mandatory `ChangeDetectionStrategy.Default → Eager` and `PendingTasks` rename migrations exist and run automatically as described | Code Examples | These were corroborated by multiple independent WebSearch sources (not a raw angular.dev changelog fetch, which failed this session) — if inaccurate, worst case is `ng update` behaving slightly differently than described here, not a missed manual step, since these are framed as "no action needed" items |
| A4 | Angular 22 is the correct reading of D-01's "current active LTS support" (vs. stopping at v21, which is in LTS proper while v22 is in Active support) | Summary | If the user's intent was strictly "a version already in the LTS phase," v21 would be the more literal target; risk is low either way since the plan should re-confirm this exact point with the user/executor at execution time given D-01 explicitly says "re-confirm... at execution time" |

## Open Questions

1. **Is Angular 22 or Angular 21 the correct target given D-01's exact wording?**
   - What we know: v22 is the latest stable release, in "Active" support (not yet "LTS" in Angular's own terminology) with support through mid-2028; v21 is in "LTS" support proper (critical fixes only) through mid-2027; v20 is also in "LTS" through late 2026.
   - What's unclear: D-01 says "whatever Angular version is under active LTS support" — this phrase blends "active support" and "LTS" which are two distinct phases in Angular's own release model. Read literally as "LTS," v21 wins; read as "maximizes the security-patch runway" (the stated rationale), v22 wins decisively (longest total window, and is the version that will *become* LTS at the end of its Active phase rather than already being partway through LTS).
   - Recommendation: Target v22, per the "maximize the security-patch runway" rationale explicitly stated in D-01, and re-confirm this specific number against `angular.dev/reference/releases` immediately before starting execution (versions and dates shift roughly every 6 months) — D-01 itself anticipates this re-confirmation step.

2. **Will `ng update`'s peer-dependency resolution actually require `--force` on any specific hop, and if so which?**
   - What we know: This is documented, common guidance for large major-version jumps in general, and the resolver is stricter with a fresh Angular-13-era `package-lock.json`.
   - What's unclear: The exact hop(s) where this becomes necessary depends on the installed npm version at execution time and cannot be determined without actually running the sequence.
   - Recommendation: Plan tasks should include "if `ng update` reports peer-dependency conflicts, retry with `--force`" as a conditional fallback step on every hop, not a guaranteed step on any specific one.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Node.js | All hops; hard requirement `^22.22.3 \|\| ^24.15.0 \|\| ^26.0.0` for the final v22 hop | ✓ (partial — below v22's minimum) | v24.14.0 installed | Per D-02, upgrade Node locally to at least v24.15.0 before/during the v22 hop; nvm-windows or a direct installer bump is sufficient — no code-level fallback needed since this is purely a local dev-machine tooling change |
| npm | Package installation and `ng update` execution | ✓ (implicit — Node install includes npm) | not independently checked this session | — |
| Angular CLI (global or npx) | Running `ng update`/`ng build`/`ng test`/`ng serve` | ✓ via project-local `@angular/cli` in `devDependencies` | 13.3.1 currently; bumped per hop | Use `npx ng ...` or the project's `npm run` scripts rather than a global CLI install, to avoid a global/local version mismatch during the multi-hop upgrade |

**Missing dependencies with no fallback:**
- Node.js patch bump (v24.14.0 → v24.15.0+) is required before the final hop completes successfully; there is no way to run Angular 22 on v24.14.0. This is a local dev-machine change only (per D-02's reversibility note), not a blocker requiring a decision — just an action item in the plan.

**Missing dependencies with fallback:**
- None — the Node.js gap above has no viable fallback (Angular 22's engine check is a hard requirement, not a soft warning, in recent CLI majors); it must simply be resolved.

## Security Domain

`security_enforcement` is on (ASVS Level 1, block on `high`) project-wide, but this phase makes **no functional or security-relevant code changes** — it is a dependency-version bump with mandatory migrations only (per D-03), isolated from the backend (per CONTEXT.md), with no new auth flows, input handling, or cryptography introduced. None of the ASVS categories below are newly implicated by this phase; they are listed for completeness and to record that none apply here.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-------------------|
| V2 Authentication | No | Unchanged — `AuthGuard`/JWT storage in `localStorage` is untouched by this phase; any hardening of this is Phase 3's scope |
| V3 Session Management | No | Unchanged — no session-handling code is touched |
| V4 Access Control | No | Unchanged — `AuthGuard` route protection logic is not modified |
| V5 Input Validation | No | No new input-handling code is introduced by a framework version bump |
| V6 Cryptography | No | No cryptographic code exists in the frontend; not applicable |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|----------------------|
| Running a framework version with known, unpatched CVEs (the pre-upgrade state) | Tampering / Elevation of Privilege (via known Angular-core or transitive-dependency vulnerabilities) | This phase itself *is* the mitigation — moving off Angular 13.3.0 (unpatched since ~April 2023 per `.planning/codebase/CONCERNS.md`) onto a currently-supported major closes this exposure window. No further action needed within this phase's scope. |
| `npm audit` findings introduced transitively by the version bump itself | Tampering (supply-chain) | Run `npm audit` after the final hop as part of the phase's build verification step; this is a lightweight, standard check appropriate to add to the plan's verification list even though it wasn't explicitly named in the phase's success criteria |

## Sources

### Primary (HIGH confidence)
- npm registry, queried directly this session (`npm view @angular/core@<N> peerDependencies`, `npm view @angular-devkit/build-angular@<N> peerDependencies`, `npm view zone.js/rxjs/karma/jasmine-core/... version`) — versions and peer-dependency ranges for Angular 13 through 22, zone.js, RxJS, TypeScript, and the karma/build-angular relationship at v22
- `angular.dev/reference/releases` — support-phase status (Active/LTS/EOL) and dates for Angular 20/21/22 as of 2026-08-28
- `angular.dev/reference/versions` — Node.js/TypeScript compatibility table for Angular 20 through 22
- Direct repository reads: `game-planner-ui/package.json`, `game-planner-ui/angular.json`, `game-planner-ui/karma.conf.js`, `game-planner-ui/src/app/app-routing.module.ts` — confirmed current dependency versions, confirmed the test builder is `@angular-devkit/build-angular:karma`, confirmed no nested/child routes exist in this app

### Secondary (MEDIUM confidence)
- Multiple independent WebSearch results (cross-checked across 3+ sources each) on: Angular 14-19 breaking changes and RxJS peer-range stability, Karma deprecation timeline (v16 deprecation announcement, v20 default removal, v21 Vitest default), the `ChangeDetectionStrategy.Eager` rename and auto-migration at v22, the `paramsInheritanceStrategy` default change at v22 with no auto-migration, the optional `use-application-builder` migration mechanics

### Tertiary (LOW confidence)
- Specific version numbers for `jasmine-core`/`karma-jasmine`/`karma-chrome-launcher`/`karma-jasmine-html-reporter`/`karma-coverage`/`@types/jasmine` "latest" pulls — confirmed to exist on the npm registry but their mutual compatibility was not cross-checked against a dedicated compatibility matrix (none publicly exists, since these are independently versioned, non-Angular-peer packages) — flagged in the Assumptions Log (A1)

## Metadata

**Confidence breakdown:**
- Standard stack (Angular/CLI/zone.js/RxJS/TypeScript version matrix): HIGH — every number directly queried from the live npm registry this session, cross-checked against angular.dev
- Architecture (upgrade sequencing, karma-builder survival): HIGH — the "Karma still works at v22" finding is directly verified against the npm registry, correcting several less-precise secondary sources
- Pitfalls: MEDIUM-HIGH — the Node.js version gap, karma-builder-preservation, and no-nested-routes findings are directly verified against this repo/registry; the exact hop(s) needing `--force` and the precise mechanics of the v19/v22 mandatory migrations are MEDIUM (cross-checked secondary sources, not raw source/changelog verified)

**Research date:** 2026-08-28
**Valid until:** ~14 days — Angular's release cadence and the "current LTS" determination in particular are time-sensitive; re-confirm the target version number against `angular.dev/reference/releases` immediately before execution if more than 2 weeks have elapsed since this research
