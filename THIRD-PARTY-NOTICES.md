# Third-party notices

The MIT license of this repository covers the code and data written for this project. It does not cover the game
Homura Hime or anything from it: this repository contains no game files, only the plugin, the Archipelago world and
tables of item/location names that the randomizer needs to talk about the game. Homura Hime belongs to its developers
and publisher; this project is an unofficial fan modification and is not affiliated with them.

## Included in this repository

- **Archipelago artwork** (`resources/ap_assets/`), (c) 2022 Krista Corkos and Christopher Wilson, licensed under
  [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/) (see `resources/ap_assets/LICENSE.txt`). Two of the
  icons are embedded in the plugin to mark Archipelago items. This material is not under the MIT license; it may be
  used and shared non-commercially with attribution.

## Used by the plugin (downloaded by NuGet when building, shipped in the release)

- **Archipelago.MultiClient.Net** - MIT License, (c) Jarno Westhof et al.
  <https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net>

## Required at runtime, not shipped

- **BepInEx 5** (LGPL-2.1) <https://github.com/BepInEx/BepInEx> and its bundled **HarmonyX** (MIT).
- **Archipelago** (MIT) <https://github.com/ArchipelagoMW/Archipelago>, which loads the `.apworld`.

## Used by the tests only

- **xUnit** (Apache-2.0), **Surity** (MIT), **Ben.Demystifier** (MIT).
