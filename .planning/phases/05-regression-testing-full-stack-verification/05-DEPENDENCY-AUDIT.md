# Phase 5 Dependency Audit (D-07)

## Scan context

| Item | Value |
|------|-------|
| Date | 2026-10-24 |
| Node | v24.19.0 |
| Global npm | 8.12.1 (`npm audit fix` crashes on it, RESEARCH Pitfall 10) |
| npm used for audit and lockfile regeneration | 11.21.1 via `npx -y npm@11.21.1` |
| dotnet SDK | 10.0.401 (target framework net8.0) |
| Scope | `game-planner-ui` (npm), `GamePlanner.sln` (NuGet: API + Tests) |

Fix policy (D-07): critical/high fixed only inside Angular 22's peer ranges. No forced install, no legacy-peer-deps install, no npm `overrides` block, no audit-level or audit-disable config.

## npm: before / after

| Severity | Before | After |
|----------|-------:|------:|
| critical | 5 | 0 |
| high | 14 | 1 (`vite`) |
| moderate | 9 | 0 |
| low | 1 | 2 (`@angular/build`, `@angular-devkit/build-angular`, dependents of `vite`) |
| total | 29 | 3 |

Change made: nine `@angular/*` ranges `^22.1.4` -> `^22.2.3` (`animations`, `common`, `compiler`, `core`, `forms`, `platform-browser`, `platform-browser-dynamic`, `router`, plus `compiler-cli` in devDependencies), then lockfile regenerated with npm 11.21.1. The first `npm install` hit ERESOLVE (exact optional-peer pins in the old lockfile), so `package-lock.json` and `node_modules` were deleted and `npm install` re-run, as the plan prescribes. Nothing was forced.

Installed tree after regeneration (Phase 1 pins survived): `@angular/core` 22.2.3, `@angular/router` 22.2.3, `@angular/build` 22.2.4, `@angular-devkit/build-angular` 22.2.4, `typescript` 6.0.3, `jasmine-core` 6.3.0, `zone.js` 0.15.1.

Cleared by the bump and regeneration:

- critical: `@angular-devkit/build-angular`, `@angular/build`, `piscina`, `proxy-addr`, `shell-quote`
- high: `@angular/cli`, `@angular/router` (22.0.0 - 22.2.0), `@modelcontextprotocol/sdk`, `body-parser`, `brace-expansion`, `compression`, `engine.io`, `fast-uri`, `follow-redirects`, `launch-editor`, `source-map-js`, `webpack-dev-middleware`, `zod`
- moderate: `@angular-devkit/build-webpack`, `express`, `hono`, `ip-address`, `postcss-selector-parser`, `qs`, `sockjs`, `uuid`, `webpack-dev-server`
- low: `serialize-javascript`

## Accepted risk

| Package | Version | Severity | Advisories | Why not fixable in range | Why contextual risk is low | Revisit trigger |
|---------|---------|----------|------------|--------------------------|----------------------------|-----------------|
| `vite` | 8.3.0 | high (rolled up from `launch-editor` GHSA-587h-7wmj-h5pm; also GHSA-698v-4r3x-3vcv, GHSA-r825-w8mm-5pxw, GHSA-9jrq-w75r-8gcw, GHSA-rq7h-c2jc-7f22, GHSA-vfpm-58rq-9qcg) | range `>=8.3.0 <=8.3.2` | `@angular/build@22.2.4` (latest 22.x in range) pins `vite` to exactly `8.3.0` (`npm view`/lockfile: `dependencies.vite = 8.3.0`), and `@angular-devkit/build-angular@22.2.4` depends on `@angular/build`. The only "fix" npm offers is a different Angular build chain outside the peer range. | Evidence below: `angular.json` has no `@angular/build:*` builder (build, serve, extract-i18n, test all use `@angular-devkit/build-angular:*`, webpack-based), so Vite's dev server is never started by this project. `vite` appears only beneath `@angular/build`. The advisories concern the Vite dev-server middleware and `launch-editor` on a dev machine; nothing from `vite` ships in the production bundle. | The next `@angular/build` release that depends on a `vite` outside `>=8.3.0 <=8.3.2`, or the project moving a target to `@angular/build:*` (then re-rate immediately). |

Evidence (A6 proven, 2026-10-24):

```
npm ls vite
game-planner-ui@0.0.0
`-- @angular-devkit/build-angular@22.2.4
  `-- @angular/build@22.2.4
    +-- @vitejs/plugin-basic-ssl@2.3.0
    | `-- vite@8.3.0 deduped
    `-- vite@8.3.0

angular.json architect builders
game-planner-ui build         @angular-devkit/build-angular:browser
game-planner-ui serve         @angular-devkit/build-angular:dev-server
game-planner-ui extract-i18n  @angular-devkit/build-angular:extract-i18n
game-planner-ui test          @angular-devkit/build-angular:karma
```

Residual lows `@angular/build` and `@angular-devkit/build-angular` are dependents of `vite` only (`via: ["vite"]` / `via: ["@angular/build"]`) and clear together with it.

## Moderate/low: report only

D-07 locks moderate and low findings as report-only.

- npm: after the bump there are no moderate findings. The two lows above are the `vite` dependents.
- NuGet: two moderate transitive findings, see below.

## Phase 1 accepted findings

Phase 1 (01-06) accepted `image-size` and `uuid` rather than apply a forced fix that would have downgraded `@angular-devkit/build-angular` to the legacy `0.1002.1`.

- `image-size`: not present in the regenerated lockfile (gone).
- `uuid`: was a moderate finding in the pre-bump baseline; not present in the regenerated lockfile (gone).

No forced fix was needed; both cleared with the in-range regeneration.

## NuGet: before / after

Command: `dotnet list GamePlanner.sln package --vulnerable --include-transitive`

| Project | Before (pre-Phase 5) | After |
|---------|----------------------|-------|
| `GamePlanner.Tests` | `System.Net.Http` 4.3.0 High, `System.Text.RegularExpressions` 4.3.0 High | cleared by `xunit` 2.5.3 -> 2.9.3 (05-01); only the two moderate IdentityModel rows below remain |
| `GamePlanner.API` | moderate IdentityModel 7.0.3 | unchanged (report-only) |

No High or Critical package in either project (re-scanned in this plan).

Remaining (both projects, transitive via `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.0):

| Package | Resolved | Severity | Advisory |
|---------|----------|----------|----------|
| `Microsoft.IdentityModel.JsonWebTokens` | 7.0.3 | Moderate | GHSA-59j7-ghrg-fj52 |
| `System.IdentityModel.Tokens.Jwt` | 7.0.3 | Moderate | GHSA-59j7-ghrg-fj52 |

Not applied (D-07): a one-line `JwtBearer` 8.0.0 -> 8.0.32 bump in `GamePlanner.API.csproj` would pull the IdentityModel packages forward. It is recorded as an available option, not done, because D-07 makes moderate findings report-only.

Deprecated (`dotnet list GamePlanner.sln package --deprecated`): `GamePlanner.API` has none. `GamePlanner.Tests` lists `xunit` 2.9.3 as Legacy (alternative `xunit.v3`); report-only, no security impact.

## e2e package

Appended by 05-06.

## Verdict

ROADMAP success criterion 3 (no known-EOL or high-severity package remaining from the pre-hardening baseline) is met. All 5 critical and 13 of 14 high npm findings from the baseline are cleared in range, both NuGet High findings are cleared, and Phase 1's accepted `image-size`/`uuid` findings are gone. The single residual high, `vite` 8.3.0, arrived with the Angular 22 toolchain rather than the pre-hardening baseline; it is accepted with evidence (webpack-based builders only, dev-tooling scope) and a revisit trigger. Regression gates after the bump: `npm run build` exits 0, `npm run test:ci` 8/8, dev-server boot smoke on port 4300 served `<app-root` and compiled successfully.
