"""
The Soul Cores of the five bosses: items of the pool that Chapters 6-8 need to open the way to the fight with the same
boss again (the plugin keeps the door shut), handed out by the hub as worlds are cleared (the n-th core after the
n-th world).
"""
from ..game_data import LOCATIONS
from .test_rules import HomuraHimeLogicBase

# the region of each boss fight in Chapters 6-8 and the core its entrance needs
BOSS_REGION_AND_CORE = {
    "LevelFinalP1_L2_Boss": "Ling Ling's Soul Core",
    "LevelFinalP2_L3_Boss": "Chirakuin's Soul Core",
    "LevelFinalP2_L4_Boss": "Yumiko & Kumiko's Soul Core",
    "LevelFinalP3_L5_A2_Entrance": "Agasa's Soul Core",
    "LevelFinalP3_L5_Boss": "Mamiya's Soul Core",
}
CORES = ["Agasa's Soul Core", "Ling Ling's Soul Core", "Chirakuin's Soul Core", "Yumiko & Kumiko's Soul Core",
         "Mamiya's Soul Core"]


class TestSoulCores(HomuraHimeLogicBase):
    def pool_names(self):
        return [i.name for i in self.multiworld.itempool if i.player == self.player]

    def test_there_are_five_progression_items_in_a_group_of_their_own(self):
        self.assertEqual(set(CORES), self.world.item_name_groups["Soul Cores"])
        for name in CORES:
            with self.subTest(item=name):
                self.assertEqual(1, self.pool_names().count(name))
                self.assertTrue(self.world.create_item(name).advancement)

    def test_the_hub_hands_out_the_nth_core_when_the_nth_world_is_cleared(self):
        # the hub follows the number of cleared worlds, not which ones: clearing Chapters 6, 7 and 8 first hands out the
        # cores of Levels 1, 2 and 3 (seen in the game)
        cores = sorted((loc for loc in LOCATIONS if loc["category"] == "soul_core"), key=lambda loc: loc["cleared"])
        self.assertEqual([1, 2, 3, 4, 5], [loc["cleared"] for loc in cores])
        clears = [f"Chapter {n} Cleared" for n in (6, 7, 8, 5, 4)]
        for loc in cores:
            with self.subTest(location=loc["name"]):
                n = loc["cleared"]
                self.assertFalse(self.can_reach(loc["name"], self.state_with(clears[:n - 1], sweep=False)))
                self.assertTrue(self.can_reach(loc["name"], self.state_with(clears[:n], sweep=False)))

    def test_the_climax_counts_as_a_world_for_the_hub(self):
        location = next(loc["name"] for loc in LOCATIONS if loc["category"] == "soul_core" and loc["cleared"] == 2)
        self.assertFalse(self.can_reach(location, self.state_with(["Climax Cleared"], sweep=False)))
        self.assertTrue(self.can_reach(location, self.state_with(["Climax Cleared", "Chapter 1 Cleared"], sweep=False)))

    def everything_but(self, core):
        names = [n for n in self.pool_names() if n != core]
        names += [i.name for i in self.multiworld.precollected_items[self.player]]
        return self.state_with(names)

    def test_every_boss_fight_needs_the_core_of_that_boss(self):
        for region_name, core in BOSS_REGION_AND_CORE.items():
            region = self.multiworld.get_region(region_name, self.player)
            with self.subTest(region=region_name):
                self.assertFalse(region.can_reach(self.everything_but(core)), f"reachable without {core}")
                self.assertTrue(region.can_reach(self.state_with(self.pool_names())), f"not reachable with {core}")

    def test_the_old_chapters_do_not_need_a_core(self):
        # the bosses of Chapters 1-5 are fought before the cores exist
        for region_name in ("Level1_BossRoom_Pass1", "Level2_BossRoom", "Level5_BossRoom"):
            region = self.multiworld.get_region(region_name, self.player)
            with self.subTest(region=region_name):
                self.assertTrue(region.can_reach(self.state_with([n for n in self.pool_names() if n not in CORES])))


class TestSoulCoresAreAlwaysShuffled(HomuraHimeLogicBase):
    """A core left in its vanilla place (the clear reward of its chapter) would be out of reach in a shuffled world
    order."""
    options = {"shuffle_key_items": False, "level_order": "full"}

    def test_the_cores_stay_in_the_pool_when_key_items_are_not_shuffled(self):
        names = [i.name for i in self.multiworld.itempool if i.player == self.player]
        for core in CORES:
            self.assertIn(core, names)
