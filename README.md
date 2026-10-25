# Black Friday Game Comp Tracker

A local Blazor Server app for running the Black Friday gaming tournament. It tracks games, players and scores, and puts a live leaderboard on the big screen.

## Run it

```powershell
dotnet run --project src/CompTracker
```

The app listens on `http://0.0.0.0:5080`. Open `http://localhost:5080` on this PC or `http://<this-pc-ip>:5080` from other devices. Use `ipconfig` to find the IPv4 address, or just scan the QR code in the dashboard's corner. That card's X hides it, and the QR button in the header brings it back. The address is detected automatically; if it picks the wrong network adapter, set `"JoinUrl": "http://192.168.1.18:5080/"` in `appsettings.json`.

The SQLite database (`src/CompTracker/comptracker.db`) is created and migrated automatically on first start. Backups are automatic (see below). To start fresh, stop the app and delete it.

### Allow other devices on the network (one time, admin PowerShell)

```powershell
New-NetFirewallRule -DisplayName "CompTracker" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow
```

## Pages

| Route | Purpose |
| --- | --- |
| `/` | Dashboard: leaderboard, a card per game (current leader + how many have played), a rotating "still to play" list, and a ticker of the latest scores along the bottom. Updates live when scores change. |
| `/games/{id}` | Live standings for one game (opened by clicking its dashboard card). |
| `/stats` | Printable results: a final-standings cover page plus one report sheet per player. "Print everything" prints one page each. No PIN needed. |
| `/stats/{playerId}` | One player's printable report: final place, points by game chart, game-by-game results. A highlights section (best finish, closest call, rival, points gaps) is built but hidden; set `ShowHighlights` in `Components/Reports/PlayerReportCard.razor` to bring it back. |
| `/admin/games` | Add, edit, reorder and deactivate games, and set each game's scoring type. Click a game to enter scores. |
| `/admin/games/{id}` | Enter or overwrite each player's score or time for that game. |
| `/admin/players` | Add, rename and deactivate players. |
| `/admin/points` | Global points per place (default 10, 9, … 1). |

## Scoring rules

- Each player has one score per game. Saving again overwrites it.
- Speedruns and low-score games rank ascending, high-score games rank descending.
- Ties share a place, and the next place is skipped (1, 2, 2, 4).
- Points come from the points table. Places past the end of the list get the last value, with a minimum of 1.
- The leaderboard sums points over active games. A tie on points is broken by the number of 1st places.
- Inactive players and games are hidden from standings, the leaderboard and the todo list, but their scores are kept.

## Sounds & celebrations

The dashboard reacts when a score is saved:

- **Every score:** the game's card glows and a short chime plays.
- **New leader in a game:** a full-screen "NEW LEADER" card with confetti and a fanfare.
- **New #1 overall:** a bigger "NEW TOURNAMENT LEADER" card with its own fanfare. If one score does both, they play one after the other.

A new leader only counts when they hold 1st **alone** and took it from someone else. Ties, keeping your own lead and the very first score don't count.

Sound is off by default. Turn it on with the speaker button on the dashboard; each browser remembers the choice. Browsers block audio until someone clicks the page, so after a reload the button turns gold ("Click to enable sound") until you click anywhere. On a TV/kiosk screen you can skip that by launching the browser with `--autoplay-policy=no-user-gesture-required`.

The sounds are synthesized by `tools/make_sounds.py`, which uses only the standard library. Edit it and run `python tools/make_sounds.py` to change them. The output goes to `wwwroot/sounds/`.

## Backups

While the app runs, it saves a snapshot of the database every 15 minutes, but only if something changed. It also saves one when the app shuts down (Ctrl+C). The **Backups** admin page lists the snapshots and has **Back up now** and **Download a fresh backup** buttons. Downloading works from a phone too, which is an easy way to get a copy off the PC.

Settings live in `appsettings.json`:

```json
"Backups": {
  "Folder": "backups",       // relative to src/CompTracker, or absolute, e.g. "E:\\CompTracker Backups"
  "IntervalMinutes": 15,
  "Keep": 200                // older snapshots are deleted
}
```

To restore a backup:
1. Stop the app.
2. Delete `comptracker.db-wal` and `comptracker.db-shm`.
3. Copy the backup over `comptracker.db`.
4. Start the app again.

## Fonts & icons

Both are bundled with the app (no internet needed at the party):

- **Electrolize** font by Cyreal, SIL Open Font License (`wwwroot/fonts/Electrolize-OFL.txt`).
- QR codes by **QRCoder**, MIT license (NuGet package).
- **Phosphor** "fill" icons, MIT license. They're inlined in `Components/Icon.razor`; use `<Icon Name="trophy" />`. To add one, copy the path from `@phosphor-icons/core/assets/fill/<name>-fill.svg`.

## Development

```powershell
dotnet test                                             # unit tests
dotnet tool restore                                     # restores dotnet-ef (local tool)
dotnet ef migrations add <Name> --project src/CompTracker --output-dir Data/Migrations
```

## Admin PIN

Admin pages ask for a PIN (`AdminPin` in `src/CompTracker/appsettings.json`, default `1619`). After you enter it, you stay unlocked while moving between admin pages. Opening the dashboard, or reloading the browser tab, locks admin again.

For real accounts later, all admin pages share `Components/Pages/Admin/_Imports.razor`. Adding `@attribute [Authorize]` there (plus ASP.NET Core Identity) protects them in one place.
