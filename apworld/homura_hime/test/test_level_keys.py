"""level_unlock_items: off, linear_progression (Progressive Level Keys) and freeform_progression (a key per world).

A state made by the helpers starts with the items every player starts with, so the first world's key is already in it."""
from BaseClasses import CollectionState

from ..game_data import CHAPTERS, LABEL_OF, LOCATIONS
from .test_rules import HomuraHimeLogicBase
from .test_shop_budget import generate

PROGRESSIVE = "Progressive Level Key"
FIRST_REGION = {c["id"]: c["sub_chapters"][0]["key"] for c in CHAPTERS}
WORLD_OF_REGION = {sub["key"]: c["id"] for c in CHAPTERS for sub in c["sub_chapters"]}
WORLD_OF_LOCATION = {loc["name"]: WORLD_OF_REGION.get(loc["region"]) for loc in LOCATIONS}  # None for the hub


class KeyTestBase(HomuraHimeLogicBase):
    def enterable(self, state):
        """The worlds the state can walk into from the hub, in play order."""
        return [w for w in self.world.order if self.multiworld.get_region(FIRST_REGION[w], self.player).can_reach(state)]

    def start_inventory(self):
        return [i.name for i in self.multiworld.precollected_items[self.player]]

    def pool_names(self):
        return [i.name for i in self.multiworld.itempool if i.player == self.player]

    @staticmethod
    def cleared(*worlds):
        return [f"{LABEL_OF[w]} Cleared" for w in worlds]


class TestLinearKeys(KeyTestBase):
    options = {"level_order": "randomized", "level_unlock_items": "linear_progression"}

    def test_one_key_is_held_from_the_start_and_eight_are_in_the_pool(self):
        self.assertEqual([PROGRESSIVE], self.start_inventory())
        self.assertEqual(8, self.pool_names().count(PROGRESSIVE))
        self.assertFalse([n for n in self.pool_names() if n.endswith(" Unlock")], "no single keys in this mode")

    def test_the_first_world_is_open_and_the_next_needs_a_key_and_the_clear(self):
        order = self.world.order
        self.assertEqual([order[0]], self.enterable(self.state_with([], sweep=False)))
        self.assertEqual([order[0]], self.enterable(self.state_with(self.cleared(order[0]), sweep=False)),
                         "the clear alone does not open the next world")
        self.assertEqual([order[0]], self.enterable(self.state_with([PROGRESSIVE], sweep=False)),
                         "the key alone does not open the next world")
        self.assertEqual(order[:2], self.enterable(self.state_with([PROGRESSIVE] + self.cleared(order[0]), sweep=False)))

    def test_a_world_far_ahead_needs_all_the_keys_before_it_and_every_clear(self):
        order = self.world.order
        clears = self.cleared(*order[:-1])
        self.assertEqual(order[:-1], self.enterable(self.state_with([PROGRESSIVE] * 7 + clears, sweep=False)))
        self.assertEqual(order, self.enterable(self.state_with([PROGRESSIVE] * 8 + clears, sweep=False)))
        self.assertEqual(order[:1], self.enterable(self.state_with([PROGRESSIVE] * 8, sweep=False)), "keys without clears")

    def test_the_finale_needs_every_clear_as_before(self):
        finale = self.multiworld.get_region("LevelFinalP4_Passage_Entrance", self.player)
        everything = [PROGRESSIVE] * 8 + self.cleared(*self.world.order)
        self.assertTrue(finale.can_reach(self.state_with(everything, sweep=False)))
        self.assertFalse(finale.can_reach(self.state_with(everything[:-1], sweep=False)))


class TestFreeformKeys(KeyTestBase):
    options = {"level_order": "vanilla", "level_unlock_items": "freeform_progression"}

    def test_the_first_key_is_held_and_the_other_eight_are_in_the_pool(self):
        self.assertEqual(["Chapter 1 Unlock"], self.start_inventory())
        self.assertEqual(8, len([n for n in self.pool_names() if n.endswith(" Unlock")]))
        self.assertNotIn(PROGRESSIVE, self.pool_names())

    def test_a_key_opens_its_world_whatever_the_order_says(self):
        self.assertEqual(["Level1", "Level5"], self.enterable(self.state_with(["Chapter 5 Unlock"], sweep=False)))
        self.assertEqual(["Level1", "Level3", "Level5"],
                         self.enterable(self.state_with(["Chapter 5 Unlock", "Chapter 3 Unlock"], sweep=False)))

    def test_the_climax_still_needs_a_cleared_chapter(self):
        self.assertEqual(["Level1"], self.enterable(self.state_with(["Climax Unlock"], sweep=False)))
        self.assertEqual(["Level1", "LevelFinalP0"],
                         self.enterable(self.state_with(["Climax Unlock", "Chapter 1 Cleared"], sweep=False)))


class TestNoKeys(KeyTestBase):
    options = {"level_order": "randomized"}

    def test_there_are_no_keys_at_all(self):
        self.assertFalse([n for n in self.pool_names() + self.start_inventory() if n.endswith(" Unlock") or n == PROGRESSIVE])

    def test_the_order_alone_opens_the_worlds(self):
        order = self.world.order
        self.assertEqual([order[0]], self.enterable(self.state_with([], sweep=False)))
        self.assertEqual(order[:2], self.enterable(self.state_with(self.cleared(order[0]), sweep=False)))


class TestLinearKeysKeepGateItemsOutOfLaterWorlds(KeyTestBase):
    """A gate item is never hidden in a world that comes after the world that needs it (Nurikabe's Hat is needed in Level 3)."""

    def test_the_hat_is_not_placed_in_a_world_after_level_3(self):
        for seed in range(40):
            multiworld = generate(seed, {"level_order": "randomized", "level_unlock_items": "linear_progression"}, 1, check=False)
            order = multiworld.worlds[1].order
            self.assertTrue(multiworld.can_beat_game(CollectionState(multiworld)), f"seed {seed}")
            hat = next(l for l in multiworld.get_locations(1) if l.item is not None and l.item.name == "Nurikabe's Hat")
            world = WORLD_OF_LOCATION.get(hat.name)
            if world is not None:  # a place in the hub is open from the start
                self.assertLessEqual(order.index(world), order.index("Level3"),
                                     f"seed {seed}: the hat is in {hat.name}, a later world than Level 3: {order}")

    def test_freeform_keys_can_still_hide_it_in_a_later_world(self):
        """With a key per world the player jumps between worlds, so the old placements stay possible."""
        later = 0
        for seed in range(40):
            multiworld = generate(seed, {"level_order": "randomized", "level_unlock_items": "freeform_progression"}, 1, check=False)
            order = multiworld.worlds[1].order
            hat = next(l for l in multiworld.get_locations(1) if l.item is not None and l.item.name == "Nurikabe's Hat")
            world = WORLD_OF_LOCATION.get(hat.name)
            if world is not None and order.index(world) > order.index("Level3"):
                later += 1
        self.assertGreater(later, 0)
