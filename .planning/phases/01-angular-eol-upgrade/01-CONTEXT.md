# Phase 1: Angular EOL Upgrade - Context

**Gathered:** 2026-08-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Move the Angular SPA from the unpatched 13.3.0 to the current LTS release, hopping through every intermediate major version sequentially (no version skipping), with TypeScript/RxJS/zone.js/Jasmine/Karma kept at versions compatible with each step. No functional regressions — every existing user workflow must behave identically afterward. This is a dependency-currency fix, not a feature or architecture change; the backend is untouched in this phase.

</domain>

<decisions>
## Implementation Decisions

### Target version
- **D-01:** Upgrade all the way to whatever Angular version is under active LTS support at execution time (not a fixed stop like v18) — re-confirm the exact current LTS number via angular.dev when the phase is executed. Maximizes the security-patch runway, which is the point of this milestone.
- **D-02:** If a hop requires a newer Node.js than what's installed, upgrade Node locally as part of this phase rather than stopping to ask. — **Reversibility:** reversible — local dev-machine tooling change, no shared/deployed impact.

### Modernization scope
- **D-03:** Pure dependency bump only. Keep NgModules and `*ngIf`/`*ngFor` structural directives as-is; do not opt into `ng update`'s optional migrations to standalone components or the new `@if`/`@for` control-flow syntax. Only accept migrations `ng update` applies automatically/mandatorily (e.g. codemods for removed APIs). Minimizes diff and risk to match the "no functional regressions" bar for a hardening milestone. — **Reversibility:** reversible — nothing here is a one-way architectural commitment; a later phase can adopt standalone/new syntax independently.

### Per-hop verification & commits
- **D-04:** After each major-version hop (13→14→15→...), run a build + boot smoke check before moving to the next hop. Reserve the full manual walkthrough of every user workflow (per ROADMAP.md Phase 1 success criterion 3) for the final target version.
- **D-05:** One git commit per successful hop, not one commit for the whole phase — preserves bisectability if a regression surfaces later that traces back to a specific hop. This matches the existing project decision (STATE.md) that Phase 1 is isolated for bisectability.

### Zoneless change detection
- **D-06:** Keep zone.js — do not adopt Angular's (experimental) zoneless change detection in this phase. Just bump zone.js to the version matching the target Angular release. Adopting zoneless would touch app-wide change-detection behavior, contradicting the "pure dependency bump" / "no functional regressions" decisions above. — **Reversibility:** reversible — zoneless is an opt-in mode; staying on zone.js now doesn't block adopting it later.

### Claude's Discretion
None — all four discussed areas resolved to explicit user decisions above.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & requirements
- `.planning/ROADMAP.md` — Phase 1 section: goal, success criteria, DEP-01/DEP-02 requirements
- `.planning/REQUIREMENTS.md` — DEP-01 (sequential Angular upgrade, no version skipping), DEP-02 (related frontend deps: TypeScript, RxJS, zone.js, Jasmine, Karma)

### Codebase state
- `.planning/codebase/STACK.md` — current frontend dependency versions (Angular 13.3.0, TypeScript 4.6.2, RxJS 7.5.0, zone.js 0.11.4, Jasmine 4.0.0, Karma 6.3.0)
- `.planning/codebase/CONCERNS.md` §"Angular 13.3.0 — End of Life" — original EOL finding and prior (now superseded) suggestion to target v18; §"Fix Approach" — `ng update @angular/cli @angular/core` as the mechanism
- `.planning/codebase/TESTING.md` — confirms zero existing frontend tests (Jasmine/Karma configured but unused, `skipTests: true` in `angular.json`) — the "manual walkthrough" verification approach exists because there's no automated test suite to lean on yet

### Project state
- `.planning/STATE.md` — records the prior decision that Phase 1 is isolated against the unchanged backend specifically for bisectability, and flags that the exact target LTS version needs re-confirmation via angular.dev at execution time

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None applicable — this is a dependency-version phase, not new feature code.

### Established Patterns
- No Angular Material or other major UI library dependency in `game-planner-ui/package.json` — reduces breaking-change surface area for this upgrade (no third-party Angular library version compatibility to chase).
- `angular.json` has `skipTests: true` for component/service schematics — no `.spec.ts` files exist anywhere in the frontend, so there is no test suite to run per-hop; verification relies on build success + manual walkthrough as decided above.

### Integration Points
- None — Phase 1 is explicitly isolated from the backend (`GamePlanner.API`); no coordination needed with API changes in this phase.

</code_context>

<specifics>
## Specific Ideas

No specific reference implementations mentioned. The user's guidance was entirely about process/scope for the upgrade: go to latest LTS, minimize diff (no architecture modernization, no zoneless), smoke-check each hop, commit per hop.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. Standalone components and `@if`/`@for` control-flow migration were explicitly discussed and deferred as out-of-scope for this phase (see D-03), not lost — a future phase could pick them up if desired.

</deferred>

---

*Phase: 1-Angular EOL Upgrade*
*Context gathered: 2026-08-28*
