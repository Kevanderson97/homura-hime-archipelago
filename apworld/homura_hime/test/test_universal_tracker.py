"""
Universal Tracker support. UT does not see the seed of the real generation: it generates the world again from the slot
data (re_gen_passthrough) with other random draws and replays what the player received to tell which locations are in
logic. The replay has to agree with the real generation, sphere by sphere (the check of the fuzzer's "check-ut"). These
tests do that replay themselves, so they need no tracker; one more test runs the tracker's own core when it is
installed.
"""
import json
import random
import unittest

from BaseClasses import CollectionState
from test.general import setup_multiworld
from worlds.AutoWorld import call_all

from .. import GAME_NAME, SCALE_SLOT_DATA, HomuraHimeWorld
from .test_option_sweep import combination
from .test_shop_budget import generate

# the generation steps UT runs (worlds/tracker/TrackerCore.py, TMain)
UT_STEPS = ("generate_early", "create_regions", "create_items", "set_rules", "connect_entrances", "generate_basic")


def slot_data_of(multiworld) -> dict:
    """The slot data as a client receives it (it travels as json)."""
    return json.loads(json.dumps(multiworld.worlds[1].fill_slot_data()))


def regenerate(slot_data: dict, seed: int, options: dict | None = None):
    """The world as UT builds it: generated again, with other random draws, from the slot data (and the yaml's options,
    or none at all without a yaml)."""
    multiworld = setup_multiworld(HomuraHimeWorld, steps=(), seed=seed, options=options or {})
    multiworld.generation_is_fake = True
    multiworld.re_gen_passthrough = {GAME_NAME: slot_data}
    for step in UT_STEPS:
        call_all(multiworld, step)
    return multiworld


def sweep_events(tracked, state) -> None:
    """Like UT: only the events are collected by themselves, the items of real locations come from the server."""
    state.sweep_for_advancements(locations=[loc for loc in tracked.get_locations(1) if not loc.address])


def disagreements(real, tracked) -> list[str]:
    """Where the logic of the tracked world differs from the real one, replaying the real playthrough sphere by sphere:
    the locations in logic for what has been received must be exactly the next sphere."""
    world = tracked.worlds[1]
    # like UT: the items the world handed itself at the start (the first level key) are received from the server instead
    tracked.precollected_items = {player: [item for item in items if item.code is None]
                                  for player, items in tracked.precollected_items.items()}
    state = CollectionState(tracked)
    for item in real.precollected_items[1]:
        state.collect(world.create_item(item.name), True)
    sweep_events(tracked, state)
    checked: set[str] = set()
    problems = []
    for number, sphere in enumerate(real.get_sendable_spheres()):
        expected = {loc.name for loc in sphere if loc.address is not None}
        if not expected:
            break
        in_logic = {
            loc.name for loc in tracked.get_locations(1)
            if loc.address is not None and loc.name not in checked and loc.can_reach(state)
        }
        if in_logic != expected:
            problems.append(f"sphere {number}: tracker only {sorted(in_logic - expected)[:4]}, "
                            f"real only {sorted(expected - in_logic)[:4]}")
            break
        checked |= expected
        for loc in sphere:
            if loc.address is not None:
                state.collect(world.create_item(loc.item.name), True)
        sweep_events(tracked, state)
    return problems


class TestSlotData(unittest.TestCase):
    def test_the_slot_data_carries_what_the_pool_drew(self):
        slot_data = slot_data_of(generate(5, {}, 1))
        for name in SCALE_SLOT_DATA:
            self.assertIn(name, slot_data)
        self.assertIn(slot_data["senbei_unit"], ("Senbei", "2 Senbei"))
        for name in ("money_scale", "petal_scale", "senbei_scale"):
            self.assertGreater(slot_data[name], 0)

    def test_a_regeneration_uses_the_scales_of_the_real_generation(self):
        slot_data = slot_data_of(generate(6, {}, 1))
        for seed in range(3):
            world = regenerate(slot_data, 100 + seed).worlds[1]
            for name in SCALE_SLOT_DATA:
                self.assertEqual(slot_data[name], getattr(world, name), name)


class TestTrackerAgreesWithTheRealGeneration(unittest.TestCase):
    """The replay of the fuzzer, with options that moved the logic before (the scales of the currency depend on the
    random filler) and a number of random combinations."""

    OPTIONS = [
        {},
        {"goal": "return_all_soul_cores", "level_order": "full", "level_unlock_items": "freeform_progression"},
        {"level_order": "balanced", "level_unlock_items": "linear_progression", "optional_locations": False},
        {"filler_sakura_ratio": 100, "senbei_count": 21, "minimum_konpeito": 291359, "optional_locations": False},
        {"filler_sakura_ratio": 0, "senbei_count": 80, "minimum_konpeito": 1000000},
        {"advanced_movement_required": True, "include_cut_content": True, "shuffle_key_items": False},
    ]

    def check(self, options: dict, seed: int, with_yaml: bool) -> None:
        real = generate(seed, options, 1)
        slot_data = slot_data_of(real)
        tracked = regenerate(slot_data, seed + 7919, options if with_yaml else None)
        self.assertEqual([], disagreements(real, tracked), f"seed {seed}, options {options}")

    def test_with_the_yaml(self):
        for seed, options in enumerate(self.OPTIONS):
            with self.subTest(options=options):
                self.check(options, seed, with_yaml=True)

    def test_without_a_yaml(self):
        # a world that tells UT it needs no yaml (ut_can_gen_without_yaml) is built from the slot data alone
        for seed, options in enumerate(self.OPTIONS):
            with self.subTest(options=options):
                self.check(options, seed, with_yaml=False)

    def test_random_combinations(self):
        rng = random.Random(20261107)
        for seed in range(40):
            options = combination(rng)
            with self.subTest(seed=seed, options=options):
                self.check(options, seed, with_yaml=seed % 2 == 0)

    def test_the_world_needs_no_yaml(self):
        self.assertTrue(HomuraHimeWorld.ut_can_gen_without_yaml)


class TestTrackerCore(unittest.TestCase):
    """The tracker's own core, when it is installed (tracker.apworld in custom_worlds or worlds)."""

    def test_the_tracker_follows_a_generated_seed_without_a_yaml(self):
        try:
            from worlds.tracker import TrackerCore
        except ImportError:
            self.skipTest("Universal Tracker is not installed")
        import logging

        real = generate(3, {"goal": "return_all_soul_cores", "level_order": "full",
                            "level_unlock_items": "freeform_progression"}, 1)
        core = TrackerCore.TrackerCore(logging.getLogger("UT"), False, False)
        core.set_slot_params(GAME_NAME, 1, real.player_name[1], 1)
        core.initalize_tracker_core(HomuraHimeWorld, slot_data_of(real))
        self.assertFalse(core.tracker_disabled, core.gen_error)
        self.assertIsNotNone(core.multiworld, core.gen_error)

        expected = {loc.address for loc in real.get_locations(1) if loc.address is not None}
        core.set_missing_locations(set(expected))
        core.set_items_received([])
        update = core.updateTracker()
        tracked = {loc.address for loc in core.multiworld.get_locations(1) if loc.address is not None}
        self.assertEqual(expected, tracked, "the tracker's world has other locations than the real one")
        self.assertTrue(update.in_logic_locations, "nothing is in logic at the start")


if __name__ == "__main__":
    unittest.main()
