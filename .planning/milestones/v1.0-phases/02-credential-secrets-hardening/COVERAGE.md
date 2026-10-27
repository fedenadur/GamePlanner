# API Coverage — Phase 02: Credential & Secrets Hardening

No external API integration: this phase changes **how existing IGDB/Steam credentials are transported, stored, and error-handled** — it adds no new capability, endpoint, or verb to either provider's surface.

## Detector result and confirmation

The deterministic detector returned `detected: true` on a single weak signal:

```
{"verb":"(surface)","noun":"api","snippet":"\"The Steam API key never appears in application logs or unhandled exception output, confirmed by triggering a Steam API "}
```

That match is the phrase **"Steam API key"** in ROADMAP success criterion 2 — the word `api` inside a credential-handling sentence, not an integration verb.

Per the checkpoint's instruction ("confirm by re-reading the phase scope, not by preference"), the phase scope was re-read in full (`ROADMAP.md` Phase 2 section, `02-CONTEXT.md`, `02-RESEARCH.md`, `02-PATTERNS.md`). The complete set of provider-touching changes is:

| Change | Provider surface effect |
|--------|-------------------------|
| CRED-01 — `IgdbService.GetTokenAsync` moves credentials from URL query string to a form-encoded POST body | Same endpoint (`id.twitch.tv/oauth2/token`), same grant, same response. Transport shape only. |
| CRED-02 — `try/catch` with redacted logging around the 3 existing `SteamService` HTTP calls | Same three endpoints already integrated (`ResolveVanityURL`, `GetPlayerSummaries`, `GetOwnedGames`). No new call. |
| CRED-03/04 — secrets move to `dotnet user-secrets` / env vars, then rotate | Configuration source and credential value only. No API call changes. |
| CRED-05 — gitleaks pre-commit hook | Dev tooling, outside the app's runtime tiers entirely. |
| REL-03 — explicit failure handling + `WorkerStatus` partial-failure surfacing | Internal error handling and an existing internal endpoint (`GET /api/worker/status`). No external API. |

Every IGDB and Steam capability this app calls was already integrated before Phase 02 and is still called identically after it. There is no capability whose `INTEGRATE` / `OPT-OUT` status this phase decides, so fabricating a matrix row would record a decision that was never made — the exact failure mode the checkpoint exists to prevent.

A future phase that adds a *new* IGDB or Steam capability must produce a full coverage matrix from the full-coverage baseline; this declaration does not carry forward.
