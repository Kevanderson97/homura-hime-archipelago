# Homura-Hime Archipelago plugin (BepInEx 5 + Harmony)

The game-side half of the randomizer: it receives items, sends location checks and keeps the game vanilla otherwise. The Archipelago half is in [../apworld](../apworld/README.md); the two share the tables in [../data](../data/README.md).

## Build and deploy

```
# needs the .NET SDK and the game with BepInEx 5 installed (the plugin is compiled against the game's own assemblies)
cd plugin
dotnet build -c Release
dotnet build -c Release -p:DeployToGame=true  # also copies the dlls to BepInEx\plugins\HomuraHimeArchipelago
# other install location: -p:GameDir="D:\Games\Homura Hime"
```

The game keeps running with the plugin it started with; restart it to load a new build. Close it before building if memory is
tight (the game uses about 4 GB).

## Layout

```
Plugin.cs                 startup, config, Harmony patching (every [HarmonyPatch] class is applied on its own)
Archipelago/              ApClient (connection state machine, binding to the played save), ConnectionMonitor (reconnect),
                          CheckSender / Reconciler (checks both ways), GoalReporter, HintSender (shop hints), SlotData
                          (validated), ScoutCache, DeathLinkHandler + DeathLinkMachine, ServerAddress
Core/                     Catalog (items.json / locations.json), SessionState (per slot + seed, deferred atomic writes),
                          SessionStore, SaveBinding (which game save belongs to which slot), Playthrough, PlayerProgress,
                          SaveState (progress flags without a session), TickGuard, SceneClock, MainThread,
                          GameApi (reflection into the game), ChapterAccess + WorldProgress (level order, unlock items,
                          count-based hub progress), TutorialTracker, Hierarchy
Items/                    ItemGranter (received items -> game), PlayerApi (inventory / character access)
Patches/                  one file per hooked game system (locations, shop, stage rewards, deaths, chapter select, ...)
UI/                       Toasts + HudNotifier (game-style strips), ConnectionPanel (keyboard driven), ItemIcons, Assets
```

Rules the code follows: the game is only reached through reflection (`AccessTools`), so a game update logs a warning instead of
breaking the build; network callbacks only enqueue work for the main thread (`MainThread.Run`); every hook leaves the game
vanilla unless the played save belongs to a slot (`ApClient.Active`; `Catalog` only knows the slot's locations).

Saves: `saves.json` records, for every game save slot, the Archipelago slot and seed it belongs to and what that save had
received. It is written when the game saves (`OnSaveStarted`) and applied when the game loads (`GameSaveManager.Load`), so an
older save also brings back the older received-item index. A save with another slot/seed is refused (randomizer off, message
shown); a save without a record is adopted by the connected slot; a bound save keeps working offline from the slot data and scout
table stored in the session file.

## Status

Verified in game: connecting, item granting, chest / Senbei / journal / flower pickups, shop-less location checks reaching the
server, game-style notification strips, chapter select filtering (vanilla order).

Implemented, **not yet tested in game** (see the checklist the last session ended with): shop purchases, hand-overs, Harumi,
trials / tournament / festival stall, stage clears and the tutorial reward, encounter clears, Phantom Blade script check,
level order + unlock items + count-based hub progress, goal, DeathLink, skip tutorial, Senbei logo visuals, locked-chapter
teleport guard.

Added since: the save record lives in the game save (`Core/ApSaver`), hub boards lead to the seed's next world (`Core/HubBoards`),
no hub-currency or reward-board payouts, journals are kept silently, text audit + custom texts (`Core/TextAudit`, `data/custom_texts.json`),
cut-content guns (names, missile HUD fix, game sound samples via `Core/GameSamples`), shop display fixes.

## Tests

`tools/run_tests.ps1` runs the world tests (pytest), the plugin unit tests (`plugin.tests`) and the in-game tests
(`plugin.gametests`, Surity); see [../docs/TESTING.md](../docs/TESTING.md).

## Controls

F10 opens the connection form (the title screen shows it read-only until then; while open, the game ignores the keyboard), Tab/arrows
switch field, Ctrl+V pastes, F11 connect, F12 disconnect.

## Config (BepInEx/config/com.homurahime.archipelago.cfg)

`Archipelago.Server/Slot/Password/AutoConnect`, `Archipelago.RememberPassword` (default off), `Archipelago.ShopHints` (can only turn the
world option off), `Notifications.ItemNotifications` (All / Important / Off), `Progress.MonotonicProgress`, `ChapterSelect.*`, `Debug.VerboseLog` (logs every
hook miss), `Debug.DisabledPatches` (comma separated patch class names, `ALL`, `NOTRACKER`) for troubleshooting.
