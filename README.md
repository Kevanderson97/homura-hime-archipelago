# Homura Hime for Archipelago

An unofficial [Archipelago](https://archipelago.gg) multiworld randomizer for **Homura Hime**. Every item the game hands
out (chests, Senbei, journals, shop slots, stage rewards, quest hand-overs, optionally every fight) becomes a check whose
reward is shuffled into the multiworld, and the rewards of other players' games arrive in yours.

It has two halves that talk through the Archipelago server:

| Part | What it is | Folder |
|------|------------|--------|
| **Plugin** | A [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin for the game: connects to the server, sends checks, grants received items, shows notifications. | [`plugin/`](plugin/README.md) |
| **World** | A Python `.apworld` for Archipelago 0.6.x: the items, locations, logic, options and the generator. | [`apworld/`](apworld/README.md) |

This project is not affiliated with the developers or publisher of Homura Hime. You need your own copy of the game
(Windows, Steam); this repository contains no game files.

## Playing

Everything you need is in the [latest release](../../releases/latest):

| File | Purpose |
|------|---------|
| `HomuraHimeArchipelago-plugin-<version>.zip` | The plugin and the Archipelago client library it needs |
| `homura_hime.apworld` | The world, for generating a multiworld |
| `Homura-Hime.yaml` | A sample player file listing every option |

1. Install **BepInEx 5** (x64, 5.4.x) into the game folder (the one with `HomuraHime.exe`): extract the BepInEx release
   there, start the game once, close it.
2. Extract `HomuraHimeArchipelago-plugin-<version>.zip` into the game folder. It contains `BepInEx/plugins/HomuraHimeArchipelago/`.
3. Install [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases) 0.6.6 or newer and put
   `homura_hime.apworld` into its `custom_worlds` folder (or double-click it with the Archipelago Launcher).
4. Edit `Homura-Hime.yaml` (set `name:` and the options you want) and generate a multiworld with it, or send it to the
   host of your multiworld.
5. In the game, press **F10** on the title screen, enter server, slot name and password, and press **F11** to connect.
   Start a new game for a new seed.

The full guides are in [`apworld/homura_hime/docs/`](apworld/homura_hime/docs/): [setup](apworld/homura_hime/docs/setup_en.md)
and [what is randomized and how the logic works](apworld/homura_hime/docs/en_Homura-Hime.md). The plugin and the world must
have the same major.minor version (both are 0.3.x in this release).

## Building from source

Requirements: the game with BepInEx 5 installed (the plugin is compiled against the game's own assemblies), the .NET SDK
and Python 3.

```powershell
# everything a release contains, into dist/
tools\build_release.ps1 -GameDir "D:\Games\Homura Hime"     # GameDir defaults to the usual Steam location

# or piece by piece
dotnet build plugin -c Release -p:GameDir="D:\Games\Homura Hime"
python tools/build_apworld.py
```

## Repository layout

```
apworld/      the Archipelago world (homura_hime/), its tests and a sample yaml
plugin/       the BepInEx plugin
plugin.tests/ plugin unit tests (xUnit, run without the game)
plugin.gametests/  in-game tests (run inside a headless game with Surity)
data/         the shared data contract: items, locations, regions, ids, and the scene tables they are built from
tools/        build_data.py (data generator), build_apworld.py, build_release.ps1, run_tests.ps1,
              extract/ (reads the installed game to refresh the scene tables)
docs/         TESTING.md
resources/    Archipelago artwork used for item icons (own license, see below)
```

How the data flows: `tools/extract` reads the game once -> `data/locations_*.json` -> `tools/build_data.py` ->
`data/{locations,items,regions}.json` -> embedded in the plugin and copied into the world. Details in
[`data/README.md`](data/README.md).

## Testing

`tools/run_tests.ps1` runs the world tests, the plugin unit tests and the in-game tests. See [`docs/TESTING.md`](docs/TESTING.md).

## License

The code and data of this project are released under the [MIT License](LICENSE). The Archipelago artwork in
`resources/ap_assets` is licensed separately under CC BY-NC 4.0 and the third-party components are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
