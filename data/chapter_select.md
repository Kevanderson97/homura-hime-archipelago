# Chapter Select: chapters and sub-chapters (from the game's `Portal_UI_EN` string table and `PortalDataSO`)

Official chapter names (key `PortalChapter_*`) and the internal categories used by this project:

| Chapter (in-game name) | Internal | Plan name |
|---|---|---|
| Chapter 1: The Skeleton Girl | Level1 | Level 1 |
| Chapter 2: The Flower and Corpse Princess | Level2 | Level 2 |
| Chapter 3: The Holy Maiden of the Full Moon Order | Level3 | Level 3 |
| Chapter 4: The Twin Watchdogs | Level4 | Level 4 |
| Chapter 5: The Fallen Samurai | Level5 | Level 5 |
| Climax: The Choice | LevelFinalP0 | Final P0 (prelude of Chapter 6) |
| Chapter 6: Where it All Began | LevelFinalP1 | Final P1 |
| Chapter 7: The Brambled Path of Rebellion | LevelFinalP2 | Final P2 |
| Chapter 8: The Future of Reunion | LevelFinalP3 | Final P3 |
| Finale: Desire | LevelFinalP4 | Final P4 (final world) |

The 8 randomizable worlds are therefore **Chapters 1-8**. Entries in `Portal_UI_EN` that have a real name (not just the key) are the sub-chapters shown in the Chapter Select; the others (e.g. `Level1_Area_2_Entrance`) are scenes inside a sub-chapter and are never listed.

## Sub-chapters (named entries), in order

| Chapter | Sub-chapters (portal key -> in-game name) |
|---|---|
| 1 | `Level1_Area_1_Entrance` -> Adashino - Spiriting Away Border; `Level1_Ikai_Part1_Entrance` -> Demonic Realm - Carcass Ravine; `Level1_BossRoom_Pass1` -> Agasa, Skeleton Girl |
| 2 | `Level2_Area_1_Entrance` -> ??? - Hidden Path; `Level2_Area_3_Entrance` -> Tanka Castle - Ninomaru Palace; `Level2_Area_5_Entrance` -> Tanka Castle - Sannomaru Palace; `Level2_Area_6_Entrance` -> Tanka Castle - Rakka Observatory; `Level2_BossRoom` -> Ling Ling, Flower and Corpse Princess |
| 3 | `Level3_Area_1_Entrance` -> Chirakuin - Front Gates; `Level3_Area_3_Entrance` -> Chirakuin - Study; `Level3_Area_5_Entrance` -> Chirakuin - Confessional; `Level3_Ikai_Part2_Entrance` -> Demonic Realm - Twisted Utopia; `Level3_BossRoom` -> Chirakuin, Holy Maiden of the Full Moon Order |
| 4 | `Level4_Area_1_Entrance` -> Kemono Island - Inugami Hill; `Level4_Area_3_Entrance` -> Inumachi 1st District - Riverside; `Level4_Area_4_Entrance` -> Mikan Festival - Shiba Shrine Approach; `Level4_Ikai_Part1_Entrance` -> Demonic Realm - Twilight Demonic Capital; `Level4_Ikai_Part2_Entrance` -> Demonic Realm - Airship; `Level4_BossRoom` -> Yumiko & Kumiko, Twin Watchdogs |
| 5 | `Level5_Area_1_Entrance` -> Adashino - Firefly Bridges; `Level5_Area_4_Entrance` -> Memory - Break of Dawn; `Level5_Area_5_Entrance` -> Memory - Home; `Level5_BossRoom` -> Mamiya, Fallen Samurai |
| Climax | `LevelFinalP0_Jinguu` -> Jinguu |
| 6 | `LevelFinalP1_L1_Ikai_Entrance` -> Demonic Realm - Carcass Ravine; `LevelFinalP1_L2_A5_Entrance` -> Tanka Castle - Sannomaru Palace; `LevelFinalP1_L2_Boss` -> Reunion: Ling Ling |
| 7 | `LevelFinalP2_L3_A5_Entrance` -> Chirakuin - Confessional; `LevelFinalP2_L3_Boss` -> Reunion: Nozomi; `LevelFinalP2_L4_A2_Entrance` -> Inumachi 2nd District - Shopping District; `LevelFinalP2_L4_Boss` -> Reunion: Yumiko & Kumiko |
| 8 | `LevelFinalP3_L5_Ikai_Entrance` -> Demonic Realm - Snowy End; `LevelFinalP3_L5_A2_Entrance` -> Reunion: Agasa; `LevelFinalP3_L5_A5_Entrance` -> Memory - Home; `LevelFinalP3_L5_Boss` -> Reunion: Mamiya |
| Finale | `LevelFinalP4_Passage_Entrance` -> Jinguu - Pathway; `LevelFinalP4_Jinguu_Phase1` -> Admini; `LevelFinalP4_Jinguu_Ending` -> Ann's wish |

(Order inside a chapter follows the story/scene order; a few entries are listed in story order rather than key order, e.g. Chapter 3's Ikai.) Each sub-chapter contains several encounters and may span several scenes (Chapter 1: Adashino = Area 1 through Area 3).
