# API Coverage: IGDB v4 (api.igdb.com), Phase 6

> Every capability defaults to full coverage. Each opt-out below is an explicit decision with its reason.
> Phase 6 extends the existing IGDB integration with one new endpoint (`external_games`, D-09) and pages two existing ones (D-21). Steam and Twitch OAuth are unchanged in this phase.

| capability | decision | reason |
|---|---|---|
| games-by-id | INTEGRATE | |
| games-search | INTEGRATE | |
| games-batch-and-updated-since | INTEGRATE | |
| games-upcoming | INTEGRATE | |
| games-expansions-genres-themes-perspectives-modes-rating | INTEGRATE | |
| covers | INTEGRATE | |
| screenshots | INTEGRATE | |
| artworks | INTEGRATE | |
| external-games-steam | INTEGRATE | |
| external-games-other-stores | OPT-OUT | explicitly out of scope: v1.1 is analysis only for GOG/Epic/Xbox (REQUIREMENTS Out of Scope); Phase 10 decides the next store |
| external-game-sources | OPT-OUT | not needed: Steam = 1 was verified live and is used as a constant in the query |
| alternative-names | OPT-OUT | not needed: D-09 locks the matcher order (Steam mapping, exact name, first hit); alternative-name matching was not chosen |
| game-type | OPT-OUT | not needed: no Phase 6 query filters by game type (the old games.category field is not used anywhere) |
| videos | OPT-OUT | explicitly out of scope: only screenshots are browsable (D-13); a video display is a new capability |
| websites | OPT-OUT | not needed: the Steam store link comes from SteamAppId |
| involved-companies-and-companies | OPT-OUT | not needed in the app: queried read-only only as evidence for script 39 (plan 06-02) |
| platforms-and-release-dates | OPT-OUT | not needed: FirstReleaseDate comes from games.first_release_date |
| age-ratings | OPT-OUT | not needed yet: no v1.1 requirement |
| franchises-and-collections | OPT-OUT | not needed yet: no v1.1 requirement |
| game-time-to-beats | OPT-OUT | not needed: time-to-beat comes from HowLongToBeat (GameHLTBSyncWorker) |
| popularity-primitives | OPT-OUT | not needed yet: no v1.1 requirement |
| multiquery | OPT-OUT | not needed: per-endpoint POSTs with a 250 ms delay stay under the rate limit for this app's volume |
