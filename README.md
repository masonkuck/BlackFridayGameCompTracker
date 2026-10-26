# Black Friday Game Comp Tracker

A local Blazor Server app for running the Black Friday gaming tournament. It tracks games, players and scores, and puts a live leaderboard on the big screen.

## Run it

**Easiest (Windows):** double-click `Start.bat`. Before it starts the app, it:
- installs the .NET 10 SDK if it's missing, through winget or Microsoft's install script;
- offers to add the firewall rule so phones can connect;
- offers to share the app online through a [dev tunnel](https://aka.ms/devtunnels/docs), so people off your Wi-Fi can follow along (see below).

SQLite needs no install; it's built into the app.

Or run it by hand with the .NET 10 SDK installed:

```powershell
dotnet run --project src/CompTracker
```

Change `"AdminPin"` and `"EventName"` in `src/CompTracker/appsettings.json` before your own event.

The app listens on `http://0.0.0.0:5080`. Open `http://localhost:5080` on this PC or `http://<this-pc-ip>:5080` from other devices. Use `ipconfig` to find the IPv4 address, or just scan the QR code in the dashboard's corner. That card's X hides it, and the QR button in the header brings it back. The QR code shares whatever address the dashboard is open on, so through a dev tunnel it shows the tunnel link; on `localhost` it shows the PC's network address instead. That address is detected automatically; if it picks the wrong network adapter, set `"JoinUrl": "http://192.168.1.18:5080/"` in `appsettings.json`.

The SQLite database (`src/CompTracker/comptracker.db`) is created and migrated automatically on first start. Backups are automatic (see below). To start fresh, stop the app and delete it.

### Allow other devices on the network (one time, admin PowerShell)

```powershell
New-NetFirewallRule -DisplayName "CompTracker" -Direction Inbound -Protocol TCP -LocalPort 5080 -Action Allow
```

### Share it online (dev tunnel)

Answer **Y** to the online-link question in `Start.bat` (or run `tools\tunnel.ps1` on its own). The first time, it:
- installs the dev tunnels CLI;
- has you sign in with a Microsoft or GitHub account;
- creates a tunnel labelled `comptracker` that allows anonymous visitors.

After that it reuses the same tunnel, so **the link stays the same**. Each start pushes its 30-day expiry back. The tunnel runs in its own minimized window; close that window to take the link offline.

Anyone with the link can view the dashboard. Admin pages are protected only by the PIN, so change it before sharing widely. Visitors see a one-time Microsoft warning page and click Continue.

## Pages

| Route | Purpose |
| --- | --- |
| `/` | Dashboard: leaderboard, a card per game (current leader + how many have played), a rotating "still to play" list, and a ticker of the latest scores along the bottom. Updates live when scores change. |
| `/games/{id}` | Live standings for one game (opened by clicking its dashboard card). |
| `/brackets/{id}` | Live bracket for a bracket game: winners and losers brackets, grand final, up-next matches and places. Bracket games' dashboard cards open this instead. |
| `/stats` | Printable results: a final-standings cover page plus one report sheet per player. "Print everything" prints one page each. No PIN needed. |
| `/stats/{playerId}` | One player's printable report: final place, points by game chart, game-by-game results. A highlights section (best finish, closest call, rival, points gaps) is built but hidden; set `ShowHighlights` in `Components/Reports/PlayerReportCard.razor` to bring it back. |
| `/admin/games` | Add, edit, reorder and deactivate games, set each game's scoring type, and mark games just for fun. Click a game to enter scores. |
| `/admin/games/{id}` | Enter or overwrite each player's score or time for that game. For a bracket game: draw it, drag players to rearrange matchups, lock it in, and pick winners right on the bracket. |
| `/admin/players` | Add, rename and deactivate players. |
| `/admin/points` | Global points per place (default 10, 9, … 1). |

## Scoring rules

- Each player has one score per game. Saving again overwrites it.
- Speedruns and low-score games rank ascending, high-score games rank descending.
- Ties share a place, and the next place is skipped (1, 2, 2, 4).
- Points come from the points table. Places past the end of the list get the last value, with a minimum of 1.
- The leaderboard sums points over active games. A tie on points is broken by the number of 1st places.
- Inactive players and games are hidden from standings, the leaderboard and the todo list, but their scores are kept.
- A game marked **Just for fun** (checkbox in the add/edit game window) is tracked like any other game (standings, brackets, todo list, ticker) but is worth no points: it's left out of leaderboard totals, games played and the printable stats.

## Bracket games

Pick **Bracket (double elimination)** as a game's scoring type for a head-to-head game. On the game's admin page:

1. **Draw the bracket.** Every active player goes into a random bracket. Byes fill it out to 4, 8, 16… and go to the top seeds.
2. **Rearrange (optional).** Drag a player onto another first-round slot to swap them (or tap one, then the other, on a phone). **Redraw** reshuffles everyone.
3. **Lock it in.** Once the matchups look right, **Lock in bracket**. Until the first result, **Unlock matchups** lets you go back and rearrange.
4. **Pick winners.** Click the player who won a highlighted match and confirm. Losers drop to the losers bracket; a second loss knocks a player out. The losers-bracket champion has an arrow up into the grand final against the winners-bracket champion, with a reset match if the losers-bracket champion wins the first one.

To fix a mistake, click the loser of a match to flip it, or use the undo button on it. Any later match that depended on it is cleared, and the app warns you first. **Reset results** clears every result but keeps the matchups.

A bracket game only shows on a player's todo list while they have a match they can play right now, not while they're waiting for an opponent or once they're out.

A bracket counts toward the leaderboard like any other game: places come from how far you got (champion 1st, grand-final loser 2nd, then by which losers round you went out in; players who go out in the same round tie, e.g. 5th–6th become 5th & 5th), and then the normal points table applies. A place is final as soon as someone is knocked out. Crowning the champion gets its own "Bracket champion" celebration.

Switching a game to or from Bracket is blocked while it has scores or a bracket, since one can't be turned into the other.

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
dotnet watch --project src/CompTracker                  # run with hot reload (Ctrl+R in its window forces a restart)
dotnet test                                             # unit tests
dotnet tool restore                                     # restores dotnet-ef (local tool)
dotnet ef migrations add <Name> --project src/CompTracker --output-dir Data/Migrations
```

## Admin PIN

Admin pages ask for a PIN (`AdminPin` in `src/CompTracker/appsettings.json`, default `1619`). After you enter it, you stay unlocked while moving between admin pages. Opening the dashboard, or reloading the browser tab, locks admin again.

For real accounts later, all admin pages share `Components/Pages/Admin/_Imports.razor`. Adding `@attribute [Authorize]` there (plus ASP.NET Core Identity) protects them in one place.
