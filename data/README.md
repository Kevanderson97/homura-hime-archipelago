# Data contract

The tables the Archipelago world and the BepInEx plugin both read. Nothing here is hand-edited except where noted.

| File | Role |
|------|------|
| `locations.json`, `items.json`, `regions.json` | **Generated** by `tools/build_data.py` (and copied into `apworld/homura_hime/data/`). The plugin embeds `items.json` and `locations.json` as resources. |
| `id_registry.json` | The frozen Archipelago ids. Only ever appended to; an item keeps its id when its name changes. |
| `localized_names.tsv` | The names the game shows for items and journal notes (`<code name><TAB><name>`). Hand-maintained. |
| `custom_texts.json` | Names/descriptions the plugin shows for cut-content items the game has no text for. Embedded in the plugin. |
| `chapter_select.md` | Notes on the game's Chapter Select entries; the plugin's `ChapterData.cs` mirrors it. |
| `locations_chests.json`, `locations_senbei.json`, `locations_journals.json`, `battlerounds.json` | What the game's scenes contain (position, loot table, ...). Inputs of `build_data.py`, produced by the scripts in `tools/extract` and kept in the repository so the data can be rebuilt without owning the game. |

To change a price, a reward or a chapter layout, edit `tools/build_data.py` and run it; do not edit the generated files.
