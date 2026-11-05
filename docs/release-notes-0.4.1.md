Feature release: the Soul Cores are items that open the Chapter 6-8 boss fights, a new goal that only needs Chapters 6-8, optional locations as one setting, and options presets. See README for installation instructions.

**Update the plugin and the .apworld together and generate a new seed.** Seeds from 0.4.0 still play with this plugin, but they have no Soul Cores and no new goal. Two options of the YAML changed (see below): a YAML that names `encounter_clears` or `encounter_rewards` has to be edited.

### New:

**Soul Cores are items:**

- Each boss of Levels 1-5 leaves a Soul Core, and Chapters 6-8 fight the same five bosses again. The five Soul Cores (Agasa's, Ling Ling's, Chirakuin's, Yumiko & Kumiko's and Mamiya's) are now items of the multiworld, and the way to each rematch needs the core of that boss.
- Without the core, the way does nothing and the game says "<Name>'s Soul Core required to continue". This covers the doors to Ling Ling and the Corpse Princess, Nozomi, Yumiko & Kumiko's Ikai and the Ikai's own door, the walk to the memory tree before Agasa, and the line across the end of Chapter 8's last area before Mamiya. Everything else that leads to those fights (the Chapter Select, automatic changes of scene) is refused as well.
- The hub used to hand the cores out when a world was cleared. Those five hand-overs are locations now ("Hub: Soul Core (1st World Cleared)" up to the 5th). The hub follows the number of worlds you have cleared, so the n-th core's location is checked after the n-th world you clear, whichever world that is. The cores are always shuffled, whatever `shuffle_key_items` says.
- Shops show the Soul Cores with their own picture and description.

**Goal (`goal`: all_worlds / return_all_soul_cores):**

- `return_all_soul_cores` opens the Finale as soon as Chapters 6, 7 and 8 are cleared (which takes every Soul Core). Chapters 1-5 and the Climax stay playable for their checks but are not needed. It shortens the game when Chapters 6-8 can come before the other worlds: with a randomized order, or with freeform level keys. In the story order without freeform keys every world comes before Chapter 8 anyway.
- The goal is still reported by the Kill ending, and a save has to have reached the Finale by the new rule.

**Optional locations (`optional_locations`, default on) replace `encounter_rewards`; `encounter_clears` is gone:**

- Every fight is a location now (110), always.
- `optional_locations` decides whether the optional content counts: the ambush fights and their chests, the glitched-spirit trials, the Kushinada head chases and the optional fights (78 locations in all, the same ones `optional_location_importance` is about). Off: they are no locations, the game hands out what it always did there, and the fixed items they held leave the pool.

**Options presets (the web page of the world):** Standard (the defaults), Balanced Random (balanced order, progressive keys, few traps) and Chaos (full random order, freeform keys, the Return All Soul Cores goal, most values random, Death Link on, cut content and advanced movement on).

**Packaging:** the .apworld is built with Archipelago's own "Build APWorlds" tool and keeps the tests out of the package; the world has a bug report page.

### Changes:

- Generation is fast: about 0.1 s for one player and about 0.4 s for eight (Hollow Knight takes about 2.6 s and 3.8 s on the same machine).
- The sample YAML is regenerated for this version.

### Known issues:

- The very first walk across the line before Mamiya can hitch slightly (the notification is drawn for the first time).
- Reporting the goal with `return_all_soul_cores` has not been tried through the Kill ending yet; the Finale opening has.
- Doing the Nurikabe's Hat quest shows the "found the hat" picture although the hat comes from the multiworld. Other quest items probably do the same.
