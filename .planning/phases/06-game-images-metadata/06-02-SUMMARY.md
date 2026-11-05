---
phase: 06-game-images-metadata
plan: 02
subsystem: database
tags: [igdb, portal, data-fix, sql, external-games, survey]
status: checkpoint-pending
requires: [06-01, 06-04]
provides:
  - "Database/39_FixPortalIgdbId.sql: guarded, transactional, reversible Portal IGDBId fix 14546 -> 71 (authored, dry-run proven, NOT applied)"
  - "D-07 suspect todo (48 suspects, none changed)"
  - "Pre-restart survey for 06-05: PREVIEW lines, SIBLING_PAIR, NIER_IGDB_COVER, NULL-cover list"
affects: [06-05, 06-06]
key-files:
  created:
    - Database/39_FixPortalIgdbId.sql
    - .planning/todos/pending/2026-10-27-igdbid-suspect-rematch.md
key-decisions:
  - "D-05: Portal's correct IGDB id is 71 (Valve, 2007-10-10), resolved by the Steam external_games mapping (uid 400, source 1), not by name search"
  - "D-06: script 39 mirrors script 38; GameTags (TagId 15 Action) is kept"
  - "D-07: suspects are logged only; no suspect row was changed"
requirements-completed: []
---

# Phase 6 Plan 02: Portal IGDBId fix script 39 and pre-restart survey Summary

Script 39 (Portal 14546 -> 71) is authored, committed and proven by two rolled-back dry runs; the read-only D-07 scan, matcher preview and IMG-01 survey are done. The plan is paused at its blocking owner checkpoint (Task 3); nothing was written to GamePlannerDB.

## Status

Tasks 1 and 2 are complete and committed. Task 3 (`checkpoint:decision`, blocking) is awaiting the owner. Plan 06-05 must not start until the checkpoint outcome below is recorded.

## Tasks and commits

| Task | Commit | Result |
|------|--------|--------|
| 1 (tracer): script 39 | `15738e4` | `Database/39_FixPortalIgdbId.sql`; `DRYRUN_PORTAL` and `REVERSAL_DIFF=0` dry runs passed |
| 2: survey + suspect todo | `dcfdced` | `.planning/todos/pending/2026-10-27-igdbid-suspect-rematch.md` (48 suspects); survey sections below |
| 3: owner checkpoint | pending | see "Checkpoint decision" |

## IGDB evidence (Task 1, read-only, 2026-11-05)

`external_games` with `where uid = "400" & external_game_source = 1; limit 5;`:

```json
[{"id":15156,"game":71,"name":"Portal","uid":"400","external_game_source":1}]
```

`games` with `where id = (71,14546);` (release dates converted from `first_release_date`):

| id | name | release (UTC) | developer | cover image_id | updated_at |
|----|------|---------------|-----------|----------------|-----------|
| 71 | Portal | 2007-10-10 | Valve (publisher Electronic Arts) | coay61 | 1793883822 |
| 14546 | Portal | 1986-11-01 | Nexa Corporation (publisher Activision) | co4i78 | 1791253422 |

The mapping is unchanged from RESEARCH; 71 is Valve's 2007 Portal. The stored cover `coaanx` belongs to neither game.

## Pre-fix capture of Games 107693 (live, 2026-11-05)

```
Id 107693 | UserId 2 | Name Portal | SteamAppId 400 | Source Steam | IsActive 1 | IGDBId 14546
FirstReleaseDate 1986-11-01T00:00:00 | AggregatedRating 80.0 | AggregatedRatingCount 0
CoverImageUrl https://images.igdb.com/igdb/image/upload/t_cover_big/coaanx.jpg
IGDBUpdatedAt 2026-08-28T02:26:32 | LastIGDBSyncAt 2026-08-30T00:58:28.0118486
LastImageSyncDate 2026-11-05T16:09:38.143 | ModifiedAt NULL | DeletedAt NULL
GameGenres 1,3,7 | GameThemes 1 | GamePlayerPerspectives 1 | GameModesRelation 3 | GameTags 15 (kept)
GameImages 4464 h1sxo1adkfjzwpksozfy 320x256 screenshot SyncedAt 2026-11-05T16:09:37.5600000
GameImages 4467 xx33ndsquzvhr2i0quao 320x200 screenshot SyncedAt 2026-11-05T16:09:37.8700000
```

Difference from the RESEARCH snapshot (2026-10-27): only `LastImageSyncDate` (2026-10-27T20:08:52.467 -> 2026-11-05T16:09:38.143) and both `GameImages.SyncedAt` values moved, because an image-worker pass touched the row on 2026-11-05 before this plan ran. Everything else is identical. The live values are the ones in the script 39 REVERSAL block. If an image-worker pass touches the row again before the owner applies the script, the REVERSAL's `LastImageSyncDate` / `SyncedAt` literals will be a few days stale; they only affect which timestamps a manual rollback restores, not the fix itself.

## Dry-run outputs (both rolled back, COMMIT stripped, abort-before-sqlcmd guard ran)

Fix body:

```
DRYRUN_PORTAL=71|NULL|NULL|NULL|NULL|NULL|NULL|NULL
DRYRUN_CHILDREN=0
```

Fix body followed by the uncommented REVERSAL, compared with a pre-fix snapshot:

```
REVERSAL_DIFF=0
```

After both runs the live row still reads IGDBId 14546. Note: Windows `sqlcmd` returns CRLF, so the plan's `grep -qx` assertions only pass after piping `sqlcmd` through `tr -d '\015'`; the run used that wrapper (environment difference, not a script defect).

## D-07 suspect scan

Method (all read-only): (a) SQL early-release check, (b) IGDB Steam-mapping comparison for every active UserId 2 Steam row in batches of 50 uids (`fields uid,game; where uid = (...) & external_game_source = 1 & game != null; sort game asc; limit 500;`, no batch reached 500 rows), (c) SQL same-user duplicate-title groups.

- (a) `SteamAppId IS NOT NULL AND FirstReleaseDate < '1995-01-01'`: Portal 107693 (1986, fixed by script 39) and Strider 107764 (1989, stored 6969 vs mapped 5333).
- (b) 480 active UserId 2 Steam rows: **411 agree, 49 disagree, 0 unmapped, 0 multi-mapped, 20 have a NULL IGDBId** (those are the matcher rows below, not suspects). Portal is one of the 49; 48 are logged as suspects.
- (c) Same-user duplicate-title IGDBId groups: none (Half-Life/Half-Life 2 were fixed by script 38).

Full disagree list (SteamAppId | Id | Name | stored -> mapped):

```
SteamAppId | Id | Name | stored -> mapped
10 | 107677 | Counter-Strike | 266357 (Counter-Strike (2003)) -> 241 (Counter-Strike (2000))
60 | 107682 | Ricochet | 368008 (Ricochet (2022)) -> 7589 (Ricochet (2000))
130 | 107686 | Half-Life: Blue Shift | 285235 (Half-Life: Blue Shift (2018)) -> 2549 (Half-Life: Blue Shift (2001))
400 | 107693 | Portal | 14546 (Portal (1986)) -> 71 (Portal (2007))  [fixed by script 39]
500 | 107694 | Left 4 Dead | 60984 (Left 4 Dead: Survivors (2014)) -> 122 (Left 4 Dead (2008))
7670 | 107704 | BioShock | 152660 (BioShock (2010)) -> 20 (BioShock (2007))
12210 | 107710 | Grand Theft Auto IV: The Complete Edition | 27912 (Grand Theft Auto IV: Complete Edition (2010)) -> 731 (Grand Theft Auto IV (2008))
13500 | 107711 | Prince of Persia: Warrior Within | 213052 (Prince of Persia: Warrior Within (2004)) -> 837 (Prince of Persia: Warrior Within (2004))
13560 | 107712 | Tom Clancy's Splinter Cell | 134350 (Tom Clancy's Splinter Cell (2003)) -> 910 (Tom Clancy's Splinter Cell (2002))
13570 | 107713 | Tom Clancy's Splinter Cell: Chaos Theory | 243021 (Tom Clancy's Splinter Cell: Chaos Theory (2005)) -> 911 (Tom Clancy's Splinter Cell: Chaos Theory (2005))
34870 | 107724 | Sniper Ghost Warrior 2 | 138980 (Sniper Ghost Warrior Contracts 2 (2021)) -> 1368 (Sniper: Ghost Warrior 2 (2013))
55150 | 107731 | Warhammer 40,000: Space Marine - Anniversary Edition | 186034 (Warhammer 40,000: Space Marine - Anniversary Edition (2021)) -> 578 (Warhammer 40,000: Space Marine (2011))
205950 | 107744 | Jet Set Radio | 21004 (Jet Set Radio (2012)) -> 1569 (Jet Grind Radio (2000))
208650 | 107745 | Batman™: Arkham Knight | 26041 (Batman: Arkham Knight - 2008 Movie Batman Skin (2015)) -> 5503 (Batman: Arkham Knight (2015))
228300 | 107756 | Remember Me | 372230 (Remember Me (2025)) -> 1827 (Remember Me (2013))
235210 | 107764 | Strider | 6969 (Strider (1989)) -> 5333 (Strider (2014))
242760 | 107777 | The Forest | 99425 (Märchen Forest: Mylne and the Forest Gift (2018)) -> 7504 (The Forest (2018))
268500 | 107792 | XCOM 2 | 86268 (XCOM 2 Collection (2018)) -> 10919 (XCOM 2 (2016))
272060 | 107793 | Serena | 325536 (Serena (2022)) -> 14528 (Serena (2014))
294860 | 107806 | Valkyria Chronicles™ | 75848 (Valkyria Chronicles 4 (2018)) -> 18967 (Valkyria Chronicles Remastered (2016))
356190 | 107831 | Middle-earth™: Shadow of War™ | 52198 (Middle-earth: Shadow of War Mobile (2017)) -> 27421 (Middle-earth: Shadow of War (2017))
435150 | 107868 | Divinity: Original Sin 2 | 154209 (Divinity: Original Sin II - Divine Edition (n/a)) -> 11800 (Divinity: Original Sin II (2017))
448280 | 107870 | Job Simulator | 120688 (Dirty Jobs Simulator (n/a)) -> 12596 (Job Simulator: The 2050 Archives (2016))
460790 | 107872 | Bayonetta | 279337 (Bayonetta (2014)) -> 2136 (Bayonetta (2009))
460810 | 107873 | Vanquish | 220716 (Vanquish (2022)) -> 3218 (Vanquish (2010))
524220 | 107889 | NieR:Automata™ | 391942 (Untitled NieR:Automata Project (n/a)) -> 11208 (NieR: Automata (2017))
571740 | 107900 | Golf It! | 64369 (WGT Golf (2013)) -> 30099 (Golf It! (2023))
632470 | 107920 | Disco Elysium | 335434 (Disco Elysium (2025)) -> 26472 (Disco Elysium (2019))
678950 | 107933 | DRAGON BALL FighterZ | 144760 (Dragon Ball FighterZ and Dragon Ball Xenoverse 2 Double Pack (2018)) -> 36911 (Dragon Ball FighterZ (2018))
678960 | 107934 | CODE VEIN | 347636 (Code Vein II (2026)) -> 28168 (Code Vein (2019))
812140 | 107957 | Assassin's Creed Odyssey | 164698 (Assassin's Creed Bundle: Assassin's Creed Valhalla, Assassin's Creed Odyssey, and Assassin's Creed Origins (2020)) -> 103054 (Assassin's Creed Odyssey (2018))
858260 | 107968 | Until You Fall | 99964 (Transformers: Fall of Cybertron - Ultimate Edition (2013)) -> 115478 (Until You Fall (2020))
883710 | 107974 | Resident Evil 2 | 347128 (Resident Evil 2 (2025)) -> 19686 (Resident Evil 2 (2019))
911400 | 107977 | Assassin's Creed III Remastered | 109532 (Assassin's Creed III Remastered (2019)) -> 237701 (Assassin's Creed III Remastered (2019))
955050 | 107982 | Bright Memory | 134072 (Bright Memory (2020)) -> 113739 (Bright Memory: Infinite (2021))
997070 | 107989 | Marvel's Avengers - The Definitive Edition | 259466 (Marvel's Avengers Definitive Edition (2023)) -> 26950 (Marvel's Avengers (2020))
1097350 | 108009 | Weird West: Definitive Edition | 250504 (Weird West: Definitive Edition (2023)) -> 127350 (Weird West (2022))
1113000 | 108010 | Persona 4 Golden | 2985 (Persona 4 Golden (2012)) -> 234702 (Persona 4 Golden (2020))
1184370 | 108022 | Pathfinder: Wrath of the Righteous - Enhanced Edition | 226521 (Pathfinder: Wrath of the Righteous - Enhanced Edition (2021)) -> 127254 (Pathfinder: Wrath of the Righteous (2021))
1332010 | 108044 | Stray | 222370 (Fantasy Maiden Wars: Dream of the Stray Dreamer (2022)) -> 110248 (Stray (2022))
1384160 | 108054 | GUILTY GEAR -STRIVE- | 264825 (Guilty Gear: Strive - Guilty Gear 25th Anniversary: Special Additional Color Pack (2023)) -> 125764 (Guilty Gear: Strive (2021))
1422450 | 108060 | Deadlock | 31699 (Deadlock (2016)) -> 301298 (Deadlock (n/a))
1449560 | 108063 | Metro Exodus Enhanced Edition | 143292 (Metro Exodus: Enhanced Edition (2021)) -> 37016 (Metro Exodus (2019))
2012320 | 108109 | Parallel Experiment | 285166 (Parallel Experiment (2025)) -> 198114 (Parallel Lab (2022))
2050650 | 108110 | Resident Evil 4 | 145191 (Resident Evil 4 (2005)) -> 132181 (Resident Evil 4 (2023))
2069040 | 108113 | Unboxing the Cryptic Killer | 247757 (Unboxing the Cryptic Killer (2023)) -> 192087 (Unboxing the Mind of a Cryptic Killer (2020))
2124490 | 108117 | SILENT HILL 2 | 222485 (Silent Hill 2 (2012)) -> 222341 (Silent Hill 2 (2024))
2592160 | 108133 | Dispatch | 376768 (Dispatch: Episode 1 - Pivot (2025)) -> 325609 (Dispatch (2025))
3837340 | 108145 | FINAL FANTASY VII | 207028 (Final Fantasy VII (2015)) -> 207026 (Final Fantasy VII (2015))
```

The 48 non-Portal suspects are in `.planning/todos/pending/2026-10-27-igdbid-suspect-rematch.md`. None was changed (D-07, D-10).

## Matcher preview

Query literals as sent, copied from the committed 06-04 `IgdbService.cs` (`FetchExternalGamesBySteamAppIdAsync` and `SearchGameAsync`); no difference from the 06-04 plan literals:

```
PREVIEW_QUERY_STEAM=fields uid,game; where uid = "<appid>" & external_game_source = 1 & game != null; sort game asc; limit 5;
PREVIEW_QUERY_SEARCH=search "<name, double quotes escaped>"; fields name,genres.name,themes.name,player_perspectives.name,game_modes.name,aggregated_rating,aggregated_rating_count,first_release_date,updated_at; limit 10;
```

All 22 active rows with `IGDBId IS NULL` (any user, Steam and non-Steam), the IGDB id the first matcher-enabled GameSyncWorker run is expected to assign (Steam mapping's lowest game id first; otherwise the exact-name-else-first-hit result of the limit-10 search; `no result` keeps NULL). A Steam row whose AppId has no IGDB mapping falls through to the name search, so every `none` row below was name-searched and returned nothing:

| Id | User | Name | SteamAppId | source | expected IGDB id | IGDB entry |
|----|------|------|-----------|--------|------------------|-----------|
| 107697 | 2 | Dark Messiah of Might & Magic Single Player | 2100 | steam | 2369 | Dark Messiah of Might and Magic (2006) |
| 107698 | 2 | Dark Messiah of Might & Magic Multi-Player | 2130 | steam | 2369 | Dark Messiah of Might and Magic (2006) |
| 107715 | 2 | Mass Effect (2007) | 17460 | steam | 73 | Mass Effect (2007) |
| 107717 | 2 | The Elder Scrolls IV: Oblivion Game of the Year Edition (2009) | 22330 | steam | 59 | The Elder Scrolls IV: Oblivion (2006) |
| 107725 | 2 | FINAL FANTASY VII (2013) | 39140 | steam | 392808 | Final Fantasy VII (2012) |
| 107787 | 2 | Darkest Dungeon® | 262060 | steam | 8965 | Darkest Dungeon (2016) |
| 107866 | 2 | Wallpaper Engine | 431960 | none | no result | no result |
| 107890 | 2 | Tom Clancy's The Division PTS | 524440 | none | no result | no result |
| 107899 | 2 | Serious Sam Fusion 2017 (beta) | 564310 | steam | 91191 | Serious Sam Fusion 2017 (n/a) |
| 107930 | 2 | Batman: The Enemy Within - The Telltale Series | 675260 | steam | 51525 | Batman: The Enemy Within (2017) |
| 107932 | 2 | We Were Here Too | 677160 | steam | 54486 | We Were Here Too (2018) |
| 107952 | 2 | Hunt: Showdown 1896 (Test Server) | 770720 | none | no result | no result |
| 107988 | 2 | Lossless Scaling | 993090 | none | no result | no result |
| 108052 | 2 | NINJA GAIDEN 3: Razor's Edge [NINJA GAIDEN: Master Collection] | 1369760 | steam | 5317 | Ninja Gaiden 3: Razor's Edge (2012) |
| 108077 | 2 | Until Then | 1574820 | steam | 134165 | Until Then (2024) |
| 108079 | 2 | NINJA GAIDEN Σ [NINJA GAIDEN: Master Collection] | 1580780 | steam | 143619 | Ninja Gaiden: Master Collection (2021) |
| 108080 | 2 | NINJA GAIDEN Σ2 [NINJA GAIDEN: Master Collection] | 1580790 | steam | 7385 | Ninja Gaiden Sigma 2 (2009) |
| 108081 | 2 | AGAINST | 1584840 | steam | 152211 | Against (2021) |
| 108099 | 2 | AGAINST Beatmap Editor | 1853600 | none | no result | no result |
| 108121 | 2 | HITMAN WOA (Early Access) | 2183750 | none | no result | no result |
| 108158 | 11 | GSD-0302-Test-Game | - | none | no result | no result |
| 108204 | 17 | UAT Delete Probe | - | none | no result | no result |

Machine-readable lines for 06-05 (compare live with the database):

```
PREVIEW 107697 2369
PREVIEW 107698 2369
PREVIEW 107715 73
PREVIEW 107717 59
PREVIEW 107725 392808
PREVIEW 107787 8965
PREVIEW 107866 NULL
PREVIEW 107890 NULL
PREVIEW 107899 91191
PREVIEW 107930 51525
PREVIEW 107932 54486
PREVIEW 107952 NULL
PREVIEW 107988 NULL
PREVIEW 108052 5317
PREVIEW 108077 134165
PREVIEW 108079 143619
PREVIEW 108080 7385
PREVIEW 108081 152211
PREVIEW 108099 NULL
PREVIEW 108121 NULL
PREVIEW 108158 NULL
PREVIEW 108204 NULL
```

Rows that change besides the Steam rows: the two non-Steam test rows (108158 user 11, 108204 user 17) have no search result and keep a NULL IGDBId. Stamped-row flag: none of the 22 NULL-IGDBId rows has `IGDBUpdatedAt` set (108266 Servant of the Lake and 108267 The Blood of Dawnwalker, flagged in RESEARCH, now carry IGDBIds 350179 and 282831 and are no longer NULL-IGDBId rows).

Notes for the owner: 107725 "FINAL FANTASY VII (2013)" would map to 392808 (Final Fantasy VII, 2012), and 108079/108080 (NINJA GAIDEN Sigma / Sigma 2) map to 143619 "Ninja Gaiden: Master Collection" and 7385 "Ninja Gaiden Sigma 2". These are what the 06-04 matcher produces; they are shown so the owner can object before the restart.

SIBLING gate (success criterion 2): Dark Messiah SP 107697 (Steam 2100) and MP 107698 (Steam 2130) both preview to 2369, "Dark Messiah of Might and Magic" (2006). IGDB has 1 artwork for game 2369 (and 5 screenshots).

```
SIBLING_PAIR=107697,107698 igdb=2369
SIBLING_IGDB_ARTWORKS=1
```

## IMG-01 proof candidates

Active rows with `CoverImageUrl IS NULL` (23): the 22 NULL-IGDBId rows listed in the matcher preview plus NieR:Automata 107889 (see below). For all 22 NULL-IGDBId rows, IGDBId, IGDBUpdatedAt and LastImageSyncDate are NULL (the two non-Steam rows 108158 and 108204 included).

NieR:Automata 107889 (UserId 2, Steam 524220): stored IGDBId 391942 = "Untitled NieR:Automata Project" (a D-07 suspect; the Steam mapping says 11208 NieR: Automata, 2017, cover co5pcj). IGDBUpdatedAt 2026-06-11T01:10:25, LastImageSyncDate 2026-11-05T16:09:49.183, cover NULL. IGDB game 391942 has no cover and its `updated_at` (1781140225 = 2026-06-11T01:10:25Z) equals the stored stamp, so the old code would have skipped it, but IGDB has nothing to fetch.

```
NIER_IGDB_COVER=no
```

Under D-22 NieR is therefore recorded as **not counted** as extra IMG-01 proof. The row's IGDBId is not changed (D-10).

Half-Life 107683 / Half-Life 2 107687 (D-22 non-discriminating regression baseline; both already hold `t_cover_big` covers, so they cannot prove IMG-01):

| Id | IGDBId | CoverImageUrl | IGDBUpdatedAt | LastImageSyncDate |
|----|--------|---------------|---------------|-------------------|
| 107683 | 231 | .../t_cover_big/coc2ea.jpg | 2026-10-27T07:17:41 | 2026-11-05T16:09:38.140 |
| 107687 | 233 | .../t_cover_big/co1nmw.jpg | 2026-10-27T07:18:44 | 2026-11-05T16:09:38.143 |

06-05's required live IMG-01 proof is the UAT-account repro row (cover NULL, `IGDBUpdatedAt` set) per D-22.

Dark Messiah 107697 / 107698: IGDBId, CoverImageUrl, IGDBUpdatedAt and LastImageSyncDate are all NULL today; the image worker's screenshot phase picks them up once the matcher gives them IGDB 2369.

## Heads-up for the owner

Shown at the Task 3 checkpoint, as written here:

1. Plan 06-05's restart runs every worker once, on all users' rows. Per D-24 plan 06-05 pauses at its own confirmation checkpoint right before the restart, so you confirm once more at that moment.
2. GameSyncWorker will assign the IGDB ids in the matcher preview to EVERY active row with a NULL IGDBId, Steam and non-Steam, and fill genres, rating and release date for them: 22 rows (listed above); 14 of the 20 Steam rows resolve through the Steam mapping or the name search, 6 Steam rows and the 2 non-Steam rows return no result and keep a NULL IGDBId. Non-Steam rows are matched by name search (exact name, else first hit), including rows an older stamp used to block (06-04).
3. GameImageSyncWorker will fetch screenshots and then covers for the NULL-cover rows (the 23 rows listed under IMG-01 proof candidates).
4. Plan 06-05 then triggers one non-force GameSyncWorker and GameImageSyncWorker run through `POST /api/worker/{name}/run`. Plan 06-06 runs the e2e lightbox test and the full Playwright suite on the same running stack without restarting it; the suite's workers block triggers the three run-now workers again.
5. Per D-26, after Claude has checked the re-synced rows with SQL and the API, 06-06 asks you to open the Portal 107693 page and the pages of the sibling pair (Dark Messiah 107697/107698) and confirm what you see. Claude never logs in as you.
6. If a preview row or the Dark Messiah siblings have not re-derived after those runs, 06-05 may trigger one more non-force GameSyncWorker and one more non-force GameImageSyncWorker run. If the check still fails, it stops and brings the named cause back to you.
7. Nothing is forced. No row that already has an IGDBId is re-matched. The 48 D-07 suspects keep their IGDBIds.

## Checkpoint decision (Task 3)

Pending. Options: `apply-user` (confirm IGDB 71 and the convention exemption, acknowledge the heads-up, apply script 39 in SSMS, reply "applied"), `revise` (supply a different id), `hold`. To be recorded here with option, date and the "applied" reply time. Sibling pair presented: `SIBLING_PAIR=107697,107698 igdb=2369`.

## Deviations from Plan

None - plan executed as written. Environment note: Windows `sqlcmd` emits CRLF, so exact-line greps in the plan's verify blocks need `tr -d '\015'` on its output.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- `Database/39_FixPortalIgdbId.sql` exists (commit `15738e4`); `.planning/todos/pending/2026-10-27-igdbid-suspect-rematch.md` exists (commit `dcfdced`).
- Live Portal still reads IGDBId 14546; no write ran against GamePlannerDB.
