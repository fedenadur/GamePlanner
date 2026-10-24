---
created: 2026-10-24T22:30:00.000Z
title: Steam OpenID follow-ups (S5 state binding, S6 rate limit, S7 SteamId/Email uniqueness)
area: auth
severity: minor
files:
  - GamePlanner.API/Controllers/AuthController.cs
  - GamePlanner.API/Services/SteamAuthService.cs
  - Database/
---

## Problem

Logged, not built, per Phase 5 decision D-09 (plan 05-04 fixed S1-S4; these are the larger items).

- **S5 - no `state` / browser binding on the OpenID round trip (login CSRF).** Nothing ties the
  callback to the browser that started the login, so an attacker can make a victim's browser
  complete the attacker's Steam login. Fix: a nonce cookie set in `SteamLogin`, carried inside
  `openid.return_to`, and checked in `SteamCallback`. Low impact for a single-user personal app.
- **S6 - no rate-limit policy on `/api/auth/steam/callback` and `/api/auth/steam/exchange`.**
  The code is single-use and lives 5 minutes, so abuse is bounded. Also, `?code=` stays in the
  browser history until the SPA navigates away. Option: `history.replaceState` after the exchange.
- **S7 - uniqueness of Steam-created users.** Research assumed there was no unique index on
  `Users.SteamId`; the dev DB actually has the filtered `UX_Users_SteamId` (`SteamId IS NOT NULL`),
  so a double submit surfaces as a `DbUpdateException` (500) from `SaveChangesAsync` in
  `HandleCallbackAsync`, not a duplicate row. Fix: catch it and re-read the user.

## Newly found while building the 05-04 tests

`UQ_Users_Email` is a plain (unfiltered) UNIQUE constraint, and SQL Server treats NULL as a single
value in it. Because Steam-created users have `Email = NULL`, **at most one email-less user can
exist**: the second distinct first-time Steam login fails with "Violation of UNIQUE KEY constraint
'UQ_Users_Email' ... duplicate key value is (<NULL>)" and the callback returns a 500. Today it is
hidden because the owner's Steam row carries a synthetic `<steamid>@steam.local` email and no other
Steam user exists. 05-04 tests rely on this (only one probe user at a time, deleted in `finally`).

Fix options (needs a numbered SQL script, so out of the Phase 5 hardening scope): replace
`UQ_Users_Email` with a filtered unique index `WHERE Email IS NOT NULL`, or give Steam users the
same synthetic `<steamid>@steam.local` email the owner's row has.

## Solution / Next steps

1. Decide whether this app will ever have a second Steam user; if yes, fix the `UQ_Users_Email` issue first.
2. S7: catch `DbUpdateException` on the find-then-insert and re-read by SteamId.
3. S5 and S6 only if the app is ever exposed to more than its owner.
