# To fix and to test

Working list for the plugin and the world. Newest items first within each section; move an item to "Done" with the version that fixed it.

## To fix

- **A key that a quest uses up is given back by the key-item repair.** The Flower quest's take-over
  (`InventoryOperator_TakeOver_小花`) removes the Flower, so `OwnedItems.IsPermanent` makes the repair give it back after the next
  reload or item batch once the quest is done. Harmless, but wrong: a key that a quest consumes needs a "spent" record, or an
  exception in the repair. (Noted 2026-11-14, together with `Progress/CountReceivedItems`, which makes the NPC item checks and
  the Soul Core gates count received items: `PlayerApi.HasOrRestore`, `NpcItemFlagPatch.GiveReceivedItems`.)
- **`handover/flower` fired four times across reloads of one save** in a player's log. The location is only recorded once
  (`ApClient.CheckLocation` ignores a repeat), but find out what repeats the hand-over. Noted 2026-11-14, not started.
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

- **The Blue Hexed Doll rule of the Hexed Doll Trio Trap** (noted 2026-11-19, built and deployed but not tried in the game).
  The Blue doll is the C one (`Enemy_CursedDoll_C_Default`, codex `Codex_Enemy_CursedDollBlue`). A trio only goes off while no
  Blue doll is alive: the Blue doll of our own trios is tracked from the spawn (`OurBlueDolls`), a Blue doll of a normal fight is
  found by a scene search (`NoBlueDollBlocks`). Send many `Hexed Doll Trio Trap` (the batch script sends 15) and check that only
  one trio is out at a time, the log says `HexedDollTrioTrap waits: ...`, and that the dead trap enemies are destroyed instead
  of piling up in the scene (`ForgetEnemy`, was over 250 switched-off objects).
- **Finishing the Level 1 Area 1 quest checks the Flower hand-over** (`QuestCompletionPatch`, new, not tried): with
  `skip_early_macguffin_checks` off, give the Flower with `!getitem`, finish the doll and flower quest without taking the story's
  Flower, and the log should say `Quest ... finished without Level 1 Area 1: Pretty Little Flower: checking it now`.
- **`skip_early_macguffin_checks`** (new world option, on by default, tried only in the world tests): with Chapter 1 first, a new
  seed has no Flower item and no `Level 1 Area 1: Pretty Little Flower` location, and the story hands the Flower over as in the
  base game. The plugin needs no change for it, but a game with it has not been played.

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

### 0.4.4

- **Large batches over frames.** `ItemGranter.ItemsPerFrame` = 5; traps: `SpawnLimiter` (2 per 5 frames, by frame), `ActiveTraps` (at most 6 traps out, a trap counts until all its enemies are defeated, forgotten at a scene change), dead trap enemies destroyed (`ForgetEnemy`), and the Hexed Doll Trio Trap waits while a Blue doll (the C one, `Codex_Enemy_CursedDollBlue`) is alive.
- **Key items and Soul Cores are repaired** like equipment (`OwnedItems.IsPermanent`), after a load and 3 s after a batch; Soul Core gates and NPC item checks count received items (`Progress/CountReceivedItems`, `PlayerApi.HasOrRestore`).
- **Finishing the Level 1 Area 1 quest checks `handover/flower`** (`QuestCompletionPatch`, `QuestFetch.Completion`).
- **World option `skip_early_macguffin_checks`** (on): the Flower is left to the story when Chapter 1 comes first (no item, no location, an event for the logic). The game tests' seed turns it off.
- Not tried in the game when released: the Blue doll rule, the quest completion, the option in a played game (see "To test in the game").

### 0.4.3

- **Connection screen in the game's style** (`ConnectionView`, `GameStyle`): a uGUI canvas built at runtime, with the sprites and fonts taken from the title scene's `SaveCanvas` (flat colours as the fallback). Three cards (server, slot, password), Connect / Disconnect buttons (Disconnect greyed and skipped while there is nothing to disconnect), the status along the top, a small corner label (a black bar only while not connected, faint white text connected). `FormFocus` holds the selection rules (tested), `ConnectionViewModel` what the view shows.
- **Server window and command box**: `ApClient.ServerLog` (`ConsoleLog`), `Say`, `ListenToMessages` (the library only parses the server's messages while something listens, so the form listens only while it is open). Greyed and unselectable while not connected.
- **Controller**: `ControllerNav` (D-Pad / left stick, A, B), Start closes (`StartButton`), the chord Start + Select + LB + RB + LT + RT opens (`ControllerChord`, `RisingEdge`). Read through the Input System by reflection.
- **Reconnect no longer re-grants items**: `Rebind` applied the stored progress of the save file whenever the state had been cleared by `Connect`; now only on a load, a different save or a different slot (`SaveBinding.ShouldApplyPlaythrough`).
- **Death menu** (`DeadMenuGuard`, `DeadMenuPatch`) and generic **Death Link** messages (`DeathCause`, `DamageSourcePatch`).
- Slot data of real 0.4.1 and 0.4.2 seeds is read by this plugin (`SlotDataCompatibilityTests`). World: version number only.
- Tested in the game: the screen with keyboard and controller, the greyed Server window, `!collect` and other commands, reconnecting (items and offline checks), Death Link, Esc / Start.

### 0.4.2

- **Universal Tracker support.** The Waimea report (0.4.1) failed `check-ut` for 67 of 500 seeds: UT regenerates the world with other random draws, and the currency scales the logic uses (`money_scale`, `petal_scale`, `senbei_scale`, `senbei_unit`) come from the random filler of the pool, so Harumi's stages (and twice a shop) were in logic at other times. They are in the slot data now and restored in the regeneration (`SCALE_SLOT_DATA`); the world sets `ut_can_gen_without_yaml`. `test_universal_tracker.py` replays seeds sphere by sphere against a regeneration, and runs the tracker's own core when it is installed. The 67 failing yamls and 1,900 random ones pass the tracker's `check-ut` hook, with and without a yaml. Passes the full test bench of the world index.

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
