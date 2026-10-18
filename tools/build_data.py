"""Builds the shared data contract that the Archipelago world and the BepInEx plugin both read:
data/locations.json, data/items.json and data/regions.json (copied into apworld/homura_hime/data/).

Run:  python tools/build_data.py

Inputs (all in data/):
    locations_chests.json, locations_senbei.json, locations_journals.json, battlerounds.json
        what the game scenes contain, produced by the scripts in tools/extract (see tools/extract/README.md)
    localized_names.tsv   the names the game shows for items and notes ("<code name><TAB><shown name>")
    id_registry.json      the frozen Archipelago ids (existing keys keep their id, new keys get the next free one)

Everything else (shop prices, quest items, chapter layout, ...) is written down in this file because the game only
has it spread over scripts and scenes. The script is deterministic: running it twice changes nothing.

A location is a dict with a stable `key` ("chest/Level_2_Area_5_Main/1"), a readable `name`, a `category`, a `region`
and the `vanilla` reward (what the unmodified game gives there, ";"-separated), plus category specific fields.
"""
import json
import os
import re
import shutil
import sys
from collections import Counter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from location_names import assign_names, typed_item_name  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "data")
# Archipelago ids: locations start at LOC_BASE, items at ITEM_BASE (id_registry.json stores the final numbers).
LOC_BASE = 7_300_000
ITEM_BASE = 7_200_000


def load(name):
    """Reads a JSON file from the data folder."""
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------- chapters / sub-chapters
# The chapters in story order. Each chapter is split into sub-chapters, the named entries of the game's Chapter Select
# (see data/chapter_select.md); every sub-chapter becomes one region of the world and lists the scenes that belong to it.
#   (chapter id, chapter number, in-game name, [(sub-chapter key, in-game name, [scene names])])
CHAPTERS = [
    ("Level1", 1, "Chapter 1: The Skeleton Girl", [
        # the Demonic Realm is entered from Area 1; Area 2 (and 3) come after it
        ("Level1_Area_1_Entrance", "Adashino - Spiriting Away Border", ["Level_1_Area_1_Main"]),
        ("Level1_Ikai_Part1_Entrance", "Demonic Realm - Carcass Ravine", ["Level_1_Ikai_Part_1_Main"]),
        ("Level1_Area_2_Entrance", "Adashino - Beyond The Carcass Ravine", ["Level_1_Area_2_Main", "Level_1_Area_3_Main"]),
        ("Level1_BossRoom_Pass1", "Agasa, Skeleton Girl", ["Level1Boss_Main", "Level_1_BossPass2"]),
    ]),
    ("Level2", 2, "Chapter 2: The Flower and Corpse Princess", [
        ("Level2_Area_1_Entrance", "??? - Hidden Path", ["Level_2_Area_1_Main", "Level_2_Area_2_Main"]),
        ("Level2_Area_3_Entrance", "Tanka Castle - Ninomaru Palace", ["Level_2_Area_3_Main", "Level_2_Area_4_Main"]),
        ("Level2_Area_5_Entrance", "Tanka Castle - Sannomaru Palace", ["Level_2_Area_5_Main", "Level_2_Area_5_2F_Main", "Level_2_MidBoss_Main"]),
        ("Level2_Area_6_Entrance", "Tanka Castle - Rakka Observatory", ["Level_2_Area_6_Main", "Level_2_Area_7_Main", "Level_2_Area_8_Main", "Level_2_Area_9_Main", "Level_2_Ikai_Part1_Main"]),
        ("Level2_BossRoom", "Ling Ling, Flower and Corpse Princess", ["Level2Boss_Main"]),
    ]),
    ("Level3", 3, "Chapter 3: The Holy Maiden of the Full Moon Order", [
        ("Level3_Area_1_Entrance", "Chirakuin - Front Gates", ["Level_3_Area_1_Main", "Level_3_Area_2_Main"]),
        ("Level3_Area_3_Entrance", "Chirakuin - Study", ["Level_3_Area_3_Main", "Level_3_Area_4_Main", "Level3_SecretZone_Main"]),
        ("Level3_Area_5_Entrance", "Chirakuin - Confessional", ["Level_3_Area_5_Main", "Level_3_Area_5_Boss_Main", "Level_3_Area_6_Main", "Level_3_Area_7_Main", "Level_3_Ikai_Part1_Main"]),
        ("Level3_Ikai_Part2_Entrance", "Demonic Realm - Twisted Utopia", ["Level_3_Ikai_Part2_Main"]),
        ("Level3_BossRoom", "Chirakuin, Holy Maiden of the Full Moon Order", ["Level3Boss_Main"]),
    ]),
    ("Level4", 4, "Chapter 4: The Twin Watchdogs", [
        ("Level4_Area_1_Entrance", "Kemono Island - Inugami Hill", ["Level_4_Area_1_Main", "Level_4_Area_2_Main"]),
        ("Level4_Ikai_Part1_Entrance", "Demonic Realm - Twilight Demonic Capital", ["Level_4_Ikai_Part1_Main"]),
        ("Level4_Area_3_Entrance", "Inumachi 1st District - Riverside", ["Level_4_Area_3_Main"]),
        ("Level4_Area_4_Entrance", "Mikan Festival - Shiba Shrine Approach", ["Level_4_Area_4_Main", "Level_4_Area_Arena_Main"]),
        ("Level4_Ikai_Part2_Entrance", "Demonic Realm - Airship", ["Level_4_Ikai_Part2_Main"]),
        ("Level4_BossRoom", "Yumiko & Kumiko, Twin Watchdogs", ["Level_4_Boss", "Level_4_BossPhase2"]),
    ]),
    ("Level5", 5, "Chapter 5: The Fallen Samurai", [
        ("Level5_Area_1_Entrance", "Adashino - Firefly Bridges", ["Level_5_Area_1_Main", "Level_5_Area_2_Main", "Level_5_Area_3_Main"]),
        ("Level5_Area_4_Entrance", "Memory - Break of Dawn", ["Level_5_Area_4_Main"]),
        ("Level5_Area_5_Entrance", "Memory - Home", ["Level_5_Area_5_Main", "Level_5_Ikai_Part1_Main", "Level_5_Ikai_Part2_Main"]),
        ("Level5_BossRoom", "Mamiya, Fallen Samurai", ["Level_5_Boss"]),
    ]),
    ("LevelFinalP1", 6, "Chapter 6: Where it All Began", [
        ("LevelFinalP0_Jinguu", "Jinguu (Climax: The Choice)", ["Level_FinalP0_Jinguu_Main"]),
        ("LevelFinalP1_L1_Ikai_Entrance", "Demonic Realm - Carcass Ravine (Ch6)", ["Level_FinalP1_L1_Ikai_Main", "Level_FinalP1_L1_A3_Main", "Level_FinalP1_L1Boss_Main"]),
        ("LevelFinalP1_L2_A5_Entrance", "Tanka Castle - Sannomaru Palace (Ch6)", ["Level_FinalP1_L2_A5_Main", "Level_FinalP1_L2_Ikai_Main"]),
        ("LevelFinalP1_L2_Boss", "Reunion: Ling Ling", ["Level_FinalP1_L2Boss_Main"]),
    ]),
    ("LevelFinalP2", 7, "Chapter 7: The Brambled Path of Rebellion", [
        ("LevelFinalP2_L3_A5_Entrance", "Chirakuin - Confessional (Ch7)", ["Level_FinalP2_L3_A5_Main"]),
        ("LevelFinalP2_L3_Boss", "Reunion: Nozomi", ["Level_FinalP2_L3Boss_Main"]),
        ("LevelFinalP2_L4_A2_Entrance", "Inumachi 2nd District - Shopping District", ["Level_FinalP2_L4_A2_Main", "Level_FinalP2_L4_Ikai_Main"]),
        ("LevelFinalP2_L4_Boss", "Reunion: Yumiko & Kumiko", ["Level_FinalP2_L4Boss_Phase1_Main", "Level_FinalP2_L4Boss_Phase2_Main"]),
    ]),
    ("LevelFinalP3", 8, "Chapter 8: The Future of Reunion", [
        ("LevelFinalP3_L5_Ikai_Entrance", "Demonic Realm - Snowy End", ["Level_FinalP3_L5_Ikai_Main"]),
        ("LevelFinalP3_L5_A2_Entrance", "Reunion: Agasa", ["Level_FinalP3_L5_A2_Main", "Level_FinalP3_L5_A2_Phase2_Main"]),
        ("LevelFinalP3_L5_A5_Entrance", "Memory - Home (Ch8)", ["Level_FinalP3_L5_A5_Main"]),
        ("LevelFinalP3_L5_Boss", "Reunion: Mamiya", ["Level_FinalP3_L5Boss_Main"]),
    ]),
]
FINALE = ("LevelFinalP4", 9, "Finale: Desire", [
    ("LevelFinalP4_Passage_Entrance", "Jinguu - Pathway", ["Level_FinalP4_Passage_Main"]),
    ("LevelFinalP4_Jinguu_Phase1", "Admini", ["Level_FinalP4_Jinguu_Phase1_Main", "Level_FinalP4_Jinguu_Phase2_Main"]),
    ("LevelFinalP4_Jinguu_Ending", "Ann's wish", ["Level_FinalP4_Jinguu_Ending_Main", "Level_FinalP4_EndFinale_Main"]),
])

# scene name -> key of the sub-chapter (region) it belongs to
SCENE_TO_SUB = {}
for cid, _num, _name, subs in CHAPTERS + [FINALE]:
    for key, _sname, scenes in subs:
        for s in scenes:
            SCENE_TO_SUB[s] = key
# The hub scenes all belong to the one "Hub" region.
HUB_SCENES = {"HubWorld_Main", "HubWorld_PostGame_Main", "HubWorld_TrainingGround_Main"}


def region_of(scene):
    """The region (sub-chapter key or "Hub") a scene belongs to, None for an unknown scene."""
    if scene in HUB_SCENES:
        return "Hub"
    return SCENE_TO_SUB.get(scene)


# ---------------------------------------------------------------- id registry
# Ids must never change once a seed may exist, so they are stored in id_registry.json and only ever appended to.
reg_path = os.path.join(DATA, "id_registry.json")
registry = json.load(open(reg_path, encoding="utf-8")) if os.path.exists(reg_path) else {"locations": {}, "items": {}}


def loc_id(key):
    """The Archipelago id of a location key (a new key gets the next free id)."""
    ids = registry["locations"]
    if key not in ids:
        ids[key] = LOC_BASE + (max([v - LOC_BASE for v in ids.values()], default=-1) + 1)
    return ids[key]


def item_id(name):
    """The Archipelago id of an item name (a new name gets the next free id)."""
    ids = registry["items"]
    if name not in ids:
        ids[name] = ITEM_BASE + (max([v - ITEM_BASE for v in ids.values()], default=-1) + 1)
    return ids[name]


locations = []


def add_rewards(key, category, region, vanilla, scene=None, **extra):
    """
    A reward screen that hands out several things ("a; b; c") is one location per thing: the first keeps the key, the others get
    "#2", "#3"... The hooks report all of them when the screen is paid (see Catalog.Bundle in the plugin).
    """
    for n, part in enumerate(p.strip() for p in vanilla.split(";")):
        add(key if n == 0 else f"{key}#{n + 1}", category, region, part, scene, **extra)


def add(key, category, region, vanilla, scene=None, pos=None, **extra):
    """Appends a location. `vanilla` is what the unmodified game gives there; `extra` are category specific fields."""
    if region is None:
        raise SystemExit("no region for %s (scene %s)" % (key, scene))
    rec = {"id": loc_id(key), "key": key, "category": category, "region": region, "vanilla": vanilla}
    if scene:
        rec["scene"] = scene
    if pos:
        rec["pos"] = pos
    rec.update(extra)
    locations.append(rec)


# ---------------------------------------------------------------- treasure chests
# Chests whose loot table is one of a kind (a named omamori) rather than the generic Konpeito/HP/SP tables.
OMAMORI_TABLE = {
    "Omamori_L2A3": "Omamori_DefenseValue_Big", "Omamori_L2Ikai": "Omamori_IncreaseHealingDropValue",
    "Omamori_L3A5": "Omamori_AttackValue_Big", "Omamori_L4A3": "Omamori_MoneyGain",
    "Omamori_L5A5": "Omamori_ParrySkillCharge", "Omamori_LFP1L2Ikai": "Omamori_IncreaseGunRecoverValue",
}
chests = [c for c in load("locations_chests.json") if c["type"] == "TreasureChest"]
by_scene = {}
for c in chests:
    by_scene.setdefault(c["scene"], []).append(c)
for scene, lst in by_scene.items():
    lst.sort(key=lambda c: tuple(c["pos"]))
    for i, c in enumerate(lst, 1):
        table = c["table"].replace("TreasureChestAddItemTable_", "")
        if table == "Default":
            vanilla = "Konpeitou x3000"
        elif table == "HPCrystal":
            vanilla = "HP Shard"
        elif table == "SPCrystal":
            vanilla = "SP Shard"
        else:
            vanilla = OMAMORI_TABLE[table]
        add("chest/%s/%d" % (scene, i), "chest", region_of(scene), vanilla, scene, c["pos"], object=c["go"].split(" < ")[0])

# ---------------------------------------------------------------- battle chests (ambushes) and head-chase chests
for scene, ambush in [("Level_1_Area_2_Main", "L1C01"), ("Level_2_Area_3_Main", "L2C01"), ("Level_2_Area_5_Main", "L2C03"), ("Level_3_Area_2_Main", "L3C01")]:
    add("ambush/" + ambush, "ambush_chest", region_of(scene), "Konpeitou x5000", scene)
HEADS = [("Level1", "Level_1_Ikai_Part_1_Main", "HP Shard"), ("Level2", "Level_2_Ikai_Part1_Main", "Konpeitou x5000"),
         ("Level3", "Level_3_Ikai_Part1_Main", "Konpeitou x5000"), ("Level4", "Level_4_Ikai_Part1_Main", "Konpeitou x5000"),
         ("Level5", "Level_5_Ikai_Part2_Main", "Konpeitou x5000")]
for lv, scene, vanilla in HEADS:
    add("head/" + lv, "head_chase", region_of(scene), vanilla, scene)
# optional ambushes without a vanilla item (encounter_rewards option)
for scene, ambush in [("Level_2_Area_3_Main", "L2C02"), ("Level_2_Ikai_Part1_Main", "L2BI01"), ("Level_4_Area_1_Main", "L4C01"), ("Level_5_Area_5_Main", "L5C01")]:
    add("ambush/" + ambush, "ambush_reward", region_of(scene), "None", scene, option="encounter_rewards")

# ---------------------------------------------------------------- Senbei on the map (37) and settlement Senbei (5)
sen = load("locations_senbei.json")
by_scene = {}
for s in sen:
    by_scene.setdefault(s["scene"], []).append(s)
for scene, lst in by_scene.items():
    lst.sort(key=lambda s: ("SettlementManager" in s["go"], tuple(s["pos"])))
    for i, s in enumerate(lst, 1):
        senbei_type = "settlement" if "SettlementManager" in s["go"] else "map"
        add("senbei/%s/%d" % (scene, i), "senbei", region_of(scene), "Senbei x1", scene, s["pos"], senbei_type=senbei_type)

# ---------------------------------------------------------------- journals (world pickups and the Level 1 diary hand-over)
for j in load("locations_journals.json"):
    for src in j["sources"]:
        if src["type"] == "pickup":
            if src["scene"] in HUB_SCENES:
                continue  # post-game hub notes are not part of the randomized pool
            add("journal/" + j["item"], "journal", region_of(src["scene"]), j["item"], src["scene"], src.get("pos"))
        elif src["type"] == "operator:HandOver" and j["item"] == "Codex_Note_Level1_Diary2":
            add("journal/" + j["item"], "journal", region_of(src["scene"]), j["item"], src["scene"])

# ---------------------------------------------------------------- shop (tier = number of cleared worlds needed)
# The merchant's slots. "tier" is the number of cleared chapters that unlocks the slot, "item_type" becomes the item's
# `type` (combo, skill, omamori, shard, other). Shard slots exist three times ("#1".."#3") for the three price stages.
SHOP = []  # (definition, tier, item_type)
for d in ["ComboB", "ComboCTCA", "ComboCTCB", "ComboD", "Combo KnockOutAerialRave", "HeavyComboB", "HeavyComboC", "HeavyCombo KnockOutAerialRave", "AerialComboB", "AerialComboC"]:
    SHOP.append((d, 0, "combo"))
for d, t in [("Skill_Explosion Charm", 0), ("Skill_Blaze Rush", 0), ("Skill_Thousand Cuts", 0), ("Skill_Suzaku", 0), ("Skill_Gale Blade", 3), ("Skill_Heart Prison", 3),
             ("Skill_Binding Curse", 2), ("Skill_Bloodflame Palm", 2), ("Skill_Heavenly Guardian", 4), ("Skill_Kagemusha", 4), ("Skill_Needle Rain", 5)]:
    SHOP.append((d, t, "skill"))
for d, t in [("Omamori_AttackValue", 1), ("Omamori_DefenseValue", 1), ("Omamori_HP", 1), ("Omamori_ParryIncreaseHealingDropRate", 1), ("Omamori_BulletAtkReduce", 1),
             ("Omamori_ExtendParryTime", 1), ("Omamori_MeleeAtkReduce", 2), ("Omamori_ParryInvincibleTime", 3), ("Omamori_IncreaseDashInvincibleTime", 3), ("Omamori_TransformCharge", 6)]:
    SHOP.append((d, t, "omamori"))
for d in ["OmamoriCostUpgrade_1", "OmamoriCostUpgrade_2", "HP Crystal", "SP Crystal", "Kushinada_Dango"]:
    SHOP.append((d, 0, "other"))
for d in ["HP Shard", "SP Shard"]:
    for stage in (1, 2, 3):
        SHOP.append((d + "#" + str(stage), 0, "shard"))
# Konpeito price of every shop slot, read from the merchant's inventory; the world's shop logic reads it from the "price" field
SHOP_PRICE = {
    **{name: 8000 for name in ("ComboB", "ComboCTCA", "HeavyComboB")},
    **{name: 12000 for name in ("ComboCTCB", "Combo KnockOutAerialRave", "HeavyComboC")},
    "ComboD": 16000, "HeavyCombo KnockOutAerialRave": 16000, "AerialComboB": 10000, "AerialComboC": 10000,
    "Skill_Blaze Rush": 8000, "Skill_Thousand Cuts": 8000, "Skill_Gale Blade": 8000,
    "Skill_Heart Prison": 12000, "Skill_Needle Rain": 12000,
    "Skill_Explosion Charm": 16000, "Skill_Binding Curse": 16000, "Skill_Bloodflame Palm": 16000,
    "Skill_Suzaku": 36000, "Skill_Heavenly Guardian": 36000, "Skill_Kagemusha": 36000,
    **{name: 16000 for name in ("Omamori_AttackValue", "Omamori_DefenseValue", "Omamori_HP",
                                "Omamori_ParryIncreaseHealingDropRate", "Omamori_BulletAtkReduce", "Omamori_MeleeAtkReduce")},
    "Omamori_ExtendParryTime": 24000, "Omamori_TransformCharge": 24000,
    "Omamori_ParryInvincibleTime": 32000, "Omamori_IncreaseDashInvincibleTime": 32000,
    "OmamoriCostUpgrade_1": 12000, "OmamoriCostUpgrade_2": 36000,
    "HP Crystal": 48000, "SP Crystal": 48000, "Kushinada_Dango": 24000,
    **{f"{shard} Shard#{stage}": price for shard in ("HP", "SP") for stage, price in ((1, 4000), (2, 12000), (3, 24000))},
}
# Senbei (Crackers) some slots cost on top of the Konpeito price, read from the merchant's BuyPrice attributes. Unlike
# Konpeito, Senbei cannot be farmed, so the world's shop logic counts them too ("senbei" field of the slot).
SHOP_SENBEI = {
    "Skill_Blaze Rush": 1, "Skill_Thousand Cuts": 1, "Skill_Explosion Charm": 1, "Skill_Suzaku": 3,
    "Skill_Gale Blade": 1, "Skill_Heart Prison": 2, "Skill_Binding Curse": 1, "Skill_Bloodflame Palm": 1,
    "Skill_Heavenly Guardian": 1, "Skill_Kagemusha": 2, "Skill_Needle Rain": 1,
    "Omamori_BulletAtkReduce": 3, "Omamori_ExtendParryTime": 1, "Omamori_MeleeAtkReduce": 3,
    "Omamori_ParryInvincibleTime": 1, "Omamori_IncreaseDashInvincibleTime": 1, "Omamori_TransformCharge": 2,
}
for d, t, item_type in SHOP:
    add("shop/" + d, "shop", "Hub", d.split("#")[0], tier=t, shop_type=item_type, price=SHOP_PRICE[d],
        senbei=SHOP_SENBEI.get(d, 0))

# ---------------------------------------------------------------- stage clears, Harumi, tournament, festival stall
# (chapter id, what clearing the chapter gives for the first time)
STAGE = [("Level1", "Senbei x1; Omamori_Jinguu"), ("Level2", "Gun_Funnel"), ("Level3", "Kushinada_Dango"), ("Level4", "Skill_Chain Lightning"),
         ("Level5", "Skill_Quaking Strike"), ("LevelFinalP1", "Kushinada_Dango"), ("LevelFinalP2", "Kushinada_Dango"), ("LevelFinalP3", "Kushinada_Dango")]
for cid, vanilla in STAGE:
    add_rewards("stage/" + cid, "stage_clear", "Hub", vanilla, chapter=cid)
# the Level 0 tutorial reward board (a location only when the tutorial is played)
add("tutorial/clear", "tutorial_clear", "Hub", "Konpeitou x8000")
# Harumi (the hub's flower girl) rewards, in order, and the Sakura Petals needed for each of them.
HARUMI = ["Kushinada_Dango", "Omamori_HP_Big", "Konpeitou x5000", "Kushinada_Dango", "Skill_Rising Serpent", "Konpeitou x10000", "Konpeitou x15000"]
HARUMI_NEED = [100, 300, 600, 1000, 1400, 1800, 2300]
for i, (v, need) in enumerate(zip(HARUMI, HARUMI_NEED), 1):
    add("harumi/%d" % i, "harumi", "Hub", v, petals=need)
add_rewards("tournament/reward", "tournament", region_of("Level_4_Area_Arena_Main"), "Codex_Important_Level4_ProofOfTheLnugami; Senbei x3; HP Crystal", "Level_4_Area_Arena_Main")
add("stall/festival", "stall", region_of("Level_4_Area_4_Main"),
    "; ".join("Codex_Important_Level4_" + n for n in ("AppleCandy", "ChocolBanana", "Takoyaki", "WaterBall")), "Level_4_Area_4_Main")

# ---------------------------------------------------------------- glitched-spirit challenges (Trials I-X)
# (trial id, scene, reward)
FC = [("FC01", "Level_3_Area_7_Main", "Konpeitou x5000; Codex_Note_FinalLevel_ExorcismArts"), ("FC02", "Level_4_Area_3_Main", "Konpeitou x5000; Codex_Note_FinalLevel_Negai"),
      ("FC03", "Level_5_Area_4_Main", "Konpeitou x5000; Codex_Note_FinalLevel_Demon"), ("FC04", "Level_FinalP1_L1_A3_Main", "Konpeitou x5000; Codex_Note_FinalLevel_Jinguu"),
      ("FC05", "Level_FinalP2_L3_A5_Main", "Konpeitou x5000; Codex_Note_FinalLevel_SomeoneNotes"), ("FC06", "Level_FinalP2_L3_A5_Main", "Senbei x3; Codex_Note_FinalLevel_Soul"),
      ("FC07", "Level_FinalP2_L4_A2_Main", "Senbei x3; Codex_Note_FinalLevel_ObservationRecords2"), ("FC08", "Level_FinalP3_L5_Ikai_Main", "Senbei x3; Codex_Note_FinalLevel_HighPriestess"),
      ("FC09", "Level_FinalP3_L5_A5_Main", "Konpeitou x5000; Codex_Note_FinalLevel_TatteredMissionLogs"), ("FC10", "Level_FinalP3_L5_A5_Main", "Konpeitou x5000; Codex_Note_FinalLevel_Ikai")]
for name, scene, vanilla in FC:
    add_rewards("trial/" + name, "trial", region_of(scene), vanilla, scene)
# extra Senbei from the 5 settlement pickups are already in the Senbei section

# ---------------------------------------------------------------- encounter clears (option encounter_clears)
# Every BattleRound in the game is a location sent when the round finishes. A round is identified by its scene and the
# names of its parent objects (the "chain"), the label is the readable part of that chain.
def encounter_label(chain):
    """A short label for a BattleRound chain ("B03", "Room2", "Round1", ...), "main" for a scene's plain fight."""
    parts = chain.split(" < ")
    for part in parts[:2]:
        m = re.fullmatch(r"BattleRound_(\w+)", part)
        if m:
            return m.group(1)
    for part in parts:
        m = re.fullmatch(r"Room ?(\d+)", part)
        if m:
            return "Room" + m.group(1)
        m = re.fullmatch(r"Gameplay_(Round\d+)", part)
        if m:
            return m.group(1)
    return "main"


# Encounters (B..) and bosses are mandatory; ambushes (C.., BI..), trials (FC..) and the Ikai dungeon rooms are optional.
OPTIONAL_ENCOUNTER = re.compile(r"(C|BI|FC)\d+|Room\d+")


# Rounds of one scene with the same label are numbered ("B03", "B03#2", ...) in a stable (sorted) order.
seen_labels = {}
for r in sorted((r for r in load("battlerounds.json") if r["type"] == "BattleRound"), key=lambda r: (r["scene"], r["chain"])):
    label = encounter_label(r["chain"])
    n = seen_labels[(r["scene"], label)] = seen_labels.get((r["scene"], label), 0) + 1
    key = "encounter/%s/%s" % (r["scene"], label if n == 1 else "%s#%d" % (label, n))
    extra = {}
    if OPTIONAL_ENCOUNTER.fullmatch(label):
        extra["optional"] = True
    add(key, "encounter", "Hub" if r["scene"] == "Level_0_Main" else region_of(r["scene"]), "None", r["scene"], option="encounter_clears", chain=r["chain"], **extra)
# ---------------------------------------------------------------- hand-overs and ability pickups
# Things an NPC hands over: (key, scene, vanilla reward, kind). "gate" items open a part of a chapter, see the world's
# SCENE_REQ / TRANSITION_REQ.
HANDOVERS = [
    ("handover/flower", "Level_1_Area_1_Main", "Codex_Important_Level1_Flower", "gate"),
    ("handover/launcher", "Level_2_Area_1_Main", "Codex_Important_Level2_Launcher", "gate"),
    ("handover/boot", "Level_2_Area_4_Main", "Codex_Important_Level2_Boot", "gate"),
    ("handover/hat", "Level_3_Area_5_Main", "Codex_Important_Level3_Nurikabe'sHat", "gate"),
    ("handover/fresh_fish", "Level_4_Area_3_Main", "Codex_Important_Level4_FreshFish", "gate"),
    ("handover/gloves", "Level_5_Area_5_Main", "Codex_Important_Level5_Nurikabe'sGloves", "gate"),
    ("handover/ultimate", "Level3_SecretZone_Main", "HH_SpecialMove", "ability"),
    ("handover/omamori_ann", "Level_4_Area_4_Main", "Omamori_Ann", "omamori"),
    ("handover/transformation", "Level_FinalP1_L2_A5_Main", "TransformationAbility", "ability"),
]
for key, scene, vanilla, hand_type in HANDOVERS:
    add(key, "handover", region_of(scene), vanilla, scene, hand_type=hand_type)
# Phantom Blade is equipped by a script at the start of Level 1 Area 2; in the randomizer that script sends this check
# instead (reaching the area needs the flower, but not the dash skill: the gap comes later in the area).
add("handover/phantom_blade", "handover", region_of("Level_1_Area_2_Main"), "Skill_Blade Warp", "Level_1_Area_2_Main", hand_type="skill_equip", scene_req=["Pretty Little Flower"])
add("handover/desire_circle", "handover", "Hub", "Gun_Laser", hand_type="gun")

# ---------------------------------------------------------------- readable names
# the names the game shows for items and notes (read from its localization), also used for the readable location names
LOCALIZED = dict(line.rstrip("\n").split("\t") for line in open(os.path.join(DATA, "localized_names.tsv"), encoding="utf-8-sig") if line.strip())
assign_names(locations, LOCALIZED)
# ---------------------------------------------------------------- Konpeito that the vanilla game pays at a location
# The hooks take the vanilla payout away, so the pool must hold it again: every location that pays a fixed amount gets a
# "konpeito" field, and the world adds one "<amount> Konpeito" item per such location. The stage-clear reward boards pay by
# rank (S/A/B/C/D); their median (rank B) is used. The hub currency boards pay nothing in the randomizer and are not
# counted (all Konpeito comes from enemies, map pickups and checks).
STAGE_BOARD_MEDIAN = {"Level1": 6000, "Level2": 6000, "Level3": 6000, "Level4": 6000, "Level5": 6000,
                      "LevelFinalP1": 10000, "LevelFinalP2": 14000, "LevelFinalP3": 14000}
for l in locations:
    amount = sum(int(m.group(1)) for m in re.finditer(r"Konpeitou x(\d+)", l["vanilla"] or ""))
    if l["category"] == "stage_clear" and "#" not in l["key"]:  # the board pays with the first part of the reward
        amount += STAGE_BOARD_MEDIAN[l["chapter"]]
    if amount:
        l["konpeito"] = amount

# The Pretty Little Flower opens Area 1: its chest and its journal cannot be collected before it is handed over (the
# hand-over itself, the Senbei and the fights need nothing).
for l in locations:
    if l.get("scene") == "Level_1_Area_1_Main" and l["category"] in ("chest", "journal"):
        l["scene_req"] = ["Pretty Little Flower"]

# ---------------------------------------------------------------- write locations
locations.sort(key=lambda l: l["id"])
json.dump(locations, open(os.path.join(DATA, "locations.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# ---------------------------------------------------------------- regions
regions = {"chapters": [], "hub_tiers": "shop tier t needs cleared_world_count >= t"}
for cid, num, name, subs in CHAPTERS + [FINALE]:
    regions["chapters"].append({"id": cid, "number": num, "name": name, "sub_chapters": [
        {"key": key, "name": sname, "scenes": scenes} for key, sname, scenes in subs]})
json.dump(regions, open(os.path.join(DATA, "regions.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# ---------------------------------------------------------------- items
ITEMS = []


# Items that are game items carry the name the game itself shows (data/localized_names.tsv, read from the game's
# localization), so what other players see matches what the game shows. Ids stay with the item when its name changes.


def shown_name(name, game):
    shown = typed_item_name(game, LOCALIZED) if game in LOCALIZED else name
    if shown != name and shown not in registry["items"]:
        # an item that was known under an older name keeps its id
        for old in (LOCALIZED.get(game), name):
            if old in registry["items"]:
                registry["items"][shown] = registry["items"].pop(old)
                break
    return shown

def item(name, item_type, cls, count=1, game=None, **extra):
    """
    Appends an item definition: `cls` is the generator classification ("progression", "useful", "filler"), `count` how
    many copies the pool holds (0 = decided by the world, e.g. filler), `game` the code name the plugin uses.
    """
    name = shown_name(name, game)
    rec = {"id": item_id(name), "name": name, "type": item_type, "classification": cls, "count": count, "game": game or name}
    rec.update(extra)
    ITEMS.append(rec)


# progression: key items
for n, g in [("Pretty Little Flower", "Codex_Important_Level1_Flower"), ("Elevator Lever", "Codex_Important_Level2_Launcher"), ("Nurikabe's Shoe", "Codex_Important_Level2_Boot"),
             ("Nurikabe's Hat", "Codex_Important_Level3_Nurikabe'sHat"), ("Fresh Fish", "Codex_Important_Level4_FreshFish"),
             ("Inugami Certificate", "Codex_Important_Level4_ProofOfTheLnugami"), ("Nurikabe's Gloves", "Codex_Important_Level5_Nurikabe'sGloves")]:
    item(n, "key", "progression", game=g, collection="Codex")
item("Mirage Daydream: Slash", "ability", "progression", game="HH_SpecialMove")
item("Transformation", "ability", "progression", game="TransformationAbility")
for n, g in [("Phantom Blade", "Skill_Blade Warp"), ("Spark Rush", "Skill_Blaze Rush"), ("Quake Strike", "Skill_Quaking Strike"), ("Bloodflame Palm", "Skill_Bloodflame Palm")]:
    item(n, "dash_skill", "progression", game=g)
# useful
for d, t, item_type in SHOP:
    if item_type == "combo":
        item(d, "combo", "useful", game=d, collection="Combo")
for d, t, item_type in SHOP:
    if item_type == "skill" and d not in ("Skill_Blaze Rush", "Skill_Bloodflame Palm"):
        item(d.replace("Skill_", ""), "skill", "useful", game=d)
item("Rising Serpent", "skill", "useful", game="Skill_Rising Serpent")
item("Chain Lightning", "skill", "useful", game="Skill_Chain Lightning")
# Omamori (charms) by the suffix of their game name "Omamori_<suffix>"
OMAMORI = ["Ann", "AttackValue", "AttackValue_Big", "BulletAtkReduce", "DefenseValue", "DefenseValue_Big", "ExtendParryTime", "HP", "HP_Big", "IncreaseDashInvincibleTime",
           "IncreaseGunRecoverValue", "IncreaseHealingDropValue", "Jinguu", "MeleeAtkReduce", "MoneyGain", "ParryIncreaseHealingDropRate", "ParryInvincibleTime", "ParrySkillCharge", "TransformCharge"]
for o in OMAMORI:
    item("Omamori: " + o.replace("_", " "), "omamori", "useful", game="Omamori_" + o)
item("Desire Circle", "gun", "useful", game="Gun_Laser")
item("Ann's Support", "gun", "useful", game="Gun_Funnel")
item("Omamori Cost Up 1", "upgrade", "useful", game="OmamoriCostUpgrade_1")
item("Omamori Cost Up 2", "upgrade", "useful", game="OmamoriCostUpgrade_2")
item("HP Shard", "shard", "useful", count=12, game="HP Shard")
item("SP Shard", "shard", "useful", count=12, game="SP Shard")
item("HP Crystal", "crystal", "useful", count=1, game="HP Crystal")
item("SP Crystal", "crystal", "useful", count=1, game="SP Crystal")
# cut content (option)
item("Ann's Board", "gun", "useful", game="Gun_Board", option="cut_content")
item("Fortunate Misfortune", "gun", "useful", game="Gun_Missile", option="cut_content")
item("Ruptured Bones", "gun", "useful", game="Gun_Shotgun", option="cut_content")
item("Flame-Stoking Explosion Charm", "skill", "useful", game="Skill_Double Charm", option="cut_content")
# Konpeito amounts of the vanilla payouts (one item per location that paid it, see the "konpeito" field of the locations)
FIXED_KONPEITO = sorted({l["konpeito"] for l in locations if "konpeito" in l})
# filler (counts decided by the generator)
item("Special Hanami Dango", "consumable", "filler", count=0, game="Kushinada_Dango")
item("Senbei", "currency", "filler", count=0, game="Crackers", amount=1)
# pairs keep the number of Senbei items in the pool down
item("2 Senbei", "currency", "filler", count=0, game="Crackers", amount=2)
# sizes a Konpeito filler item can be raised to when the pool has to reach options.MinimumKonpeito ("ladder": true)
KONPEITO_LADDER = (500, 750, 1000, 1500, 2000, 3000, 4000, 5000, 7500, 10000)
for amount in sorted(set(KONPEITO_LADDER) | set(FIXED_KONPEITO)):
    extra = {"ladder": True} if amount in KONPEITO_LADDER else {}
    item("%d Konpeito" % amount, "currency", "filler", count=0, game="Konpeitou", amount=amount, **extra)
for amount in (10, 20, 30, 40, 50):
    item("%d Sakura Petals" % amount, "currency", "filler", count=0, game="Sakura", amount=amount)
item("Full Heal", "utility", "filler", count=0, game="FullHeal")
item("Ultimate Charge", "utility", "filler", count=0, game="UltimateCharge")
# level unlock items (option): one per world
for cid, num, name, subs in CHAPTERS:
    item("Chapter %d Unlock" % num, "unlock", "progression", count=0, game=cid, chapter=cid)

json.dump(ITEMS, open(os.path.join(DATA, "items.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
json.dump(registry, open(reg_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)

# ---------------------------------------------------------------- report
print("locations:", len(locations), dict(Counter(l["category"] for l in locations)))
print("optional-only:", sum(1 for l in locations if l.get("option")))
print("items:", len(ITEMS), "fixed item copies:", sum(i["count"] for i in ITEMS if i["classification"] != "filler" and "option" not in i and i["type"] != "unlock"))

# ---------------------------------------------------------------- copy into the apworld package
for name in ("locations.json", "items.json", "regions.json"):
    shutil.copy(os.path.join(DATA, name), os.path.join(ROOT, "apworld", "homura_hime", "data", name))
