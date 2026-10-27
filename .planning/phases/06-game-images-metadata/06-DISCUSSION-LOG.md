# Phase 6: Game Images & Metadata - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md. This log keeps the alternatives that were considered.

**Date:** 2026-10-27
**Phase:** 6-game-images-metadata
**Areas discussed:** Cover freshness fix, Portal fix scope, Lightbox finishing, Verification depth

---

## Cover freshness fix

| Option | Description | Selected |
|--------|-------------|----------|
| Drop it | Batch only selects NULL covers; remove the skip | ✓ |
| Guard by has-cover | Keep the skip but only when a cover exists | |

| Option | Description | Selected |
|--------|-------------|----------|
| Fix now | Id-cursor paging so each row is tried once per run | ✓ |
| Defer as todo | Log and leave | |

| Option | Description | Selected |
|--------|-------------|----------|
| App fix + one run | No data script; one non-force run fills Half-Life covers | ✓ |
| Also run the todo's UPDATE | Clear IGDBUpdatedAt on both rows | |

---

## Portal fix scope

| Question | Options | Selected |
|----------|---------|----------|
| IGDBId lookup | Claude looks up, you confirm / You provide the ID | Claude looks up, you confirm |
| Script effect | Mirror script 38 / IGDBId + cover only | Mirror script 38 |
| Steam-import matcher | Defer / Include | Include |
| Wider scan | Report only / No scan / Scan and fix all | Report only |
| Matcher strategy | SteamAppId first, then name / Better name ranking only / SteamAppId only | SteamAppId first, then name |
| No confident match | Fall back to first hit / Leave unmatched | Fall back to first hit |
| Call sites | Sync path only / All name searches | Sync path only |
| Existing rows | New matches only / Re-match suspects | New matches only |

**Notes:** The owner chose to include the matcher improvement, which went against the "Defer" recommendation.

---

## Lightbox finishing

| Question | Options | Selected |
|----------|---------|----------|
| Escape closes | Yes / No | Yes |
| Image set | Screenshots only / Screenshots + artworks | Screenshots only |
| Fade | Keep as-is / Fade on image load / You decide | Keep as-is |

---

## Verification depth

| Question | Options | Selected |
|----------|---------|----------|
| xUnit (multi) | Shared-IGDBId images / NULL-cover fetch / Matcher | All three |
| Lightbox UAT | Add to Playwright UAT / One-off Playwright MCP check | Add to Playwright UAT |
| Data checks | Live run + SQL / You check by hand | Live run + SQL |

---

## Claude's Discretion

- SQL heuristics for the suspect scan
- Shape of the IGDB external-games lookup method

## Deferred Ideas

- Improved matcher in `GameEnrichmentManager` and the cover name search
- Re-matching scan suspects
- Artworks in the carousel or lightbox
- Touch/swipe lightbox navigation
