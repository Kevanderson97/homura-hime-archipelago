# To fix and to test

Working list for the plugin and the world. Newest items first within each section; move an item to "Done" with the version that fixed it.

## To fix

- **Quest item pop-up for items that were not found.** Doing the Nurikabe's Hat quest showed the "found the hat" picture
  although the hat was not received (it is a randomized item now). Probably the same for the other quest items. Find the
  pop-up and decide whether to hide it for randomized quest items or to show the real reward. Noted 2026-11-03, not started.
- **Small hitch on the very first walk-in at a Soul Core line** (the line before Mamiya, probably the first toast being built
  rather than the trigger lookup, which is made while the scene loads). A throwaway toast at connect would warm it up; the log
  could also say how many triggers were looked up and how long it took.
- **Skip Tutorial prompt throws.** `SkipTutorialOption.ShowPrompt` hit a `NullReferenceException` (`Component.get_transform`)
  when the option was chosen right before a teleport to the title screen. Check whether the prompt object can be gone by then.
- **Error after teleporting to the title screen:** "Can't remove UtageDialogCommander (Script) because DialogRTHook (Script)
  depends on it". Find out if it is ours (the debug teleport) or the game's.

## To test in the game

Nothing at the moment.

## Planned

- The journals tab showing the hinted items of our world ("X's item located at Y"). Runtime item definitions work (tested: clone a
  `Codex_Important_*` definition, `Initialize`, register with a fixed id, text in the `HH_Codex` string table); open: keep the
  generated entries out of the save, and whether the real journals hide only while connected.
- Some quests in the game have you fetch items that aren't actually inventory items (e.g. the quest to collect the 3 relic fragments
  from the hexed dolls in world 2). Those could be expanded to have an actual inventory item instead of
  being handled entirely through quest progression
- A nicer, more stylized UI for connecting to the AP world
- More polish on some of the quests that are related to items in the AP.

## Ideas

- Give the player the combo moves and skills Dark Homura Hime uses.
- Player skin to use Dark Homura Hime's model and blue flames
- Ship the multiworld generation sweep (`multi_sweep.py`) as an optional tool.

## Done

### 0.4.1

- **Soul Cores gate the Chapter 6-8 boss fights.** Five items of type `soul_core` (always shuffled) and five hub locations
  (`soul_core/N`, the vanilla `InventoryOperator_SoulCore` hand-overs, redirected by `HandOverPatch`). The hub shows the n-th story
  screen after the n-th cleared world, so the n-th location is checked after the n-th world cleared, whichever world that is (seen
  in the game: Chapters 6, 7, 8 cleared first gave the cores of Levels 1, 2, 3); the logic says `cleared >= n` (the Climax counts)
  and the data field is `cleared`. The way to each fight needs the core: `SoulCoreDoorPatch` (interact press at a door whose
  PortalController leads to the fight: Ling Ling, Nozomi, the Area 2 door before Yumiko & Kumiko's Ikai, and the door in that Ikai),
  `SoulCoreWalkInPatch` (walk-in trigger areas: the memory tree in the Level 5 Ikai that starts Agasa's fight, and the line across
  the end of Chapter 8's last area before Mamiya; both are `PlayMakerTriggerEnter`, and refusing the trigger keeps its state machine
  out of `Finish`) and a check in `TeleportPatch` for everything else. The message is a toast (the game's own one-button box draws in
  world space and cannot be seen). The trigger lookups are made while a Chapter 6-8 scene loads (`SoulCoreGate.Init`). Seeds of world
  0.4.0 have no cores, so the gate stays open for them (`SlotData.SoulCores`). Shops show the cores through the game's own item
  definitions.
- **Goal `return_all_soul_cores`.** Option `goal` (`all_worlds` default): the Finale opens once Chapters 6, 7 and 8 are cleared.
  World: `_can_enter_finale`; slot data `goal`; plugin: `WorldRules(finaleNeeds)`, `SlotData.Goal`, `Playthrough.ReachedFinale(rules)`
  (the save gate of the Kill ending). It only shortens the game when Chapters 6-8 can come before the others (a randomized order, or
  freeform keys). The Chaos preset uses it.
- **`optional_locations` replaces `encounter_rewards`; `encounter_clears` is gone.** Every fight is always a location; the toggle
  covers the 78 optional locations (`is_optional`). Off: they are not locations and the fixed items they held leave the pool
  (`_items_of_missing_locations`).
- Options presets (Standard, Balanced Random, Chaos), the bug report page, packaging with Archipelago's "Build APWorlds" component.
- **Generation failures looked into.** The solo failure (seed 60606, 2,500 combinations) no longer reproduces: the sweep passes
  with 7,500 tests. The local / non-local failures are impossible requests, not bugs: in 25 of 150 random multiworlds a player sent
  more items away than the other worlds had spots free of their own local items (checked by counting the pools); `local_items`
  alone never failed, and no configuration failed with neither list. Documented in the game doc; `test_local_items.py` guards it.
- Generation speed compared with Hollow Knight (about 0.04 s against 0.5 s for one player, 0.4 s against 3.8 s for eight, 3.2 s
  against 18.9 s for 32). The sweeps (single player and multiworlds of 2-6 players) pass.

### 0.4.0

- **The Climax as a world of its own.** Shuffled like a chapter, never first, needs one cleared chapter, own item "Climax Unlock",
  the finale needs it. Plugin: nine worlds, no prelude offset in the hub progress (2 per world). Old 0.3.x seeds are refused.
- **Level keys: `off`, `linear_progression`, `freeform_progression`** (option `level_unlock_items`; `true` in an old YAML means
  freeform). Linear: one "Progressive Level Key" item, nine in all. The plugin counts them in the session state (`ProgressiveKeys`).
- **`level_order`: `off` / `balanced` / `full`** (the old `vanilla` / `randomized` still work). With freeform keys the keys follow
  the order.
- **Hub: the Torii gate opens the Chapter Select with freeform keys** (in every hub stage; it calls the game's own
  `MainMenuAndDisplayPanelHandler.OpenChapterPortal`, like the post-game gate). **Post-mission report and clear reward** show the
  world that was just cleared, not the story's chapter.
- **Traps: Puffer Swarm (`puffer_swarm_traps`, default 20) and Hexed Doll Trio (`hexed_doll_trio_traps`, default 5).** Swarm: 4 flying
  bombs + 4 flying spiky puffers at 6-20 m. Trio: Cursed Doll A, B and C (the three colours) as a group on the ground, 4-8 m, only
  while the player stands on solid ground (`PlayerApi.IsOnSolidGround`: isStableGround and not isAir; a grind reports air and no
  control, so the trap waits). The game has no prefab of them, so `TrapSpawner` loads a source scene additively once per session per
  trap kind, copies the enemies into a hidden DontDestroyOnLoad holder and unloads the scene; a trap copies from the holder in any
  scene. A single scene with everything exists (`Level_FinalP2_L3_A5_Main`) but loading it froze the game: do not use it. Traps are
  queued in memory (not saved).
  - The setup runs at launch, while the logos play (the black cover, the muted sound and the blocked menu input are commented out in
    `TrapSpawner`, search "temporarily off"). The source scenes are switched off the moment they load (`HideSourceScene`) and the
    sounds they start are stopped (`SceneSounds`). When a save or new game starts, the copies of traps the played game does not use
    are destroyed. The Hexed Dolls come from the training ground's prefabs (`Prefabs = true`), the puffers from `Level_4_Area_1_Main`.
    Candidates for lighter puffer sources: level63 (spiky only, 21 MB) and level46 (bomb only, 36 MB) in the data folder, not yet
    mapped to scene names.
  - `SetupLoadBlockPatch` blocks the save panel's Continue / NewGame / difficulty buttons and the start screen's NewGame,
    NewGameDirect, ContinueDirect and StartLevel while the setup runs ("Preparing AP World Settings, please try again in a moment").
  - No trap goes off in the hubs (HubWorld_*: enemies were buggy there); they stay queued until the player is in a scene named Level*.
  - Bomb puffers were frozen: `TrapSpawner.Aim` sets `target` = the player and `isTrack` = true. The spiky puffers
    (`HomingStabSummonObject`) hover in their Observe state and attack through a token system; they were left alone.
- **Saves load only while connected to their slot.** While the Continue panel is open, `SavePanelGate` greys out (the game's own
  `SaveViewData.SetFiltered`) every save whose record names a slot and seed other than the connection, or there is no connection. It
  follows the connection live, moves the selection to the first usable slot, and pins a banner (`MenuNotice`, drawn with IMGUI)
  naming the slots. `SavePanelHandler.Continue(int)` is only a backstop. Unbound saves load as before. This ends offline play of
  bound saves. `StartScreenManager.ContinueDirect` reads a fixed event slot (100) and is not guarded.
- **Cut-content guns.** The shotgun and Ann's Board break the large Konpeito boxes (`CutGunBreakBoxPatch` makes their damage boxes
  triggers when the bullet is shot). Missiles no longer hover around the spiky puffer (`MissileArrivalPatch`). The standard gun no
  longer stops firing after a shot (`MissileChargeCancelPatch` ran for every gun). Missile gun, round 3: charge sound 0.3 s for
  every orb; `MissileReleaseEffectsPatch` clears a released missile's flame and trail; `GunSoundPreload` prepares the samples once the
  game has been quiet for 2 s. Earlier rounds: release timer, charge-cancel cleanup, destroyed targets stopping the release, missiles
  staying put inside a domain, sounds, title-screen connection.
- Reconnecting to another slot from the title screen: a connection made there is no longer judged against the save that was played
  before (`SaveBinding.AtTitle`).
- All filler forced local (`local_items` with every currency and utility item) no longer fails for about 12% of the seeds ("No more
  spots to place 1 items"). The direct placement of local Konpeito now takes the places reached last first. It had nothing to do with
  `minimum_konpeito`; 2,400 attempts across minimums from 0 to 1,000,000 passed.
- Tested in the game: goal protections (a finished save with the final boss on the server completes; a title-screen replay on
  another server is refused), the 20000 and 30000 Konpeito items, Continue on a save from another seed is refused, a randomized
  order with level keys, non-default options, the hub after Chapters 6-8, the Phantom Blade skip, Level 4 Area 2 to the Ikai,
  offline play and reconnect end to end, vanilla save adoption.

### 0.3.4

- Item-checking NPCs, Nurikabe dialogue cut, hub arrival lift, Kill goal hook, goal protections, shard recombine.
