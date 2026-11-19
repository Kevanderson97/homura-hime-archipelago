"""
skip_early_macguffin_checks: when Chapter 1 is the first world played, the Pretty Little Flower is left to the story: it
is not in the pool, its hand-over is not a location, and the logic gets it as an event of the first area. With another
world first, or with the option off, the Flower is an item and its hand-over a location like any other.
"""
from test.general import setup_multiworld

from .. import HomuraHimeWorld
from ..game_data import DASH_SKILLS
from .test_rules import HomuraHimeLogicBase, scene_location

FLOWER = "Pretty Little Flower"
FLOWER_LOCATION = "Level 1 Area 1: Pretty Little Flower"
FLOWER_EVENT = "Pretty Little Flower (from the story)"


def real_items_named(multiworld, player, name):
    """The items of the multiworld with this name that are real items (not the logic's event markers)."""
    items = list(multiworld.itempool) + [loc.item for loc in multiworld.get_locations(player) if loc.item is not None]
    return [i for i in items if i.name == name and i.player == player and i.code is not None]


class TestChapterOneFirst(HomuraHimeLogicBase):
    """The default options: the world order is the story's, so Chapter 1 is first and the option (on by default)
    applies."""

    def test_the_option_is_on_by_default(self):
        self.assertTrue(self.world.options.skip_early_macguffin_checks)

    def test_the_flower_is_not_a_location_or_an_item(self):
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        self.assertNotIn(FLOWER_LOCATION, names)
        self.assertEqual([], real_items_named(self.multiworld, self.player, FLOWER))
        self.assertNotIn(FLOWER, {i.name for i in self.multiworld.precollected_items[self.player]})

    def test_the_story_hands_it_over_in_the_logic_at_the_start_of_the_chapter(self):
        event = self.multiworld.get_location(FLOWER_EVENT, self.player)
        self.assertIsNone(event.address)
        self.assertEqual(FLOWER, event.item.name)
        self.assertIsNone(event.item.code)
        self.assertEqual("Level1_Area_1_Entrance", event.parent_region.name)

    def test_chapter_one_needs_no_flower_item_but_still_the_dash(self):
        loc = scene_location("Level_1_Area_3_Main")
        self.assertFalse(self.can_reach(loc, self.state_with([])), "the dash is still needed")
        self.assertTrue(self.can_reach(loc, self.state_with([DASH_SKILLS[0]])), "no Flower item is needed")

    def test_the_option_reaches_the_slot_data(self):
        self.assertTrue(self.world.fill_slot_data()["skip_early_macguffin_checks"])


class TestOptionOff(HomuraHimeLogicBase):
    options = {"skip_early_macguffin_checks": False}

    def test_the_flower_is_an_item_and_its_hand_over_a_location(self):
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        self.assertIn(FLOWER_LOCATION, names)
        self.assertEqual(1, len(real_items_named(self.multiworld, self.player, FLOWER)))
        self.assertNotIn(FLOWER_EVENT, names)

    def test_level_one_needs_the_flower_item(self):
        loc = scene_location("Level_1_Area_3_Main")
        self.assertFalse(self.can_reach(loc, self.state_with([DASH_SKILLS[0]])))
        self.assertTrue(self.can_reach(loc, self.state_with([FLOWER, DASH_SKILLS[0]])))

    def test_the_slot_data_says_so(self):
        self.assertFalse(self.world.fill_slot_data()["skip_early_macguffin_checks"])


class TestAnotherWorldFirst(HomuraHimeLogicBase):
    """With a shuffled order that does not begin with Chapter 1, the option has nothing to skip."""
    options = {"level_order": "full"}

    def setUp(self):
        # a seed whose order starts with another world
        for seed in range(1, 200):
            self.multiworld = setup_multiworld(HomuraHimeWorld, seed=seed, options=self.options)
            if self.multiworld.worlds[1].order[0] != "Level1":
                break
        self.player = 1
        self.world = self.multiworld.worlds[1]

    def test_the_flower_stays_an_item_and_a_location(self):
        self.assertNotEqual("Level1", self.world.order[0])
        self.assertTrue(self.world.options.skip_early_macguffin_checks)
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        self.assertIn(FLOWER_LOCATION, names)
        self.assertNotIn(FLOWER_EVENT, names)
        self.assertEqual(1, len(real_items_named(self.multiworld, self.player, FLOWER)))


class TestChapterOneFirstInAShuffledSetup(HomuraHimeLogicBase):
    """Chapter 1 first by freeform keys with the world order left alone: the first world is the starting one."""
    options = {"level_unlock_items": "freeform_progression"}

    def test_the_flower_is_skipped(self):
        self.assertEqual("Level1", self.world.order[0])
        names = {loc.name for loc in self.multiworld.get_locations(self.player)}
        self.assertNotIn(FLOWER_LOCATION, names)
        self.assertEqual([], real_items_named(self.multiworld, self.player, FLOWER))
