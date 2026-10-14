"""Tests of the access logic: gates, shop money, Senbei and Sakura counting, dash skills and the item pool."""
from BaseClasses import CollectionState, ItemClassification
from test.bases import WorldTestBase

from ..game_data import DASH_SKILLS, GATE_ITEMS, KEY_OF_NAME, LOCATIONS, NAME_OF_KEY, SENBEI_VALUES


def scene_location(scene):
    """Name of some location in the given scene."""
    return next(loc["name"] for loc in LOCATIONS
                if loc.get("scene") == scene and loc["category"] in ("chest", "senbei", "journal"))


class HomuraHimeLogicBase(WorldTestBase):
    """Generates a world and offers helpers to ask what a player holding only certain items can reach."""
    game = "Homura-Hime"
    options = {"dash_skill_start": False}

    def state_with(self, names, sweep=True):
        """A fresh state holding exactly these items (event items are only added by the sweep)."""
        state = CollectionState(self.multiworld)
        for n in names:
            state.collect(self._make(n), True)
        if sweep:
            state.sweep_for_advancements()
        return state

    def _make(self, name):
        """An item by name; the chapter events are made here because the world does not create them as items."""
        if name.endswith(("Cleared", "Entered")) or name == "Victory":
            from .. import HomuraHimeItem
            return HomuraHimeItem(name, ItemClassification.progression, None, self.player)
        return self.world.create_item(name)

    def can_reach(self, location_name, state):
        """Whether the named location of the player is reachable in the state."""
        return self.multiworld.get_location(location_name, self.player).can_reach(state)


class TestRules(HomuraHimeLogicBase):
    """The gates of the chapters and the shop tiers."""
    def test_level1_needs_flower_and_dash(self):
        loc = scene_location("Level_1_Area_3_Main")
        self.assertFalse(self.can_reach(loc, self.state_with([])))
        self.assertFalse(self.can_reach(loc, self.state_with(["Pretty Little Flower"])),
                         "flower alone must not be enough")
        self.assertFalse(self.can_reach(loc, self.state_with([DASH_SKILLS[0]])), "dash alone must not be enough")
        self.assertTrue(self.can_reach(loc, self.state_with(["Pretty Little Flower", DASH_SKILLS[0]])))

    def test_the_dash_is_asked_for_at_area_2_not_in_the_demonic_realm(self):
        # Level 1 runs Area 1 -> Demonic Realm -> Areas 2 and 3 -> boss; the gap is at the start of Area 2
        head_chase = self.multiworld.get_location(NAME_OF_KEY["head/Level1"], self.player)
        demonic_chest = self.multiworld.get_location("Level 1 Demonic Realm 1: Chest 1", self.player)
        area_2_chest = self.multiworld.get_location("Level 1 Area 2: Chest 1", self.player)
        boss = self.multiworld.get_location("Level 1 Boss: Fight", self.player)
        flower = ["Pretty Little Flower"]
        for location in (head_chase, demonic_chest):
            self.assertFalse(location.can_reach(self.state_with([])), f"{location.name} needs the flower")
            self.assertTrue(location.can_reach(self.state_with(flower)), f"{location.name} needs no dash skill")
            self.assertTrue(location.item_rule(self.world.create_item(DASH_SKILLS[0])), location.name)
        self.assertFalse(area_2_chest.can_reach(self.state_with(flower)))
        self.assertTrue(area_2_chest.can_reach(self.state_with(flower + [DASH_SKILLS[0]])))
        self.assertFalse(boss.can_reach(self.state_with(flower)))
        self.assertTrue(boss.can_reach(self.state_with(flower + [DASH_SKILLS[0]])))

    def test_any_dash_skill_works(self):
        loc = scene_location("Level_1_Area_3_Main")
        for skill in DASH_SKILLS:
            self.assertTrue(self.can_reach(loc, self.state_with(["Pretty Little Flower", skill])), skill)

    def test_area_1_chest_and_journal_need_the_flower(self):
        empty, flower = self.state_with([]), self.state_with(["Pretty Little Flower"])
        for name in ("Level 1 Area 1: Chest 1", "Journal: Level 1 Rural Horror"):
            self.assertFalse(self.can_reach(name, empty), name)
            self.assertTrue(self.can_reach(name, flower), name)

    def test_the_rest_of_area_1_needs_nothing(self):
        for name in ("Level 1 Area 1: Pretty Little Flower", "Level 1 Area 1: Senbei 1", "Level 1 Area 1: Fight"):
            self.assertTrue(self.can_reach(name, self.state_with([])), name)

    def test_nurikabe_boot_gate(self):
        loc = scene_location("Level_2_Area_5_Main")
        base = ["Pretty Little Flower", DASH_SKILLS[0], "Elevator Lever"]
        # chapters 1 and 2 reachable, but Sannomaru Palace sits behind the Nurikabe
        self.assertFalse(self.can_reach(loc, self.state_with(base)))
        self.assertTrue(self.can_reach(loc, self.state_with(base + ["Nurikabe's Shoe"])))

    def test_shop_tier_follows_cleared_count(self):
        # Needle Rain is a tier-5 skill: it needs five cleared chapters
        location = self.multiworld.get_location(NAME_OF_KEY["shop/Skill_Needle Rain"], self.player)
        state = self.state_with([f"Chapter {n} Cleared" for n in range(1, 5)] + ["5000 Konpeito"] * 2 + ["2 Senbei"],
                                sweep=False)
        self.assertFalse(location.access_rule(state))
        state.collect(self._make("Chapter 5 Cleared"), True)
        self.assertTrue(location.access_rule(state))

    def test_every_shop_slot_needs_a_cleared_chapter(self):
        # the tutorial alone pays too little Konpeito to shop, so even tier-0 slots wait for one cleared chapter
        location = self.multiworld.get_location(NAME_OF_KEY["shop/ComboB"], self.player)
        self.assertFalse(location.access_rule(self.state_with([], sweep=False)))
        self.assertFalse(location.access_rule(self.state_with(["3000 Konpeito"] * 2, sweep=False)))
        self.assertTrue(location.access_rule(self.state_with(["Chapter 1 Cleared"] + ["3000 Konpeito"] * 2,
                                                             sweep=False)))

    def test_gate_items_are_progression(self):
        for name in GATE_ITEMS:
            self.assertEqual(self.world.create_item(name).classification, ItemClassification.progression)


class TestShopMoney(HomuraHimeLogicBase):
    """A shop slot is only reachable when the Konpeito the randomizer paid out (times 1.5) covers its price."""

    def setUp(self):
        super().setUp()
        # the arithmetic below without the scaling of the counted pieces
        self.world.money_scale = self.world.senbei_scale = 1.0

    def slot(self, key):
        return self.multiworld.get_location(NAME_OF_KEY[key], self.player)

    def reachable(self, key, items):
        return self.slot(key).access_rule(self.state_with(["Chapter 1 Cleared"] + items, sweep=False))

    def test_price_is_covered_by_one_and_a_half_times_the_konpeito_items(self):
        # ComboB costs 8,000: 5,334 Konpeito items are enough, 5,000 are not
        self.assertFalse(self.reachable("shop/ComboB", ["3000 Konpeito", "2000 Konpeito"]))
        self.assertTrue(self.reachable("shop/ComboB", ["3000 Konpeito", "3000 Konpeito"]))

    def test_senbei_cost_needs_senbei_items(self):
        konpeito = ["3000 Konpeito"] * 8  # 24,000 x 1.5 = 36,000, enough for any price
        # Blaze Rush costs 8,000 Konpeito and 1 Senbei, Suzaku 36,000 and 3
        self.assertFalse(self.reachable("shop/Skill_Blaze Rush", konpeito))
        self.assertTrue(self.reachable("shop/Skill_Blaze Rush", konpeito + ["2 Senbei"]))
        self.assertFalse(self.reachable("shop/Skill_Suzaku", konpeito + ["2 Senbei"]))
        self.assertTrue(self.reachable("shop/Skill_Suzaku", konpeito + ["2 Senbei"] * 2))

    def test_slots_without_a_senbei_price_need_none(self):
        self.assertTrue(self.reachable("shop/ComboB", ["3000 Konpeito"] * 2))

    def test_senbei_are_counted_exactly(self):
        # no factor: the game has no other source of Senbei than the items
        self.assertEqual(self.world._senbei(self.state_with(["2 Senbei"] * 3, sweep=False)), 6)

    def test_shops_never_sell_this_worlds_konpeito_but_may_sell_senbei(self):
        for slot in self.world.shop_slots:
            for name in ("500 Konpeito", "3000 Konpeito"):
                self.assertFalse(slot.item_rule(self.world.create_item(name)), f"{name} in {slot.name}")
            for name in ("Full Heal", "50 Sakura Petals", "Combo Molten Impact", "Senbei", "2 Senbei"):
                self.assertTrue(slot.item_rule(self.world.create_item(name)), f"{name} in {slot.name}")

    def test_large_currency_counts_as_progression_that_is_not_balanced(self):
        for name in ("3000 Konpeito", "10000 Konpeito", "30 Sakura Petals", "50 Sakura Petals", "2 Senbei"):
            item = self.world.create_item(name)
            self.assertEqual(item.classification, ItemClassification.progression_skip_balancing, name)
            self.assertTrue(item.advancement, name)
            self.assertEqual(item.flags, 0, f"clients must see {name} as filler")

    def test_small_currency_is_plain_filler(self):
        # fewer advancement items for the fill to place; the counted pieces stand for these
        for name in ("500 Konpeito", "2000 Konpeito", "10 Sakura Petals", "20 Sakura Petals", "Senbei"):
            self.assertEqual(self.world.create_item(name).classification, ItemClassification.filler, name)

    def test_small_pieces_are_not_counted(self):
        state = self.state_with(["2000 Konpeito"] * 5, sweep=False)
        self.assertEqual(self.world._money(state), 0)

    def test_counted_pieces_stand_for_the_whole_pool(self):
        from .. import KONPEITO_VALUES, COUNTED_KONPEITO
        pool = [item.name for item in self.multiworld.itempool if item.player == self.player]
        pool += [loc.item.name for loc in self.multiworld.get_locations(self.player)
                 if loc.item is not None and loc.item.player == self.player and loc.address is not None]
        scale = self.world._scale_currency(pool, KONPEITO_VALUES, COUNTED_KONPEITO)
        self.assertGreater(scale, 1.0)
        # holding every counted piece means holding all the Konpeito of the pool
        counted = sum(COUNTED_KONPEITO.get(name, 0) for name in pool)
        total = sum(KONPEITO_VALUES.get(name, 0) for name in pool)
        self.assertAlmostEqual(counted * scale, total)

    def test_no_dash_skill_of_this_world_in_a_shop(self):
        # Level 1 needs a dash skill; a shop price in front of it would make the start a grind
        for slot in self.world.shop_slots:
            for skill in DASH_SKILLS:
                self.assertFalse(slot.item_rule(self.world.create_item(skill)), f"{skill} in {slot.name}")


class TestSenbeiUnit(HomuraHimeLogicBase):
    """The logic counts the "2 Senbei" pieces, or the single Senbei when the pool has no pairs."""

    def total(self, names):
        """All items of the player (pool and locked) and the Senbei they add up to. 
ames is unused."""
        pool = [item.name for item in self.multiworld.itempool if item.player == self.player]
        pool += [loc.item.name for loc in self.multiworld.get_locations(self.player)
                 if loc.item is not None and loc.item.player == self.player and loc.address is not None]
        return pool, sum(SENBEI_VALUES.get(name, 0) for name in pool)

    def test_pairs_count_and_stand_for_all_senbei(self):
        pool, senbei = self.total(None)
        self.assertEqual(self.world.senbei_unit, "2 Senbei")
        self.assertEqual(senbei, 55)
        self.assertAlmostEqual(self.world.senbei_scale * 2 * pool.count("2 Senbei"), 55)


class TestSenbeiWithoutPairs(TestSenbeiUnit):
    options = {"senbei_count": 30, "dash_skill_start": False}

    def test_pairs_count_and_stand_for_all_senbei(self):
        pool, senbei = self.total(None)
        self.assertEqual(self.world.senbei_unit, "Senbei")
        self.assertEqual(senbei, 30)
        self.assertEqual(self.world.create_item("Senbei").classification, ItemClassification.progression_skip_balancing)
        self.assertAlmostEqual(self.world.senbei_scale * pool.count("Senbei"), 30)


class TestHarumi(HomuraHimeLogicBase):
    """Harumi counts 280 Sakura per cleared chapter plus the Sakura items held."""

    def setUp(self):
        super().setUp()
        self.world.petal_scale = 1.0  # the arithmetic below without the scaling of the counted pieces

    def harumi(self, n):
        return self.multiworld.get_location(NAME_OF_KEY[f"harumi/{n}"], self.player)

    def test_cleared_chapters_count_280_each(self):
        # Harumi 3 needs 600: two cleared chapters give 560, three give 840
        self.assertFalse(self.harumi(3).access_rule(self.state_with(["Chapter 1 Cleared", "Chapter 2 Cleared"],
                                                                    sweep=False)))
        self.assertTrue(self.harumi(3).access_rule(self.state_with([f"Chapter {n} Cleared" for n in (1, 2, 3)],
                                                                   sweep=False)))

    def test_sakura_items_add_to_the_chapters(self):
        state = self.state_with(["Chapter 1 Cleared", "Chapter 2 Cleared", "30 Sakura Petals"], sweep=False)
        self.assertFalse(self.harumi(3).access_rule(state))  # 560 + 30
        state.collect(self.world.create_item("40 Sakura Petals"), True)
        self.assertTrue(self.harumi(3).access_rule(state))  # 560 + 70

    def test_last_reward_needs_sakura_items(self):
        # 2,300: all eight chapters give 2,240, the other 60 must come from items
        chapters = [f"Chapter {n} Cleared" for n in range(1, 9)]
        self.assertFalse(self.harumi(7).access_rule(self.state_with(chapters + ["50 Sakura Petals"], sweep=False)))
        self.assertTrue(self.harumi(7).access_rule(self.state_with(chapters + ["50 Sakura Petals", "30 Sakura Petals"],
                                                                   sweep=False)))

    def test_removing_sakura_takes_it_out_of_the_count(self):
        state = self.state_with(["50 Sakura Petals"] * 2, sweep=False)
        self.assertEqual(self.world._petals(state), 100)
        state.remove(state.multiworld.worlds[self.player].create_item("50 Sakura Petals"))
        self.assertEqual(self.world._petals(state), 50)


class TestSakuraInThePool(HomuraHimeLogicBase):
    """Without Sakura filler the pool still holds what Harumi's last reward needs beyond the chapters."""
    options = {"filler_sakura_ratio": 0, "dash_skill_start": False}

    def test_minimum_sakura(self):
        from .. import MIN_SAKURA_IN_POOL, SAKURA_VALUES
        total = sum(SAKURA_VALUES.get(item.name, 0) for item in self.multiworld.itempool if item.player == self.player)
        self.assertEqual(MIN_SAKURA_IN_POOL, 60)
        self.assertGreaterEqual(total, MIN_SAKURA_IN_POOL)


class TestAdvancedMovement(HomuraHimeLogicBase):
    """With advanced_movement_required the logic asks for no dash skill: the gap is crossed by parrying the laser."""
    options = {"advanced_movement_required": True, "dash_skill_start": False}

    def test_level1_needs_only_the_flower(self):
        loc = scene_location("Level_1_Area_3_Main")
        self.assertFalse(self.can_reach(loc, self.state_with([])))
        self.assertTrue(self.can_reach(loc, self.state_with(["Pretty Little Flower"])))

    def test_no_dash_skill_is_placed_early(self):
        self.assertFalse(set(self.multiworld.early_items[self.player]) & set(DASH_SKILLS))

    def test_dash_skills_are_only_useful_and_may_be_sold(self):
        for skill in DASH_SKILLS:
            self.assertEqual(self.world.create_item(skill).classification, ItemClassification.useful, skill)
        for slot in self.world.shop_slots:
            self.assertTrue(slot.item_rule(self.world.create_item(DASH_SKILLS[0])), slot.name)

    def test_the_option_is_in_the_slot_data(self):
        self.assertTrue(self.world.fill_slot_data()["advanced_movement_required"])


class TestDashSkillsAreProgressionByDefault(HomuraHimeLogicBase):
    """Without advanced movement the dash skills are progression and the slot data says so."""
    def test_default_asks_for_a_dash_skill(self):
        self.assertTrue(self.world.dash_required)
        self.assertFalse(self.world.fill_slot_data()["advanced_movement_required"])
        for skill in DASH_SKILLS:
            self.assertEqual(self.world.create_item(skill).classification, ItemClassification.progression, skill)


class TestPool(HomuraHimeLogicBase):
    """The item pool follows the options (Dango count, starting dash skill, cut content)."""
    options = {"dango_count": 7, "dash_skill_start": True, "include_cut_content": False}

    def test_pool_counts(self):
        names = [i.name for i in self.multiworld.itempool if i.player == self.player]
        self.assertEqual(names.count("Special Hanami Dango"), 7)
        self.assertNotIn("Ann's Board", names)
        self.assertEqual(len([i for i in self.multiworld.precollected_items[self.player] if i.name in DASH_SKILLS]), 1)

    def test_one_shop_dango_location(self):
        dango = [loc for loc in self.world.get_locations() if KEY_OF_NAME.get(loc.name) == "shop/Kushinada_Dango"]
        self.assertEqual(len(dango), 1)


class TestDesireCircleNeedsFirstClear(HomuraHimeLogicBase):
    """The merchant only hands over Desire Circle after the first chapter is cleared (HUB_CLEARS_REQUIRED)."""
    def test_desire_circle_waits_for_first_world(self):
        location = self.multiworld.get_location(NAME_OF_KEY["handover/desire_circle"], self.player)
        self.assertFalse(location.access_rule(self.state_with([], sweep=False)))
        self.assertFalse(location.access_rule(self.state_with(["3000 Konpeito"] * 2, sweep=False)))
        self.assertTrue(location.access_rule(self.state_with(["Chapter 1 Cleared"] + ["3000 Konpeito"] * 2,
                                                             sweep=False)))
