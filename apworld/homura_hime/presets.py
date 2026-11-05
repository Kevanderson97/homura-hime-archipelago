"""Option presets, offered on the game's options page of the Archipelago website."""

# Every option at its default, so that choosing a preset also resets what an earlier choice changed. The world test
# test_presets.py checks that this stays equal to the defaults in options.py.
STANDARD = {
    "level_order": "off",
    "level_unlock_items": "off",
    "advanced_movement_required": False,
    "encounter_clears": True,
    "encounter_rewards": True,
    "include_cut_content": False,
    "optional_location_importance": 100,
    "shuffle_combos": True,
    "shuffle_skills": True,
    "shuffle_omamori": True,
    "shuffle_guns": True,
    "shuffle_shards": True,
    "shuffle_upgrades": True,
    "shuffle_key_items": True,
    "minimum_konpeito": 550000,
    "filler_sakura_ratio": 20,
    "senbei_count": 55,
    "dango_count": 15,
    "full_heal_count": 10,
    "ultimate_charge_count": 10,
    "puffer_swarm_traps": 20,
    "hexed_doll_trio_traps": 5,
    "shop_hints": True,
    "death_link": False,
    "death_link_skip_revives": False,
}

options_presets = {
    "Standard": dict(STANDARD),

    # a shuffled world order that keeps the story's pacing, played strictly in order with progressive keys, few traps
    "Balanced Random": {
        **STANDARD,
        "level_order": "balanced",
        "level_unlock_items": "linear_progression",
        "puffer_swarm_traps": 5,
        "hexed_doll_trio_traps": 2,
    },

    # everything that can be random is, and every extra location is in
    "Chaos": {
        **STANDARD,
        "level_order": "full",
        "level_unlock_items": "freeform_progression",
        "advanced_movement_required": True,
        "encounter_rewards": True,
        "include_cut_content": True,
        "death_link": True,
        "minimum_konpeito": "random",
        "dango_count": "random",
        "senbei_count": "random",
        "full_heal_count": "random",
        "ultimate_charge_count": "random",
        "filler_sakura_ratio": "random",
        "puffer_swarm_traps": "random",
        "hexed_doll_trio_traps": "random",
    },
}
