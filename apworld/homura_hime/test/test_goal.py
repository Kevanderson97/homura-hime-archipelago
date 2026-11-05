"""
The goal option: the Finale opens once every world is cleared (all_worlds) or once Chapters 6, 7 and 8 are cleared,
which takes every Soul Core (return_all_soul_cores).
"""
from .test_rules import HomuraHimeLogicBase

FINALE_REGION = "LevelFinalP4_Passage_Entrance"
CHAPTER_6_TO_8 = ["Chapter 6 Cleared", "Chapter 7 Cleared", "Chapter 8 Cleared"]
OTHER_CLEARS = [f"Chapter {n} Cleared" for n in range(1, 6)] + ["Climax Cleared"]


class TestAllWorldsGoal(HomuraHimeLogicBase):
    def finale(self):
        return self.multiworld.get_region(FINALE_REGION, self.player)

    def test_it_is_the_default_and_is_sent_to_the_plugin(self):
        self.assertEqual("all_worlds", self.world.options.goal.current_key)
        self.assertEqual("all_worlds", self.world.fill_slot_data()["goal"])

    def test_chapters_6_to_8_alone_do_not_open_the_finale(self):
        self.assertFalse(self.finale().can_reach(self.state_with(CHAPTER_6_TO_8, sweep=False)))

    def test_every_world_opens_it(self):
        self.assertTrue(self.finale().can_reach(self.state_with([*CHAPTER_6_TO_8, *OTHER_CLEARS], sweep=False)))


class TestReturnAllSoulCoresGoal(HomuraHimeLogicBase):
    options = {"goal": "return_all_soul_cores", "level_unlock_items": "freeform_progression"}

    def finale(self):
        return self.multiworld.get_region(FINALE_REGION, self.player)

    def test_it_is_sent_to_the_plugin(self):
        self.assertEqual("return_all_soul_cores", self.world.fill_slot_data()["goal"])

    def test_chapters_6_to_8_open_the_finale_without_the_other_worlds(self):
        self.assertTrue(self.finale().can_reach(self.state_with(CHAPTER_6_TO_8, sweep=False)))

    def test_every_one_of_the_three_is_needed(self):
        for left_out in CHAPTER_6_TO_8:
            with self.subTest(left_out=left_out):
                clears = [c for c in CHAPTER_6_TO_8 if c != left_out]
                self.assertFalse(self.finale().can_reach(self.state_with([*clears, *OTHER_CLEARS], sweep=False)))

    def test_nothing_is_open_without_a_clear(self):
        self.assertFalse(self.finale().can_reach(self.state_with([], sweep=False)))
