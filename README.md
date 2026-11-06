# GamePlanner

Personal game-tracking web app: manage your library, see what to play next, track what you are playing on each device, and keep a journal. Angular SPA (`game-planner-ui/`) + ASP.NET Core 8 API (`GamePlanner.API/`) + SQL Server (`Database/`). The UI is in English.

## Installation

Requirements: .NET 8 SDK, Node.js 14+ with npm, SQL Server (2019+ or Express), Chrome (for e2e).

1. **Database.** In SSMS, run the scripts in `Database/` one by one in numeric order: `01` to `42` (`17b_AlterImportJobs_AddSteamId.sql` goes right after `17`). Scripts `40_MvpSchema.sql`, `41_MvpDataMigration.sql` and `42_MvpProcedures.sql` are the MVP: schema, migration of existing data, and the new stored procedures. New migrations take the next free number and are applied by hand.
2. **Secrets** (not stored in the repo). In development, from `GamePlanner.API/`:
   ```bash
   dotnet user-secrets set "IGDB:ClientId" "..."
   dotnet user-secrets set "IGDB:ClientSecret" "..."
   dotnet user-secrets set "Steam:ApiKey" "..."
   dotnet user-secrets set "Jwt:Secret" "..."
   ```
   `GamePlanner.API/appsettings.Development.json.example` lists the keys. In other environments use the environment variables `IGDB__ClientId`, `IGDB__ClientSecret`, `Steam__ApiKey`, `Jwt__Secret`. Set `ConnectionStrings:DefaultConnection` for your SQL Server.
3. **Run.**
   ```bash
   cd GamePlanner.API && dotnet run --launch-profile https   # https://localhost:7174
   cd game-planner-ui && npm install && npm start             # http://localhost:4200
   ```
4. **Secret-scanning hook (once):** `winget install --id Gitleaks.Gitleaks --exact`, then `powershell -File scripts/install-git-hooks.ps1`.

## Configuration: admin role

Workers (background syncs) are visible only to admins. Nobody is admin by default. Turn it on with:

```sql
UPDATE Users SET IsAdmin = 1 WHERE Id = <user id>;
```

**Sign out and sign in again** afterwards: the role travels in the JWT, so the old token does not carry it. Admins get an "Administration > Workers" item in the menu and can open `/workers`; everyone else is redirected to `/dashboard` and gets 403 from `/api/worker`.

## Features

- **Now Playing:** one active game per device (PC, Steam Deck, Switch, PlayStation, Xbox, Mobile, Other; or a single General slot if you set none). Log play sessions (hours accept "1,5" or "1.5", a note, a date), switch games, and finish with an outcome (Finished with a 1-5 star rating, Dropped, Paused) that updates the Journal.
- **What to Play:** ranked recommendations with the reasons for each, by device, session time, game length, genre, mood, perspective, mode and strategy. Finished, dropped and now-playing games are left out. "Start on {device}" puts the top pick straight into Now Playing.
- **My Games:** library independent of Steam. Add through IGDB search or by hand, edit, set store and playable devices, mark favorites, filter by status, store, device, length and favorites.
- **Game detail:** metadata, HowLongToBeat data, screenshots lightbox, play sessions (log, edit, delete).
- **Journal, Calendar, Stats:** status per game with ratings and notes, a calendar, and statistics.
- **Profile:** your devices and preferences (favorite and avoided genres, maximum game length, single-player, story and combat leanings).
- **Steam import** and background workers for IGDB, images, HowLongToBeat and Steam playtime (admin only).

## Known limitations

- The store of a game is entered by hand. Only Steam imports automatically; there are no Epic, GOG, Xbox or PlayStation integrations.
- Hours logged as play sessions are kept apart from Steam playtime and are not written to `PlaytimeHours`; both are shown side by side.
- Removing a device that still holds a game in Now Playing is refused (`device_in_use`).
- The e2e test account is a regular user, so the admin-only "Run now" worker flows are not exercised by the e2e suite.
- See `docs/MVP_RELEASE_CHECKLIST.md` for the full list and the verified state.

## Tests

```bash
# Backend (needs the test database; rebuild it with scripts/build-test-db.ps1)
export ConnectionStrings__DefaultConnection="Server=localhost;Database=GamePlannerDB_Test;Trusted_Connection=True;TrustServerCertificate=True"
dotnet test GamePlanner.Tests

# Frontend
cd game-planner-ui && npx ng test --watch=false --browsers=ChromeHeadless

# E2E (API and UI running; credentials of the dedicated test account in e2e/.env)
cd e2e && npx playwright test
```

The e2e suite only runs as the dedicated test account (`uat-test@gameplanner.local`) and prefixes everything it creates with `uat-`.
