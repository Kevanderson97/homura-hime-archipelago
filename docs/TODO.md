# To fix and to test

Working list for the plugin and the world. Newest items first within each section; move an item to "Done" with the version that fixed it.

## To fix

- **Quest item pop-up for items that were not found.** Doing the Nurikabe's Hat quest showed the "found the hat" picture
  although the hat was not received (it is a randomized item now). Probably the same for the other quest items. Find the
  pop-up and decide whether to hide it for randomized quest items or to show the real reward. Noted 2026-11-03, not started.
- **Skip Tutorial prompt throws.** `SkipTutorialOption.ShowPrompt` hit a `NullReferenceException` (`Component.get_transform`)
  when the option was chosen right before a teleport to the title screen. Check whether the prompt object can be gone by then.
- **Error after teleporting to the title screen:** "Can't remove UtageDialogCommander (Script) because DialogRTHook (Script)
  depends on it". Find out if it is ours (the debug teleport) or the game's.
- **One solo generation failure in 2,500 option combinations** (seed 60606, sweep `solo4`). Not investigated.
- **Local and non-local item lists that need more items than a world has room for** can still fail to generate (about 11 in
  300 two-to-many-player test multiworlds). Left alone on purpose; document it in the setup guide if it comes up.

## Built, waiting for a deploy and a test in the game

- **The Climax as a world of its own (0.4.0).** Shuffled like a chapter, never first, needs one cleared chapter, own item
  "Climax Unlock", the finale needs it. Plugin: nine worlds, no prelude offset in the hub progress (2 per world). Old 0.3.x
  seeds are refused. To check: Chapter Select and the hub boards with the Climax in the middle of a random order, the hub
  stage after clearing it first/last, the finale gate, Continue on a save of the new version.
- **Level keys: `off`, `linear_progression`, `freeform_progression`** (option `level_unlock_items`; `true` in an old YAML means
  freeform). Linear: one "Progressive Level Key" item, nine in all. The plugin counts them in the session state
  (`ProgressiveKeys`). To check: Chapter Select and the hub boards with linear keys and a shuffled order, a freeform key
  received while playing another world, an old YAML with `true`.
- **Traps: Puffer Swarm (`puffer_swarm_traps`, default 20) and Hexed Doll Trio (`hexed_doll_trio_traps`, default 5).** Swarm: 4 flying
  bombs + 4 flying spiky puffers at 6-20 m. Trio: Cursed Doll A, B and C (the three colours) as a group on the ground, 4-8 m, only while the player stands on solid ground (`PlayerApi.IsOnSolidGround`: isStableGround and not isAir; checked on the Level 4 Ikai Part 1 rail: a grind reports air and no control, so the trap waits).
  The game has no prefab of them, so `TrapSpawner` loads a source scene additively once per session per trap kind
  (`Level_4_Area_1_Main` for puffers, `Level_3_Area_6_Main` for dolls), copies the enemies into a hidden DontDestroyOnLoad
  holder and unloads the scene; a trap copies from the holder in any scene. A single scene with everything exists
  (`Level_FinalP2_L3_A5_Main`) but loading it froze the game: do not use it. Traps are queued in memory (not saved). Also
  fixed: the standard gun stopped firing after a shot (`MissileChargeCancelPatch` ran for every gun). To check in the game:
  the log line "Enemies of ... are ready" after loading a save and how long the two background loads hitch, no errors from
  them, each trap in the hub and in a level (bombs home in and explode, spiky ones hover and stab, the dolls fight), a trap
  received during a cutscene waits, standard gun fires again.
- **Saves load only while connected to their slot.** While the Continue panel is open, `SavePanelGate` greys out (the game's own
  `SaveViewData.SetFiltered`: dimmed to 50%, not selectable) every save whose record (read from the save file once per opening)
  names a slot and seed other than the connection, or there is no connection. It follows the connection live, moves the selection
  to the first usable slot, and pins a banner (`MenuNotice`, drawn with IMGUI: toasts never show on the main menu) naming the
  slots. Refusing the click instead left the highlight broken (two slots red, arrow on the wrong one), so
  `SavePanelHandler.Continue(int)` is only a backstop with a timed notice. Unbound saves load as before (and can still adopt a
  slot by connecting in-game, in which case the trap setup waits for the next title screen). This ends offline play of bound
  saves. `StartScreenManager.ContinueDirect` reads a fixed event slot (100) and is not guarded. To check: a bound save with no
  connection / the wrong slot / the right slot (connect while the panel is open: it should light up), an unbound save, all
  three slots greyed, the banner going away when the panel closes, B/Cancel still working.
- **The shotgun and Ann's Board now break the large Konpeito boxes.** Their bullets' damage boxes were solid colliders, and Unity only
  raises a trigger event (what `ColliderHitDamage` listens to) when one collider is a trigger; the crates' damage colliders are
  triggers, the Konpeito boxes' are solid (the standard bullet has a swept hit check, the missile's box is a trigger).
  `CutGunBreakBoxPatch` makes those damage boxes triggers when the bullet is shot. Verified live with a probe: a shotgun hit box laid
  over a large Konpeito box did nothing while solid and broke it once it was a trigger. To check: both guns break large Konpeito boxes
  and crates, and still hit enemies (not twice).
- **Missiles hovered around the spiky puffer until their timer ran out.** The puffer's hurtbox (the only thing bullets hit; bodies are on a
  layer they ignore) is shifted 0.8 m up, so its lock point lies 0.21 m below it and a missile (hit box 0.3 m) circled the lock
  point without touching it. `MissileArrivalPatch`: a missile within 0.3 m of its lock point without a hit hits the target's
  nearest hurtbox through `ColliderHitDamage.TryHit`, and is released where it is if that did not release it. Measured live; the
  fix itself is untested. To check: missiles hit the spiky puffers (and anything else) on arrival and kill/damage them.
- **Bomb puffers were frozen.** `HomingAttackSummonObject.Update` only moves the bomb when `target` is set, and a cloned enemy has none
  (the levels assign it); `TrapSpawner.Aim` now sets `target` = the player and `isTrack` = true (without it, beyond 10 m the bomb steers
  by a ray-sensor result nobody fills in). Verified live on a probe bomb: it flew at the player and exploded at 2 m. The spiky
  puffers (`HomingStabSummonObject`) hover in their Observe state and attack through a token system when conditions hold; they
  were left alone. To check: all four bombs fly at you from any distance and explode; the spiky ones still stab as before.
- **No loads during the trap setup.** `SetupLoadBlockPatch` blocks the save panel's Continue / NewGame / difficulty buttons and
  the start screen's NewGame, NewGameDirect, ContinueDirect and StartLevel while the setup runs, and in the ~1.5 s between connecting
  and the screen fading (`TrapSpawner.BlocksLoading`); the menu's event system is switched off as well. Exit/options stay possible.
  A refused attempt shows "Preparing AP World Settings, please try again in a moment" when the cover is not up yet. To check:
  press Continue / New Game / a slot right after connecting and during the black screen; nothing may start, and the menu works
  again afterwards.
- **Traps wait for a level.** No trap goes off in the hubs (HubWorld_*: enemies were buggy there); they stay queued until the player
  is in a scene named Level*. The setup cover reads "Preparing AP World Settings".
- **Missile gun, round 3.** Charge sound now 0.3 s (from 1.1 s to 1.4 s of the sample) and played for every orb, no minimum gap.
  Hit particles: a released missile used to keep its flame/trail/launch particles hanging for 2 s (`Destroy(gameObject, 2f)`);
  `MissileReleaseEffectsPatch` stops and clears the `Model` and `MSL_Burst` effects at the release (the `HitFeedback` explosion
  plays on). First-charge stutter: the first sample lookup searched the whole sound bank (about a second); `GunSoundPreload`
  now prepares the samples and loads the two sound events once the game has been quiet for 2 s (the log shows "Gun sounds
  prepared in N ms"). To check: no particles linger after a hit, the explosion still shows, no stall on the first charge.
- **`level_order`: `off` / `balanced` / `full`** (the old `vanilla` / `randomized` still work). With freeform keys the keys
  follow the order. To check in the game: a balanced seed's order, and the keys turning up in play order.
- **Hub: the Torii gate opens the Chapter Select with freeform keys** (in every hub stage; it calls the game's own
  `MainMenuAndDisplayPanelHandler.OpenChapterPortal`, like the post-game gate). **Post-mission report and clear reward** show the
  world that was just cleared, not the story's chapter. To check: the gate in several hub stages, leaving the Chapter Select
  without choosing, the gate and the player being free afterwards; the report and reward after clearing a world out of order.
- Missile gun: release timer, charge-cancel cleanup (orbs and red boxes), destroyed targets stopping the release, the orbiting
  missiles staying put inside a domain (the game stops its scaled timer there; they now move on the player's clock); sounds
  (sound bank is read as a stream, cuts use the parent clock, new charge sound with a 0.6 s gap); title-screen connection.

## To test in the game

- Goal protections: finished save with the final boss on the server completes; title-screen replay on another server is refused.
- 20000 and 30000 Konpeito items.
- Continue on a save from another seed is refused.
- Randomized chapter order and level unlock items (in progress).
- Non-default options: shuffle toggles off, Encounter Clears off, encounter rewards, cut content, death link, shop hints
  off, advanced movement.
- Hub after Chapters 6-8, Phantom Blade skip, Level 4 Area 2 to Ikai crash.
- Offline play and reconnect end to end, vanilla save adoption.

## Planned

- Level unlock keys (nine: Chapters 1-8 and the Climax) as entries of the Important Items codex; the journals tab showing
  the hinted items of our world ("X's item located at Y"). Runtime item definitions work (tested: clone a
  `Codex_Important_*` definition, `Initialize`, register with a fixed id, text in the `HH_Codex` string table); open: keep the
  generated entries out of the save, and whether the real journals hide only while connected.

## Ideas

- Give the player the combo moves and skills Dark Homura Hime uses.
- Ship the multiworld generation sweep (`multi_sweep.py`) as an optional tool.

## Done

- Built for 0.4.0: reconnecting to another slot from the title screen. The title screen has no played save, so a connection made
  there is no longer judged against the save that was played before (`SaveBinding.AtTitle`, with a test).

- Built for 0.4.0: all filler forced local (`local_items` with every currency and utility item) no longer fails for about 12% of
  the seeds ("No more spots to place 1 items"). The cause was the direct placement of local Konpeito in random places, which
  took early places the fill needed; it now takes the places reached last first. It had nothing to do with `minimum_konpeito`
  (the same failure rate from 0 to 300,000), so there is no floor for it. 2,400 attempts across minimums from 0 to 1,000,000 passed.

- 0.3.4: item-checking NPCs, Nurikabe dialogue cut, hub arrival lift, Kill goal hook, goal protections, shard recombine.
