---
phase: quick-261027-qhu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - GamePlanner.API/Controllers/WorkerController.cs
  - e2e/tests/uat.spec.ts
autonomous: true
requirements: [quick-261027-qhu]

must_haves:
  truths:
    - "Clicking Run now for SteamPlaytimeSyncWorker on the Workers page no longer returns 404; POST api/worker/SteamPlaytimeSyncWorker/run returns 202 Accepted with message 'SteamPlaytimeSyncWorker triggered.'"
    - "The triggered request actually runs SteamPlaytimeSyncWorker.RunAsync in the background (same fire-and-forget path as the other three workers), so the WorkerStatus row for SteamPlaytimeSyncWorker moves through Running to Completed/Failed"
    - "Unknown worker names still return 404 'Worker '<name>' not found.' (allowlist is extended, not removed)"
    - "GameSyncWorker, GameImageSyncWorker and GameHLTBSyncWorker run-now behavior is unchanged"
    - "The e2e UAT spec comment no longer claims SteamPlaytimeSyncWorker lacks a run-now trigger; test body and title are unchanged"
  artifacts:
    - path: "GamePlanner.API/Controllers/WorkerController.cs"
      provides: "KnownWorkers allowlist entry and RunWorkerAsync dispatch branch for SteamPlaytimeSyncWorker"
      contains: "SteamPlaytimeSyncWorker.WorkerName"
    - path: "e2e/tests/uat.spec.ts"
      provides: "Accurate comment above the restart-initiated SteamPlaytimeSyncWorker test"
  key_links:
    - from: "GamePlanner.API/Controllers/WorkerController.cs (Run)"
      to: "KnownWorkers HashSet"
      via: "KnownWorkers.Contains(name) gate — missing entry is the 404 root cause"
      pattern: "SteamPlaytimeSyncWorker\\.WorkerName"
    - from: "GamePlanner.API/Controllers/WorkerController.cs (RunWorkerAsync)"
      to: "GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs (RunAsync)"
      via: "new SteamPlaytimeSyncWorker(scopeFactory, ILogger<SteamPlaytimeSyncWorker>) then RunAsync(CancellationToken.None)"
      pattern: "new SteamPlaytimeSyncWorker\\(scopeFactory"
---

<objective>
Fix the 404 "Worker 'SteamPlaytimeSyncWorker' not found." returned when the user clicks "Run now" for SteamPlaytimeSyncWorker on the Workers page.

Root cause: `WorkerController` gates `POST api/worker/{name}/run` on a static `KnownWorkers` HashSet that lists only GameSyncWorker, GameImageSyncWorker and GameHLTBSyncWorker, and `RunWorkerAsync` dispatches only those three. `GET api/worker` returns every WorkerStatus row (including SteamPlaytimeSyncWorker), so the frontend renders a Run button the backend then rejects. `SteamPlaytimeSyncWorker` already exposes `public const string WorkerName` and `public async Task RunAsync(CancellationToken ct)` with the same shape as `GameHLTBSyncWorker`, so it plugs into the existing dispatch with no worker changes.

Purpose: make the run-now trigger work for all four workers shown on the Workers page.
Output: one-entry allowlist addition + one dispatch branch in WorkerController.cs; a corrected comment in e2e/tests/uat.spec.ts. No frontend change (workers.component.html already renders Run for every row). No worker, manager, DB or SP change.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md
@GamePlanner.API/Controllers/WorkerController.cs
@GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs

<interfaces>
From GamePlanner.API/Workers/SteamPlaytimeSyncWorker.cs (unchanged, already present):
- class SteamPlaytimeSyncWorker(IServiceScopeFactory scopeFactory, ILogger<SteamPlaytimeSyncWorker> logger) : BackgroundService
- public const string WorkerName = "SteamPlaytimeSyncWorker";
- public async Task RunAsync(CancellationToken ct)  — creates its own scope, resolves SteamPlaytimeSyncManager + WorkerStatusManager, sets Running/Completed/Failed/Idle itself. No force parameter.

From GamePlanner.API/Controllers/WorkerController.cs (current):
- private static readonly HashSet<string> KnownWorkers = [GameSyncWorker.WorkerName, GameImageSyncWorker.WorkerName, GameHLTBSyncWorker.WorkerName];
- RunWorkerAsync(string name, bool force) — if/else-if chain; the GameHLTBSyncWorker branch constructs the worker with scopeFactory + scope.ServiceProvider.GetRequiredService<ILogger<GameHLTBSyncWorker>>() and awaits RunAsync(CancellationToken.None).
</interfaces>

Working-tree note: the repo has unrelated uncommitted user changes in GamePlanner.API/Managers/GameImageManager.cs and game-planner-ui/src/app/components/game-detail/* plus untracked .claude/commands/, .claude/skills/, .playwright-mcp/, openspec/. Do NOT modify, stage, stash, revert or commit any of them. Stage only the two files in files_modified by explicit path (never `git add -A` / `git add .` / `git commit -a`).

No xUnit test is added (project rule: targeted tests only, no boilerplate). Rationale: the in-process ApiFactory host in GamePlanner.Tests points at the dev SQL Server, and a 202 from this endpoint fires a real SteamPlaytimeSync run (real WorkerStatus writes and playtime updates against dev data, Steam calls answered by the stub handler) on a fire-and-forget task, which would pollute the dev WorkerStatus row and make the assertion racy. The regression is covered instead by the live HTTP smoke check in Task 1.
</context>

<tasks>

<task type="tracer">
  <name>Task 1: Wire SteamPlaytimeSyncWorker into WorkerController run-now (allowlist + dispatch) and prove 202 end-to-end</name>
  <files>GamePlanner.API/Controllers/WorkerController.cs</files>
  <action>
In GamePlanner.API/Controllers/WorkerController.cs:

1. Add `SteamPlaytimeSyncWorker.WorkerName` as a fourth entry of the `KnownWorkers` collection initializer, after `GameHLTBSyncWorker.WorkerName` (add the comma on the previous line). Keep it a static allowlist of compile-time constants — do not replace it with reflection, a DI lookup of IHostedService types, or a string built from the request; the allowlist is the input-validation boundary for the `{name}` route value.

2. In `RunWorkerAsync`, append an `else if (name == SteamPlaytimeSyncWorker.WorkerName)` branch after the GameHLTBSyncWorker branch that mirrors it exactly: construct `new SteamPlaytimeSyncWorker(scopeFactory, scope.ServiceProvider.GetRequiredService<ILogger<SteamPlaytimeSyncWorker>>())` and `await worker.RunAsync(CancellationToken.None);`. Do not pass `force` (SteamPlaytimeSyncWorker.RunAsync has no force parameter; the frontend only shows Force Run for GameSyncWorker). The `using GamePlanner.API.Workers;` import already covers the type.

Do not touch `Run`, `GetAll`, the fire-and-forget `Task.Run`, the `[Authorize]` attribute, SteamPlaytimeSyncWorker.cs, Program.cs, or any frontend file. Match existing formatting (4-space indent, aligned constructor args on the continuation line like the other branches).

Then run the end-to-end check in the verify block:
- Build. If `dotnet build` fails with a file-lock error (MSB3027/MSB3021 on GamePlanner.API.dll), the user has the API running: do NOT kill the user's process. Instead confirm the code compiles with `dotnet build GamePlanner.API/GamePlanner.API.csproj -o` pointed at a folder under the session scratchpad, skip the live HTTP step, and report in the SUMMARY that the live 202 check needs the user to restart the API.
- Live check: if nothing is listening on https://localhost:7174, start the API from GamePlanner.API with `dotnet run --launch-profile https` as a background process and wait until `GET https://localhost:7174/api/worker` answers 401 (up, unauthenticated). Note that startup itself also kicks off every hosted worker including SteamPlaytimeSyncWorker — that is the normal dev behavior the user already accepted, not a side effect of this fix. Log in as the UAT test account (email uat-test@gameplanner.local, UserId 17; password from the uat-test-user memory note, exported as UAT_PASSWORD — never write the password into any committed file or the SUMMARY). Never use the user's own account (UserId 2). POST to /api/worker/SteamPlaytimeSyncWorker/run with the bearer token and confirm HTTP 202 and a body containing "SteamPlaytimeSyncWorker triggered."; POST to /api/worker/NoSuchWorker/run and confirm it still returns 404. Then poll GET /api/worker (every ~2s, up to ~5 min) until the SteamPlaytimeSyncWorker row is no longer Running and record its final status/lastError in the SUMMARY. Stop only the API process you started yourself.

Commit only GamePlanner.API/Controllers/WorkerController.cs by explicit path (message e.g. "fix(quick-261027-qhu): allow run-now for SteamPlaytimeSyncWorker").
  </action>
  <verify>
    <automated>cd "E:/PROYECTO NO WORK/GamePlanner" && dotnet build GamePlanner.sln --nologo -v q && test "$(grep -c 'SteamPlaytimeSyncWorker.WorkerName' GamePlanner.API/Controllers/WorkerController.cs)" -eq 2 && test "$(grep -c 'new SteamPlaytimeSyncWorker(scopeFactory' GamePlanner.API/Controllers/WorkerController.cs)" -eq 1 && test "$(grep -c 'ILogger<SteamPlaytimeSyncWorker>' GamePlanner.API/Controllers/WorkerController.cs)" -eq 1 && test "$(grep -c 'GameHLTBSyncWorker.WorkerName' GamePlanner.API/Controllers/WorkerController.cs)" -eq 2 && echo STATIC_OK</automated>
    <automated>API=https://localhost:7174; TOKEN=$(curl -sk -X POST "$API/api/auth/login" -H "Content-Type: application/json" -d "{\"email\":\"uat-test@gameplanner.local\",\"password\":\"$UAT_PASSWORD\"}" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p'); test -n "$TOKEN" && test "$(curl -sk -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $TOKEN" "$API/api/worker/SteamPlaytimeSyncWorker/run")" = "202" && test "$(curl -sk -o /dev/null -w '%{http_code}' -X POST -H "Authorization: Bearer $TOKEN" "$API/api/worker/NoSuchWorker/run")" = "404" && echo E2E_OK</automated>
  </verify>
  <done>
- `dotnet build GamePlanner.sln` succeeds (or, if the user's running API locks the DLL, the API project compiles to a scratch output and the live check is reported as pending restart).
- KnownWorkers contains SteamPlaytimeSyncWorker.WorkerName; RunWorkerAsync has a SteamPlaytimeSyncWorker branch identical in shape to the HLTB one.
- Live: POST api/worker/SteamPlaytimeSyncWorker/run returns 202 with "SteamPlaytimeSyncWorker triggered."; POST api/worker/NoSuchWorker/run returns 404; SteamPlaytimeSyncWorker status leaves Running and its final status is recorded in the SUMMARY.
- Only WorkerController.cs is in the commit; GameImageManager.cs and game-detail files remain modified-but-unstaged.
  </done>
</task>

<task type="auto">
  <name>Task 2: Correct the now-inaccurate e2e comment about SteamPlaytimeSyncWorker's trigger</name>
  <files>e2e/tests/uat.spec.ts</files>
  <action>
In e2e/tests/uat.spec.ts, edit only the two-line `//` comment immediately above the test titled "SteamPlaytimeSyncWorker restart-initiated run (...)" (around lines 246-247). Its first clause states the worker lacks a run-now trigger, which is false after Task 1. Rewrite the comment so it says: this test still asserts the restart-initiated run, per the recorded deviation decided on 2026-10-24 (keep the existing decision reference and date from the current comment verbatim), and that a run-now trigger for this worker was added later in quick task 261027-qhu. Keep it to two comment lines at the same indentation.

Do NOT change the test title, the test body, the polling logic, imports, or any other test — this is a comment-only correction (orchestrator instruction: update the comment only, do not rewrite the test).

Commit only e2e/tests/uat.spec.ts by explicit path (message e.g. "docs(quick-261027-qhu): update UAT spec comment for SteamPlaytimeSyncWorker run-now").
  </action>
  <verify>
    <automated>cd "E:/PROYECTO NO WORK/GamePlanner" && test "$(grep -c 'has no run-now trigger' e2e/tests/uat.spec.ts)" -eq 0 && test "$(grep -c '261027-qhu' e2e/tests/uat.spec.ts)" -ge 1 && test "$(grep -c 'restart-initiated run' e2e/tests/uat.spec.ts)" -ge 1 && test "$(grep -c '2026-10-24' e2e/tests/uat.spec.ts)" -ge 1 && git diff HEAD~1 --numstat -- e2e/tests/uat.spec.ts | awk '{ if ($1 > 2 || $2 > 2) exit 1 }' && echo COMMENT_OK</automated>
  </verify>
  <done>
- The comment above the restart-initiated SteamPlaytimeSyncWorker test no longer claims there is no run-now trigger and references quick task 261027-qhu.
- Diff for uat.spec.ts touches at most 2 added / 2 removed lines, all `//` comment lines; test title and body unchanged.
- Only uat.spec.ts is in this commit.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Angular client -> POST api/worker/{name}/run | Untrusted `{name}` route value from an authenticated (JWT) caller selects which background job runs |
| WorkerController -> SteamPlaytimeSyncManager -> Steam Web API | A run-now trigger causes outbound Steam API calls using the server-held Steam:ApiKey |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-qhu-01 | Tampering / Elevation | WorkerController.Run `{name}` | medium | mitigate | Name is matched against the static `KnownWorkers` HashSet of compile-time `WorkerName` constants and dispatched through an explicit if/else-if chain; Task 1 forbids reflection, DI type lookup, or any request-derived type resolution. Unknown names keep returning 404 (verified live with NoSuchWorker). |
| T-qhu-02 | Denial of Service | Run-now for SteamPlaytimeSyncWorker (Steam API quota / overlapping runs) | low | accept | Same exposure the three existing workers already have; `[Authorize]` stays on the controller; single-user personal app with no production users; the Workers UI disables Run while status is Running. Adding role-based access or a server-side overlap guard is out of scope for this quick fix. |
| T-qhu-03 | Information Disclosure | Live smoke check credentials | low | mitigate | Task 1 uses the throwaway UAT account (UserId 17) with the password supplied via UAT_PASSWORD from local memory; the password must not be written into any committed file or the SUMMARY, and the user's own account (UserId 2) is not used. |
</threat_model>

<verification>
- `dotnet build GamePlanner.sln` exits 0.
- Static gates in Task 1 verify print STATIC_OK; Task 2 verify prints COMMENT_OK.
- Live: POST api/worker/SteamPlaytimeSyncWorker/run -> 202; POST api/worker/NoSuchWorker/run -> 404; SteamPlaytimeSyncWorker WorkerStatus row transitions out of Running (final status recorded in SUMMARY).
- `git status --short` after both commits still shows GamePlanner.API/Managers/GameImageManager.cs and the game-detail component files as modified (unstaged, untouched), and the untracked folders unchanged.
</verification>

<success_criteria>
- The user can click "Run now" for SteamPlaytimeSyncWorker on the Workers page and gets "SteamPlaytimeSyncWorker triggered." instead of the 404 error.
- All four workers listed by GET api/worker are runnable on demand; unknown names still 404.
- Two atomic commits touching exactly WorkerController.cs and uat.spec.ts; no unrelated files staged.
</success_criteria>

<output>
Create `.planning/quick/261027-qhu-fix-404-al-ejecutar-steamplaytimesyncwor/261027-qhu-SUMMARY.md` with `status: complete` in frontmatter when done (do not commit docs artifacts — the orchestrator handles the docs commit).
</output>
