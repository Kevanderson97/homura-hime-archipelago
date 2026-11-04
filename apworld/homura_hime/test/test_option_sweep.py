"""
Generates the world with many random combinations of its options. Every combination is a test of its own (the checks of
WorldTestBase: all locations reachable, the game beatable, nothing left over), so an option set that cannot be generated or
completed shows up with its options in the name. A fixed number of combinations runs by default, built from a fixed seed so
a failure repeats; for a long sweep set HH_SWEEP (how many) and HH_SWEEP_SEED (which sequence) in the environment.
"""
import os
import random
import unittest

from . import test_generation  # the base class is used through the module so that pytest does not collect it here again

SWEEP_SIZE = int(os.environ.get("HH_SWEEP", "24"))
SWEEP_SEED = int(os.environ.get("HH_SWEEP_SEED", "20261022"))

TOGGLES = ["advanced_movement_required", "include_cut_content", "encounter_rewards",
           "encounter_clears", "death_link", "death_link_skip_revives", "shop_hints",
           "shuffle_combos", "shuffle_skills", "shuffle_omamori", "shuffle_guns", "shuffle_shards",
           "shuffle_upgrades", "shuffle_key_items"]
# (name, lowest, highest, the values worth trying besides a random one)
RANGES = [("dango_count", 0, 99, (0, 15, 99)),
          ("optional_location_importance", 0, 100, (0, 50, 100)),
          ("senbei_count", 20, 80, (20, 55, 80)),
          ("full_heal_count", 0, 50, (0, 10, 50)),
          ("ultimate_charge_count", 0, 50, (0, 10, 50)),
          ("filler_sakura_ratio", 0, 100, (0, 20, 100)),
          ("minimum_konpeito", 0, 1000000, (0, 550000, 1000000))]
ITEM_GROUPS = ["Key Items", "Dash Skills", "Chapter Unlocks", "Guns", "Omamori", "Omamori Upgrades", "Combos", "Skills",
               "Abilities", "Shards And Crystals", "Currency", "Utility"]


def combination(rng: random.Random) -> dict:
    options = {"level_order": rng.choice(["off", "balanced", "full"]),
               "level_unlock_items": rng.choice(["off", "linear_progression", "freeform_progression"])}
    for name in TOGGLES:
        options[name] = rng.random() < 0.5
    for name, low, high, special in RANGES:
        options[name] = rng.choice(special) if rng.random() < 0.5 else rng.randint(low, high)
    if rng.random() < 0.3:
        options["local_items"] = set(rng.sample(ITEM_GROUPS, rng.randint(1, len(ITEM_GROUPS))))
    return options


def describe(options: dict) -> str:
    """The options that differ from a plain game, short enough for a test name."""
    return ",".join(f"{k}={sorted(v) if isinstance(v, set) else int(v) if isinstance(v, bool) else v}"
                    for k, v in sorted(options.items()))


def _make_tests() -> None:
    rng = random.Random(SWEEP_SEED)
    for index in range(SWEEP_SIZE):
        options = combination(rng)
        name = f"TestSweep{index:03d}"
        globals()[name] = type(name, (test_generation.HomuraHimeTestBase,), {"options": options, "seed": SWEEP_SEED + index,
                                                             "__doc__": describe(options), "__module__": __name__})


_make_tests()

if __name__ == "__main__":
    unittest.main()
