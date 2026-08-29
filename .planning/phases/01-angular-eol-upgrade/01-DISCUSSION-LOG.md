# Phase 1: Angular EOL Upgrade - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-28
**Phase:** 1-Angular EOL Upgrade
**Areas discussed:** Target version & stop point, Modernization scope, Per-hop verification & commits, Zoneless change detection

---

## Target version & stop point

| Option | Description | Selected |
|--------|-------------|----------|
| Latest current LTS | Go all the way to whatever Angular version is under active LTS support as of execution time. Maximizes security-patch runway. | ✓ |
| Fixed target: Angular 18 | Stop at v18 as originally suggested in CONCERNS.md. More conservative, but may already be behind current LTS. | |
| You decide at execution time | Defer exact stopping point to execution-time research. | |

**User's choice:** Latest current LTS
**Notes:** Re-confirm the exact current LTS number via angular.dev at execution time.

**Follow-up: hop pacing**

| Option | Description | Selected |
|--------|-------------|----------|
| Smoke check every hop | Build + boot briefly after each major-version hop before moving on. | ✓ |
| Run through, verify only at the end | Apply all hops back-to-back, build/test once at the end. | |

**User's choice:** Smoke check every hop

**Follow-up: Node.js version blocking a hop**

| Option | Description | Selected |
|--------|-------------|----------|
| Upgrade Node as needed | Treat Node.js as in-scope; upgrade locally when a hop requires it. | ✓ |
| Stop and ask | Pause the phase and flag it back to the user before proceeding. | |

**User's choice:** Upgrade Node as needed

---

## Modernization scope

| Option | Description | Selected |
|--------|-------------|----------|
| Pure dependency bump | Keep NgModules and `*ngIf`/`*ngFor` as-is; only accept mandatory ng update codemods. | ✓ |
| Opportunistic modernization | Accept optional migrations (standalone components, `@if`/`@for`) when offered. | |
| You decide per-migration | Let Claude judge case-by-case at execution time. | |

**User's choice:** Pure dependency bump
**Notes:** Fits the hardening milestone's "no functional regressions" bar — minimizes diff and risk.

---

## Per-hop verification & commits

| Option | Description | Selected |
|--------|-------------|----------|
| One commit per hop | Commit after each successful hop for bisectability. | ✓ |
| One commit for the whole phase | Single commit once fully upgraded and verified. | |

**User's choice:** One commit per hop
**Notes:** Matches the project's existing stated bisectability goal for Phase 1 (STATE.md).

---

## Zoneless change detection

| Option | Description | Selected |
|--------|-------------|----------|
| Keep zone.js | Bump zone.js to match target Angular; don't change change-detection model. | ✓ |
| Adopt zoneless | Drop zone.js and switch to zoneless change detection. | |

**User's choice:** Keep zone.js
**Notes:** Adopting zoneless would contradict the "pure dependency bump" / "no functional regressions" decisions made earlier in the same discussion.

---

## Claude's Discretion

None — every discussed area resolved to an explicit user decision.

## Deferred Ideas

None. Standalone components and `@if`/`@for` migration were considered and explicitly deferred (not adopted in this phase) rather than left ambiguous — see "Modernization scope" above.
