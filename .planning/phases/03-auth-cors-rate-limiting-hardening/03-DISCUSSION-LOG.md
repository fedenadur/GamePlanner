# Phase 3: Auth, CORS & Rate Limiting Hardening - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-31
**Phase:** 3-Auth, CORS & Rate Limiting Hardening
**Areas discussed:** JWT lifetime & re-login UX, CORS lockdown scope, Rate limit scope & thresholds, Security event logging mechanism

---

## Todo Fold Decision

| Option | Description | Selected |
|--------|-------------|----------|
| Leave as pending todo | Doesn't fit this phase's domain; keep tracked separately | |
| Fold into Phase 3 | Include the SessionManager UserId-filter fix as part of this phase | ✓ |

**User's choice:** Fold into Phase 3.
**Notes:** The todo (`SessionManager.cs:15` cross-user leak) scored only 0.6 on keyword match and was previously reviewed-and-rejected in Phase 2's discussion for the same reason (not a credentials/secrets issue). This time the user chose to include it despite the domain mismatch.

---

## JWT lifetime & re-login UX

| Option | Description | Selected |
|--------|-------------|----------|
| 24 hours | One re-login per day at most; bounds stolen-token blast radius vs. 7 days | ✓ |
| 4 hours | Tighter security, more frequent re-logins | |
| 1 hour | Aggressive, matches short-lived-token best practice | |

**User's choice:** 24 hours.

| Option | Description | Selected |
|--------|-------------|----------|
| Leave as-is | No frontend UX change; silent redirect to /login stays | ✓ |
| Add a "session expired" message | Small frontend addition explaining the redirect | |

**User's choice:** Leave as-is.

| Option | Description | Selected |
|--------|-------------|----------|
| Keep default (5 min) | Standard practice, absorbs clock drift | ✓ |
| Tighten to near-zero | Stricter enforcement, risk of false rejections | |

**User's choice:** Keep default (5 min).

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, pin both (ValidAlgorithms=HmacSha256, RequireExpirationTime=true) | Cheap, explicit hardening satisfying AUTH-02 | ✓ |
| No, current config is enough | Skip the extra explicit pins | |

**User's choice:** Yes, pin both.

---

## CORS lockdown scope

| Option | Description | Selected |
|--------|-------------|----------|
| Single App:FrontendUrl | Matches existing config key's name/intent exactly | ✓ |
| Comma-separated list support | More flexible for multiple future origins | |

**User's choice:** Single App:FrontendUrl.

| Option | Description | Selected |
|--------|-------------|----------|
| Authorization + Content-Type only | Matches exactly what the frontend sends, confirmed by scan | ✓ |
| You decide | Claude picks minimal header set during implementation | |

**User's choice:** Authorization + Content-Type only.

| Option | Description | Selected |
|--------|-------------|----------|
| GET, POST, PUT, DELETE only | Matches actual frontend usage, confirmed by scan | ✓ |
| You decide | Claude confirms exact method list during implementation | |

**User's choice:** GET, POST, PUT, DELETE only.

| Option | Description | Selected |
|--------|-------------|----------|
| Manual browser walkthrough | Matches ROADMAP.md success criterion 4 exactly | ✓ |
| Scripted preflight check only | Faster to automate but deviates from stated criterion | |

**User's choice:** Manual browser walkthrough.

---

## Rate limit scope & thresholds

| Option | Description | Selected |
|--------|-------------|----------|
| Games + Journal + Session only | Matches NET-03's literal wording exactly | ✓ |
| All data-mutation endpoints | More complete but exceeds NET-03's stated scope | |

**User's choice:** Games + Journal + Session only.

| Option | Description | Selected |
|--------|-------------|----------|
| 100 requests / hour per user | Generous for normal use, matches CONCERNS.md example | ✓ |
| 30 requests / minute per user | Shorter window, tighter burst allowance | |
| You decide | Claude picks a number during implementation | |

**User's choice:** 100 requests / hour per user.

**Note:** The partition-key question ("partition by JWT user ID") was presented to the user as a stated decision rather than a genuine multi-option choice — ROADMAP.md's success criterion 5 already requires per-user partitioning, leaving no other viable option. It was not re-asked as an AskUserQuestion after the tool rejected a single-option question.

| Option | Description | Selected |
|--------|-------------|----------|
| One shared policy | Simpler, matches existing single "auth" policy pattern | ✓ |
| Separate policy per controller | More granular, no evidence it's needed yet | |

**User's choice:** One shared policy.

---

## Security event logging mechanism

| Option | Description | Selected |
|--------|-------------|----------|
| Structured ILogger output only | Matches existing project convention, no new infra | ✓ |
| You decide | Claude picks the exact mechanism during implementation | |

**User's choice:** Structured ILogger output only.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, LogWarning with email + reason | Matches structured-logging convention, never logs password | ✓ |
| Yes, but omit the email | More conservative, harder to spot targeted brute-force | |

**User's choice:** Yes, LogWarning with email + reason.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, log via OnRejected | Standard ASP.NET Core mechanism, satisfies OBS-01 directly | ✓ |
| You decide | Claude wires up the standard pattern during implementation | |

**User's choice:** Yes, log via OnRejected.

| Option | Description | Selected |
|--------|-------------|----------|
| Log at CORS policy level via custom OnRejected-style check | ASP.NET Core has no native CORS rejection hook; flagged as needing research during planning | ✓ |
| Skip — log CORS config only, not live rejections | Narrower scope, avoids custom middleware uncertain to work well | |

**User's choice:** Log at CORS policy level via custom check (flagged for research — see CONTEXT.md D-16).

---

## Claude's Discretion

- Exact wording/structured-parameter names of new log statements (failed login, rate-limit rejection) — follow existing `ILogger<T>` + named-parameter conventions used in `IgdbService`/`SteamService`.
- Concrete implementation mechanism for CORS rejection logging — left to research/planning to resolve.

## Deferred Ideas

None — all discussed items were either resolved as decisions or folded into scope. The one out-of-domain item raised (session-planner cross-user leak) was folded into Phase 3 rather than deferred.
