---
created: 2026-11-05T17:17:19.804Z
title: IGDBId suspects from the Phase 6 Steam external_games scan
area: database
severity: minor
files:
  - GamePlanner.API/Managers/GameSyncManager.cs
---

## Problem

Found by the Phase 6 plan 06-02 read-only suspect scan (D-07). Portal (107693) was the confirmed case and is fixed by `Database/39_FixPortalIgdbId.sql`. This is the rest of the scan result. **Nothing below was changed in Phase 6** (D-07, D-10; CONTEXT Deferred Ideas).

## Method

All heuristics are read-only (SQL SELECTs on GamePlannerDB and IGDB v4 POST queries):

- (a) SQL: active UserId 2 rows with `SteamAppId IS NOT NULL AND FirstReleaseDate < '1995-01-01'`. Hits: Portal 107693 (fixed) and Strider 107764 (listed below).
- (b) IGDB: every active UserId 2 row with a SteamAppId (480 rows) was checked against IGDB's Steam mapping in batches of 50 uids: `fields uid,game; where uid = ("<a>","<b>",...) & external_game_source = 1 & game != null; sort game asc; limit 500;`. Result: 411 agree, 49 disagree (stored IGDBId differs from the mapped game), 0 unmapped, 0 multi-mapped, 20 rows still have a NULL IGDBId (handled by the 06-04 matcher, not suspects).
- (c) SQL: same-user duplicate-title groups (`GROUP BY UserId, IGDBId HAVING COUNT(DISTINCT Name) > 1` over active rows with an IGDBId). Result: none.

## Suspects (48, Portal excluded)

A disagreement is a **suspect, not proof**: remasters, editions and ports are often an intentional match (e.g. "Persona 4 Golden" 2012 vs the 2020 Steam release). Many of the stored ids are clearly wrong (e.g. NieR:Automata stored as "Untitled NieR:Automata Project", Stray stored as an unrelated fan game), others are debatable.

| Steam AppId | Games.Id | Name | Stored IGDBId | Steam-mapped IGDB game |
|---|---|---|---|---|
| 10 | 107677 | Counter-Strike | 266357 Counter-Strike (2003) | 241 Counter-Strike (2000) |
| 60 | 107682 | Ricochet | 368008 Ricochet (2022) | 7589 Ricochet (2000) |
| 130 | 107686 | Half-Life: Blue Shift | 285235 Half-Life: Blue Shift (2018) | 2549 Half-Life: Blue Shift (2001) |
| 500 | 107694 | Left 4 Dead | 60984 Left 4 Dead: Survivors (2014) | 122 Left 4 Dead (2008) |
| 7670 | 107704 | BioShock | 152660 BioShock (2010) | 20 BioShock (2007) |
| 12210 | 107710 | Grand Theft Auto IV: The Complete Edition | 27912 Grand Theft Auto IV: Complete Edition (2010) | 731 Grand Theft Auto IV (2008) |
| 13500 | 107711 | Prince of Persia: Warrior Within | 213052 Prince of Persia: Warrior Within (2004) | 837 Prince of Persia: Warrior Within (2004) |
| 13560 | 107712 | Tom Clancy's Splinter Cell | 134350 Tom Clancy's Splinter Cell (2003) | 910 Tom Clancy's Splinter Cell (2002) |
| 13570 | 107713 | Tom Clancy's Splinter Cell: Chaos Theory | 243021 Tom Clancy's Splinter Cell: Chaos Theory (2005) | 911 Tom Clancy's Splinter Cell: Chaos Theory (2005) |
| 34870 | 107724 | Sniper Ghost Warrior 2 | 138980 Sniper Ghost Warrior Contracts 2 (2021) | 1368 Sniper: Ghost Warrior 2 (2013) |
| 55150 | 107731 | Warhammer 40,000: Space Marine - Anniversary Edition | 186034 Warhammer 40,000: Space Marine - Anniversary Edition (2021) | 578 Warhammer 40,000: Space Marine (2011) |
| 205950 | 107744 | Jet Set Radio | 21004 Jet Set Radio (2012) | 1569 Jet Grind Radio (2000) |
| 208650 | 107745 | Batman™: Arkham Knight | 26041 Batman: Arkham Knight - 2008 Movie Batman Skin (2015) | 5503 Batman: Arkham Knight (2015) |
| 228300 | 107756 | Remember Me | 372230 Remember Me (2025) | 1827 Remember Me (2013) |
| 235210 | 107764 | Strider | 6969 Strider (1989) | 5333 Strider (2014) |
| 242760 | 107777 | The Forest | 99425 Märchen Forest: Mylne and the Forest Gift (2018) | 7504 The Forest (2018) |
| 268500 | 107792 | XCOM 2 | 86268 XCOM 2 Collection (2018) | 10919 XCOM 2 (2016) |
| 272060 | 107793 | Serena | 325536 Serena (2022) | 14528 Serena (2014) |
| 294860 | 107806 | Valkyria Chronicles™ | 75848 Valkyria Chronicles 4 (2018) | 18967 Valkyria Chronicles Remastered (2016) |
| 356190 | 107831 | Middle-earth™: Shadow of War™ | 52198 Middle-earth: Shadow of War Mobile (2017) | 27421 Middle-earth: Shadow of War (2017) |
| 435150 | 107868 | Divinity: Original Sin 2 | 154209 Divinity: Original Sin II - Divine Edition (n/a) | 11800 Divinity: Original Sin II (2017) |
| 448280 | 107870 | Job Simulator | 120688 Dirty Jobs Simulator (n/a) | 12596 Job Simulator: The 2050 Archives (2016) |
| 460790 | 107872 | Bayonetta | 279337 Bayonetta (2014) | 2136 Bayonetta (2009) |
| 460810 | 107873 | Vanquish | 220716 Vanquish (2022) | 3218 Vanquish (2010) |
| 524220 | 107889 | NieR:Automata™ | 391942 Untitled NieR:Automata Project (n/a) | 11208 NieR: Automata (2017) |
| 571740 | 107900 | Golf It! | 64369 WGT Golf (2013) | 30099 Golf It! (2023) |
| 632470 | 107920 | Disco Elysium | 335434 Disco Elysium (2025) | 26472 Disco Elysium (2019) |
| 678950 | 107933 | DRAGON BALL FighterZ | 144760 Dragon Ball FighterZ and Dragon Ball Xenoverse 2 Double Pack (2018) | 36911 Dragon Ball FighterZ (2018) |
| 678960 | 107934 | CODE VEIN | 347636 Code Vein II (2026) | 28168 Code Vein (2019) |
| 812140 | 107957 | Assassin's Creed Odyssey | 164698 Assassin's Creed Bundle: Assassin's Creed Valhalla, Assassin's Creed Odyssey, and Assassin's Creed Origins (2020) | 103054 Assassin's Creed Odyssey (2018) |
| 858260 | 107968 | Until You Fall | 99964 Transformers: Fall of Cybertron - Ultimate Edition (2013) | 115478 Until You Fall (2020) |
| 883710 | 107974 | Resident Evil 2 | 347128 Resident Evil 2 (2025) | 19686 Resident Evil 2 (2019) |
| 911400 | 107977 | Assassin's Creed III Remastered | 109532 Assassin's Creed III Remastered (2019) | 237701 Assassin's Creed III Remastered (2019) |
| 955050 | 107982 | Bright Memory | 134072 Bright Memory (2020) | 113739 Bright Memory: Infinite (2021) |
| 997070 | 107989 | Marvel's Avengers - The Definitive Edition | 259466 Marvel's Avengers Definitive Edition (2023) | 26950 Marvel's Avengers (2020) |
| 1097350 | 108009 | Weird West: Definitive Edition | 250504 Weird West: Definitive Edition (2023) | 127350 Weird West (2022) |
| 1113000 | 108010 | Persona 4 Golden | 2985 Persona 4 Golden (2012) | 234702 Persona 4 Golden (2020) |
| 1184370 | 108022 | Pathfinder: Wrath of the Righteous - Enhanced Edition | 226521 Pathfinder: Wrath of the Righteous - Enhanced Edition (2021) | 127254 Pathfinder: Wrath of the Righteous (2021) |
| 1332010 | 108044 | Stray | 222370 Fantasy Maiden Wars: Dream of the Stray Dreamer (2022) | 110248 Stray (2022) |
| 1384160 | 108054 | GUILTY GEAR -STRIVE- | 264825 Guilty Gear: Strive - Guilty Gear 25th Anniversary: Special Additional Color Pack (2023) | 125764 Guilty Gear: Strive (2021) |
| 1422450 | 108060 | Deadlock | 31699 Deadlock (2016) | 301298 Deadlock (n/a) |
| 1449560 | 108063 | Metro Exodus Enhanced Edition | 143292 Metro Exodus: Enhanced Edition (2021) | 37016 Metro Exodus (2019) |
| 2012320 | 108109 | Parallel Experiment | 285166 Parallel Experiment (2025) | 198114 Parallel Lab (2022) |
| 2050650 | 108110 | Resident Evil 4 | 145191 Resident Evil 4 (2005) | 132181 Resident Evil 4 (2023) |
| 2069040 | 108113 | Unboxing the Cryptic Killer | 247757 Unboxing the Cryptic Killer (2023) | 192087 Unboxing the Mind of a Cryptic Killer (2020) |
| 2124490 | 108117 | SILENT HILL 2 | 222485 Silent Hill 2 (2012) | 222341 Silent Hill 2 (2024) |
| 2592160 | 108133 | Dispatch | 376768 Dispatch: Episode 1 - Pivot (2025) | 325609 Dispatch (2025) |
| 3837340 | 108145 | FINAL FANTASY VII | 207028 Final Fantasy VII (2015) | 207026 Final Fantasy VII (2015) |

## Solution / Next steps

Each suspect needs a **per-row owner decision** (keep the stored id, or correct it with a numbered guarded, reversible script in the shape of `Database/38_FixHalfLifeIgdbId.sql` / `39_FixPortalIgdbId.sql`). None was changed in Phase 6. Candidate follow-up: a dedicated "re-match suspects" phase that presents these rows in the UI for confirmation instead of a blanket SQL fix.
