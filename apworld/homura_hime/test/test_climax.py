"""The climax is a world of its own: shuffled like a chapter, never the first one, needed for the finale."""
from test.general import setup_multiworld

from .. import HomuraHimeWorld
from ..game_data import CHAPTERS, CLIMAX_ID, LABEL_OF
from .test_rules import HomuraHimeLogicBase

CHAPTER_CLEARS = [f"Chapter {n} Cleared" for n in range(1, 9)]


class TestClimaxInTheOrder(HomuraHimeLogicBase):
    def test_the_vanilla_order_plays_the_climax_between_chapters_5_and_6(self):
        order = self.world.order
        self.assertEqual(9, len(order))
        self.assertEqual(["Level5", CLIMAX_ID, "LevelFinalP1"], order[4:7])

    def test_the_climax_and_chapter_6_are_separate_worlds(self):
        self.assertEqual("Climax", LABEL_OF[CLIMAX_ID])
        self.assertEqual("Chapter 6", LABEL_OF["LevelFinalP1"])
        self.assertEqual(9, len(CHAPTERS))
        self.assertIn("Climax Unlock", self.world.item_name_to_id)


class TestRandomizedOrder(HomuraHimeLogicBase):
    options = {"level_order": "randomized"}

    def test_the_climax_is_never_first_but_can_be_anywhere_else(self):
        positions = set()
        for seed in range(150):
            world = setup_multiworld([HomuraHimeWorld], seed=seed, options=self.options).worlds[1]
            self.assertEqual(sorted(c["id"] for c in CHAPTERS), sorted(world.order))
            self.assertNotEqual(CLIMAX_ID, world.order[0], f"seed {seed}")
            positions.add(world.order.index(CLIMAX_ID))
        self.assertEqual(set(range(1, 9)), positions)


class TestFreeformKeys(HomuraHimeLogicBase):
    options = {"level_order": "randomized", "level_unlock_items": "freeform_progression"}

    def all_keys_but_the_first_world(self):
        return [f"{LABEL_OF[w]} Unlock" for w in self.world.order]

    def climax_region(self):
        return self.multiworld.get_region("LevelFinalP0_Jinguu", self.player)

    def test_the_climax_has_its_own_unlock_item_in_the_pool_or_at_the_start(self):
        names = [i.name for i in self.multiworld.itempool if i.player == self.player]
        names += [i.name for i in self.multiworld.precollected_items[self.player]]
        self.assertEqual(1, names.count("Climax Unlock"))
        self.assertEqual(1, names.count("Chapter 6 Unlock"), "Chapter 6 keeps its own unlock item")

    def test_the_climax_needs_a_cleared_chapter_even_with_its_key(self):
        keys = self.all_keys_but_the_first_world()
        self.assertFalse(self.climax_region().can_reach(self.state_with(keys, sweep=False)))
        self.assertTrue(self.climax_region().can_reach(self.state_with(keys + ["Chapter 1 Cleared"], sweep=False)))

    def test_the_climax_needs_its_key(self):
        keys = [k for k in self.all_keys_but_the_first_world() if k != "Climax Unlock"]
        self.assertFalse(self.climax_region().can_reach(self.state_with(keys + ["Chapter 1 Cleared"], sweep=False)))

    def test_the_finale_needs_the_climax_and_all_eight_chapters(self):
        finale = self.multiworld.get_region("LevelFinalP4_Passage_Entrance", self.player)
        self.assertFalse(finale.can_reach(self.state_with(CHAPTER_CLEARS, sweep=False)), "the climax is missing")
        self.assertFalse(finale.can_reach(self.state_with(CHAPTER_CLEARS[:-1] + ["Climax Cleared"], sweep=False)),
                         "a chapter is missing")
        self.assertTrue(finale.can_reach(self.state_with(CHAPTER_CLEARS + ["Climax Cleared"], sweep=False)))


class TestVanillaOrderWithoutKeys(HomuraHimeLogicBase):
    def test_the_climax_follows_chapter_5_and_chapter_6_follows_the_climax(self):
        region = self.multiworld.get_region("LevelFinalP0_Jinguu", self.player)
        self.assertFalse(region.can_reach(self.state_with(CHAPTER_CLEARS[:4], sweep=False)))
        self.assertTrue(region.can_reach(self.state_with(CHAPTER_CLEARS[:5], sweep=False)))
        chapter_6 = self.multiworld.get_region("LevelFinalP1_L1_Ikai_Entrance", self.player)
        self.assertFalse(chapter_6.can_reach(self.state_with(CHAPTER_CLEARS[:5], sweep=False)))
        self.assertTrue(chapter_6.can_reach(self.state_with(CHAPTER_CLEARS[:5] + ["Climax Cleared"], sweep=False)))
