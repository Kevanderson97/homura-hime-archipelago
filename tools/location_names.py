"""Readable names for the Archipelago locations.

Every location has a stable internal key ("shop/ComboD", "chest/Level_2_Area_5_Main/1") that the plugin and the ids are
built on, and a readable name that players see in Archipelago ("Shop: Blazing Thrust", "Level 2 Area 5: Chest 1").
The names are generated from the key and the data in the location record, with the game's own localized names
(data/localized_names.tsv) for items and notes.
"""
import re

CHAPTER_OF_FINAL = {"P0": "Climax", "P1": "Chapter 6", "P2": "Chapter 7", "P3": "Chapter 8", "P4": "Finale"}


def scene_label(scene):
    """A short readable place for a scene name ("Level_2_Area_5_Main" -> "Level 2 Area 5")."""
    if scene in ("HubWorld_Main", "HubWorld_PostGame_Main", "HubWorld_TrainingGround_Main"):
        return "Hub"
    if scene == "Level_0_Main":
        return "Tutorial"
    if scene == "Level3_SecretZone_Main":
        return "Level 3 Secret Zone"

    # Boss rooms, named inconsistently by the game: "Level1Boss_Main", "Level_1_BossPass2", "Level_4_BossPhase2"
    m = re.fullmatch(r"Level_?(\d)_?Boss(Phase2|Pass2)?(?:_Main)?", scene)
    if m:
        return f"Level {m.group(1)} Boss" + {"Phase2": " (Phase 2)", "Pass2": " (Pass 2)", None: ""}[m.group(2)]
    m = re.fullmatch(r"Level_(\d)_(Boss|BossPhase2|BossPass2|MidBoss)(?:_Main)?", scene)
    if m:
        extra = {"Boss": "Boss", "BossPhase2": "Boss (Phase 2)", "BossPass2": "Boss (Pass 2)", "MidBoss": "Mid-Boss"}[m.group(2)]
        return f"Level {m.group(1)} {extra}"
    # The tournament arena, ordinary numbered areas ("Level_2_Area_5_2F_Main" -> "Level 2 Area 5 2F") and the
    # Demonic Realm (Ikai) parts ("Level_1_Ikai_Part_1_Main" and "Level_2_Ikai_Part1_Main" are both spelled in the game)
    m = re.fullmatch(r"Level_(\d)_Area_Arena_Main", scene)
    if m:
        return f"Level {m.group(1)} Arena"
    m = re.fullmatch(r"Level_(\d)_Area_(\w+?)_Main", scene)
    if m:
        return f"Level {m.group(1)} Area {m.group(2).replace('_', ' ')}"
    m = re.fullmatch(r"Level_(\d)_Ikai_Part_?(\d)_Main", scene)
    if m:
        return f"Level {m.group(1)} Demonic Realm {m.group(2)}"

    # The final chapters 6-8 replay the earlier levels ("Level_FinalP2_L3_A5_Main" = chapter 7, level 3, area 5) and
    # the finale itself is "Level_FinalP4_...".
    m = re.fullmatch(r"Level_Final(P\d)_Jinguu(?:_(Phase\d))?_Main", scene)
    if m:
        return CHAPTER_OF_FINAL[m.group(1)] + (f" {m.group(2).replace('Phase', 'Phase ')}" if m.group(2) else "")
    m = re.fullmatch(r"Level_Final(P\d)_L(\d)(Boss(?:_Phase\d)?)?(?:_(A\d+|Ikai))?(?:_(Phase\d))?_Main", scene)
    if m:
        chapter = f"{CHAPTER_OF_FINAL[m.group(1)]} (Level {m.group(2)})"
        if m.group(3):
            return chapter + " Boss" + (f" Phase {m.group(3)[-1]}" if "Phase" in m.group(3) else "")
        place = m.group(4)
        if place == "Ikai":
            return chapter + " Demonic Realm"
        label = f"{chapter} Area {place[1:]}" if place else chapter
        return label + (f" Phase {m.group(5)[-1]}" if m.group(5) else "")
    return scene.replace("_Main", "").replace("_", " ")


def typed_item_name(definition, localized):
    """
    The name of a game item as Archipelago shows it: the game's own name, with its type in front for skills, combos and
    omamori ("Skill Phantom Blade", "Combo Blazing Thrust", "Omamori Life Charm (S)") so they are easy to tell apart and
    to search for. A name that already says what it is ("Jinguu Omamori") is left alone.
    """
    name = localized.get(definition, definition)
    if definition.startswith("Skill_"):
        prefix = "Skill"
    elif definition.startswith("Omamori_"):
        prefix = "Omamori"
    elif "Combo" in definition:
        prefix = "Combo"
    else:
        return name
    return name if prefix in name else f"{prefix} {name}"


def chapter_label(chapter_id):
    """"Level1" -> "Level 1", "LevelFinalP2" -> "Chapter 7"."""
    m = re.fullmatch(r"Level(\d)", chapter_id)
    if m:
        return f"Level {m.group(1)}"
    return CHAPTER_OF_FINAL[chapter_id.replace("LevelFinal", "")]


def ambush_code(tail):
    """"L2C01" -> "C01" (the level is already part of the place)."""
    return re.sub(r"^L\d", "", tail)


def note_name(definition, localized):
    """The journal's in-game name, or a readable form of its code name when the game's name is not known yet."""
    if definition in localized:
        return localized[definition]
    parts = definition.replace("Codex_Note_", "").split("_")
    words = " ".join(re.sub(r"(?<!^)(?=[A-Z])|(?<=[a-z])(?=\d)", " ", p) for p in parts)
    return words.replace("Final Level", "Final")


def location_name(rec, localized):
    """The readable name of one location record (see data/locations.json)."""
    name = base_location_name(rec, localized)
    part = rec["key"].partition("#")[2]
    if part and rec["category"] in ("stage_clear", "tournament", "trial"):
        return f"{name} (Part {part})"  # the later parts of a reward screen that hands out several things
    return name


def base_location_name(rec, localized):
    key, category = rec["key"], rec["category"]
    tail = key.split("/", 1)[1]  # the key without its category prefix: "chest/<scene>/2" -> "<scene>/2"
    if category in ("stage_clear", "tournament", "trial"):
        tail = tail.partition("#")[0]  # the parts of one reward screen are named alike (assign_names numbers them)
    scene = rec.get("scene")
    place = scene_label(scene) if scene else None

    if category == "chest":
        return f"{place}: Chest {tail.rsplit('/', 1)[1]}"
    if category == "ambush_chest":
        return f"{place}: Ambush Chest ({ambush_code(tail)})"
    if category == "ambush_reward":
        return f"{place}: Ambush Reward ({ambush_code(tail)})"
    if category == "head_chase":
        return f"{chapter_label(tail)}: Kushinada Head Chase"
    if category == "senbei":
        index = tail.rsplit("/", 1)[1]
        if rec.get("senbei_type") == "settlement":
            return f"{place}: Encounter Reward Senbei {index}"
        return f"{place}: Senbei {index}"
    if category == "journal":
        return f"Journal: {note_name(tail, localized)}"
    if category == "shop":
        definition, _, stage = tail.partition("#")
        name = typed_item_name(definition, localized)
        return f"Shop: {name} ({stage})" if stage else f"Shop: {name}"
    if category == "stage_clear":
        return f"{chapter_label(tail)} Clear Reward"
    if category == "harumi":
        return f"Harumi: Stage {tail}"
    if category == "tournament":
        return "Tournament Reward"
    if category == "stall":
        return "Festival Stall Treat"
    if category == "tutorial_clear":
        return "Tutorial Clear Reward"
    if category == "trial":
        return f"{place}: Glitched Spirit Trial {tail[2:]}"
    if category == "handover":
        vanilla = rec["vanilla"].split(";")[0].strip()
        return f"{place}: {typed_item_name(vanilla, localized)}"
    if category == "encounter":
        label = tail.rsplit("/", 1)[1]
        return f"{place}: Fight" if label == "main" else f"{place}: Encounter {label}"
    raise ValueError("no readable name for " + key)


def assign_names(locations, localized):
    """Adds a unique readable "name" to every location record (a repeated name gets " (2)", " (3)", ... appended)."""
    seen = {}
    for rec in locations:
        name = location_name(rec, localized)
        count = seen[name] = seen.get(name, 0) + 1
        rec["name"] = name if count == 1 else f"{name} ({count})"
