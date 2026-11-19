Gameplay release: large batches of items and traps are spread out to avoid frame hitches, key items and Soul Cores that go missing are given back, and a new option leaves the first Pretty Little Flower to the story. See README for installation instructions.

**Update the plugin and the .apworld together.** Seeds from 0.4.x still play with this plugin (the new option only applies to seeds generated with this world; an older seed keeps the Flower as an item), and saves and server records are not touched.

### New:

**Skip Early Macguffin Checks (`skip_early_macguffin_checks`, on by default):**

- When Chapter 1 is the first world you play, the Pretty Little Flower is left to the story. It is not an item of the multiworld, its hand-over in Level 1 Area 1 is not a location, and you get the Flower the way the base game gives it. The logic counts on it from the start of the chapter.
- It does nothing when another world comes first (a randomized order, or a freeform key setup that starts elsewhere), or when you turn it off: then the Flower is shuffled like the other key items and its hand-over is a check.
- It is part of the Standard preset and of the sample YAML.

**Large batches of items and traps are spread over several frames:**

- Received items are handed to the game 5 per frame, in the order you received them, instead of in one long frame. Reconnecting after a long time away, or a `!release`, no longer freezes the game.
- Traps are all read in at once, but go off at a limited pace: at most 2 traps in any 5 frames, and at most 6 traps out at once. A trap counts until every enemy it spawned has been defeated (or left behind when you change scene); only then does the next one go off.
- A Hexed Doll Trio Trap waits while a Blue Hexed Doll (Aoi) is alive. The Blue doll shields the enemies near it, so two of them in one fight shield each other and cannot be killed.
- Defeated trap enemies are removed from the scene. The game only switches a dead enemy off, so hundreds of them used to pile up (over 250 in one scene) and cost frame rate.

**Soul Core gates and NPC item checks count what the multiworld has sent:**

- A Soul Core door, trigger or teleport, and an NPC that asks you for an item (Nurikabe, the elevator, the Level 1 house), no longer refuse you for an item the multiworld says you have but that is missing from your inventory. The item is given at that moment and the quest goes on.
- It can be switched off with the config setting `Progress` / `CountReceivedItems`.

### Fixes:

- **Key items and Soul Cores that went missing are given back.** Equipment already was; now the seven key items (Pretty Little Flower, Elevator Lever, Nurikabe's Shoe, Hat and Gloves, Fresh Fish, Inugami Certificate) and the five Soul Cores are too. The check runs when a save loads and a few seconds after a batch of items has been handed over. This fixes a player whose Flower was missing after many items arrived at once.
- **Finishing the Level 1 Area 1 doll and flower quest checks the Flower's location** if the hand-over was skipped (for example by finishing the quest with a Flower that was sent to you), so the check is not lost.

### Changes:

- The Standard preset, the sample YAML and the world's guide describe the new option.
- New tests: the pacing of items and traps, the trap limits, the Blue doll rule, the repair of key items (replaying the batch from a player's log), and the world option with and without Chapter 1 first. The game tests' seed keeps the Flower location, because they expect every location of the catalog.

### Known issues:

- A key item that a quest uses up (the Pretty Little Flower is taken by the house NPC) is given back by the repair after the next reload or batch of items. It does no harm, but is not vanilla behavior.
