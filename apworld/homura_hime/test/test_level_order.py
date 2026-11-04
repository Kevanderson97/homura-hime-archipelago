"""level_order: off, balanced and full, and what freeform level keys do with the order."""
import random

from .. import EARLY_WORLDS
from ..game_data import CHAPTER_OF_REGION, CHAPTERS, CLIMAX_ID, LABEL_OF, LOCATIONS
from .test_rules import HomuraHimeLogicBase
from .test_shop_budget import generate

STORY_ORDER = [c["id"] for c in CHAPTERS]
LATE_WORLDS = [w for w in STORY_ORDER if w not in EARLY_WORLDS]
LOCATION_BY_NAME = {loc["name"]: loc for loc in LOCATIONS}


class TestTheOptionNames(HomuraHimeLogicBase):
    def test_off_balanced_and_full_and_the_old_names(self):
        option = self.world.options.level_order
        self.assertEqual("off", option.current_key)
        self.assertEqual(option.option_off, option.options["vanilla"])
        self.assertEqual(option.option_full, option.options["randomized"])
        self.assertNotEqual(option.option_balanced, option.option_full)

    def test_off_keeps_the_story_order(self):
        self.assertEqual(STORY_ORDER, self.world.order)


class TestBalancedOrder(HomuraHimeLogicBase):
    options = {"level_order": "balanced"}
    RUNS = 2000

    def orders(self):
        for n in range(self.RUNS):
            self.world.random = random.Random(n)
            yield self.world._balanced_order()

    def test_every_order_has_all_nine_worlds_and_the_climax_is_never_first(self):
        for order in self.orders():
            self.assertEqual(sorted(STORY_ORDER), sorted(order))
            self.assertNotEqual(CLIMAX_ID, order[0])

    def test_the_first_world_is_always_one_of_chapters_1_to_4(self):
        for order in self.orders():
            self.assertIn(order[0], EARLY_WORLDS)

    def test_the_second_world_is_a_chapter_1_to_4_two_times_in_three_the_third_four_times_in_ten(self):
        for slot, expected in ((1, 0.66), (2, 0.40)):
            early = sum(order[slot] in EARLY_WORLDS for order in self.orders())
            self.assertAlmostEqual(expected, early / self.RUNS, delta=0.03, msg=f"slot {slot}")

    def test_each_of_the_last_three_worlds_is_a_late_one_six_times_in_ten(self):
        for slot in (6, 7, 8):
            late = sum(order[slot] in LATE_WORLDS for order in self.orders())
            self.assertAlmostEqual(0.60, late / self.RUNS, delta=0.03, msg=f"slot {slot}")

    def test_the_middle_worlds_are_what_is_left(self):
        """Most early worlds are used up by the places before, so the middle three are mostly late worlds (about 25% early)."""
        for slot in (3, 4, 5):
            early = sum(order[slot] in EARLY_WORLDS for order in self.orders())
            self.assertGreater(early / self.RUNS, 0.15, f"slot {slot}")
            self.assertLess(early / self.RUNS, 0.35, f"slot {slot}")

    def test_any_other_world_can_be_anywhere(self):
        """Only the first place is closed to the late worlds; everywhere else both kinds can turn up."""
        seen = {(w, i) for order in self.orders() for i, w in enumerate(order)}
        missing = [(w, i) for w in STORY_ORDER for i in range(len(STORY_ORDER)) if (w, i) not in seen and not (i == 0 and w in LATE_WORLDS)]
        self.assertEqual([], missing)


class TestFreeformKeysFollowTheOrder(HomuraHimeLogicBase):
    """With a key per world the order is the order the keys turn up in: a world's key is never in that world or a later one."""

    def key_places(self, multiworld, world):
        """[(key world position, location world position)] of the keys that lie in this world's own locations."""
        order = world.order
        found = []
        for location in multiworld.get_locations(world.player):
            item = location.item
            if item is None or item.player != world.player or not item.name.endswith(" Unlock") or location.address is None:
                continue
            key_position = next(i for i, w in enumerate(order) if f"{LABEL_OF[w]} Unlock" == item.name)
            found.append((item.name, key_position, world._world_index(LOCATION_BY_NAME[location.name]), location.name))
        return found

    def check(self, options, seeds, players=1):
        checked = 0
        for seed in seeds:
            multiworld = generate(seed, options, players, check=False)
            for player in multiworld.player_ids:
                world = multiworld.worlds[player]
                for name, key_position, place, where in self.key_places(multiworld, world):
                    checked += 1
                    self.assertLess(place, key_position, f"seed {seed}: {name} (world {key_position} of the order) is at {where} (world {place})")
        self.assertGreater(checked, 0)

    def test_full_order(self):
        self.check({"level_order": "full", "level_unlock_items": "freeform_progression"}, range(30))

    def test_balanced_order(self):
        self.check({"level_order": "balanced", "level_unlock_items": "freeform_progression"}, range(30))

    def test_story_order(self):
        self.check({"level_order": "off", "level_unlock_items": "freeform_progression"}, range(20))

    def test_two_players(self):
        self.check({"level_order": "full", "level_unlock_items": "freeform_progression"}, range(10), players=2)

    def test_other_key_modes_are_not_limited_this_way(self):
        """Linear keys are progressive (nothing to place by world); without keys there is nothing to limit."""
        for keys in ("off", "linear_progression"):
            multiworld = generate(0, {"level_order": "full", "level_unlock_items": keys}, 1, check=False)
            self.assertEqual({}, multiworld.worlds[1].key_position)


class TestTheWorldOfALocation(HomuraHimeLogicBase):
    options = {"level_order": "full", "level_unlock_items": "freeform_progression"}

    def test_hub_places_follow_the_clear_that_opens_them(self):
        world = self.world
        for stage_clear in (l for l in LOCATIONS if l["category"] == "stage_clear" and l.get("chapter") in world.order):
            # (the Climax has no clear reward)
            self.assertEqual(world.order.index(stage_clear["chapter"]), world._world_index(stage_clear), stage_clear["name"])
        first_tier_shop = next(l for l in LOCATIONS if l["category"] == "shop" and l["tier"] <= 1)
        self.assertEqual(0, world._world_index(first_tier_shop))
        late_shop = max((l for l in LOCATIONS if l["category"] == "shop"), key=lambda l: l["tier"])
        self.assertEqual(late_shop["tier"] - 1, world._world_index(late_shop))

    def test_places_in_a_world_follow_the_order(self):
        world = self.world
        for position, chapter in enumerate(world.order):
            region = next(c for c in CHAPTERS if c["id"] == chapter)["sub_chapters"][0]["key"]
            self.assertEqual(chapter, CHAPTER_OF_REGION[region])
            self.assertEqual(position, world._world_index({"region": region, "category": "chest"}))
