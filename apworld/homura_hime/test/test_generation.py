from collections import Counter

from BaseClasses import LocationProgressType
from test.bases import WorldTestBase

from ..game_data import ITEM_BY_NAME, ITEMS, KEY_OF_NAME, KONPEITO_VALUES, LOCATIONS


class HomuraHimeTestBase(WorldTestBase):
    """
    Base of the generation tests. Every subclass generates one world with its options; the checks of WorldTestBase
    (all locations reachable, the game beatable, no item left over, ...) run for each of them, so a subclass without
    methods still tests that its option set generates.
    """
    game = "Homura-Hime"

    def owned_items(self):
        """Every item of this player: those still in the pool and those placed (locked) at its locations."""
        items = [i for i in self.multiworld.itempool if i.player == self.player]
        items += [loc.item for loc in self.multiworld.get_locations(self.player)
                  if loc.item is not None and loc.item.player == self.player]
        return items

    def location_keys(self):
        """The stable keys of this player's locations (see data/locations.json)."""
        return {KEY_OF_NAME.get(loc.name, loc.name) for loc in self.multiworld.get_locations(self.player)}


class TestDefaults(HomuraHimeTestBase):
    """Default options: generates, every location is reachable, the game is beatable."""


class TestRandomOrder(HomuraHimeTestBase):
    options = {"level_order": "randomized"}


class TestFreeformKeysVanilla(HomuraHimeTestBase):
    options = {"level_unlock_items": "freeform_progression"}


class TestFreeformKeysRandom(HomuraHimeTestBase):
    options = {"level_unlock_items": "freeform_progression", "level_order": "randomized"}


class TestLinearKeysVanilla(HomuraHimeTestBase):
    options = {"level_unlock_items": "linear_progression"}


class TestLinearKeysRandom(HomuraHimeTestBase):
    options = {"level_unlock_items": "linear_progression", "level_order": "randomized"}


class TestEverything(HomuraHimeTestBase):
    options = {
        "level_unlock_items": "linear_progression", "level_order": "randomized", "include_cut_content": True,
        "encounter_rewards": True, "dango_count": 99,
    }


class TestItemGroups(HomuraHimeTestBase):
    """Every kind of item belongs to a group a player can name in local_items / non_local_items."""
    options = {"local_items": {"Combos", "Skills"}}

    def test_every_kind_of_item_has_a_group(self):
        groups = self.world.item_name_groups
        covered = set().union(*groups.values())
        for item in ITEMS:
            if item["type"] in ("consumable",):  # the one Special Hanami Dango item is named directly
                continue
            self.assertIn(item["name"], covered, item["name"])
        for name in ("Combos", "Skills", "Abilities", "Omamori Upgrades", "Shards And Crystals", "Currency", "Utility"):
            self.assertTrue(groups[name], name)

    def test_the_group_sizes_match_the_catalogue(self):
        groups = self.world.item_name_groups
        self.assertEqual(len(groups["Combos"]), len([i for i in ITEMS if i["type"] == "combo"]))
        self.assertEqual(len(groups["Skills"]), len([i for i in ITEMS if i["type"] == "skill"]))


class KonpeitoPoolTestBase(HomuraHimeTestBase):
    """The Konpeito of the pool and what the player's other filler counts came to."""

    def konpeito(self):
        return sorted(KONPEITO_VALUES[item.name] for item in self.owned_items() if item.name in KONPEITO_VALUES)

    def count(self, name):
        return sum(1 for item in self.owned_items() if item.name == name)


class TestWithoutEncounterClearsTheKonpeitoTargetIsLower(KonpeitoPoolTestBase):
    """The fights still pay Konpeito in the game (at least 1000 each), so with 110 fewer places in the pool the target is
    110,000 lower, and the Full Heal / Ultimate Charge filler and the Dango keep their room (without traps: they take
    room too, see TestTrapsGiveWayWithoutEncounterClears)."""
    options = {"encounter_clears": False, "puffer_swarm_traps": 0, "hexed_doll_trio_traps": 0}

    def test_the_target_is_110000_lower(self):
        total = sum(self.konpeito())
        self.assertGreaterEqual(total, 440000)
        self.assertLess(total, 440000 + 30000)  # raised pieces overshoot the target by less than a piece

    def test_the_filler_is_kept_where_the_target_fits(self):
        self.assertEqual(self.count("Special Hanami Dango"), 15)
        self.assertGreaterEqual(self.count("Full Heal") + self.count("Ultimate Charge"), 12)

    def test_the_usual_pieces_are_enough(self):
        self.assertNotIn(20000, self.konpeito())
        self.assertNotIn(30000, self.konpeito())


class TestWithoutEncounterClearsAndTheHighestTarget(KonpeitoPoolTestBase):
    """Where lowering the target is not enough, the filler gives way step by step and the pieces may grow to 30000."""
    options = {"encounter_clears": False, "minimum_konpeito": 1000000, "full_heal_count": 50, "ultimate_charge_count": 50,
               "dango_count": 99}

    def test_the_target_is_reached(self):
        self.assertGreaterEqual(sum(self.konpeito()), 890000)

    def test_the_steps_were_taken(self):
        self.assertEqual(self.count("Full Heal") + self.count("Ultimate Charge"), 0)
        self.assertEqual(self.count("Special Hanami Dango"), 3)
        self.assertEqual(self.world.options.dango_count.value, 3)
        self.assertIn(30000, self.konpeito())


class TestWithEncounterClearsTheUsualPieces(KonpeitoPoolTestBase):
    """The larger pieces are only for the pool without encounter clears: every other seed is built as before."""
    options = {"minimum_konpeito": 1000000}

    def test_no_piece_above_the_usual_largest(self):
        self.assertNotIn(20000, self.konpeito())
        self.assertNotIn(30000, self.konpeito())
        self.assertGreaterEqual(sum(self.konpeito()), 1000000)
        self.assertEqual(self.count("Full Heal"), 10)


class TestLocalKonpeitoThatNoShopTakesIsPlacedDirectly(KonpeitoPoolTestBase):
    """The player's own small Konpeito pieces, which shop slots refuse, are put into this world's locations at once when they
    are local, not left to Archipelago's fill, which can strand them. Whatever fits a shop stays in the pool: it fills the
    shop slots. The counted (progression) pieces also still go through the fill."""
    # the generator expands item groups into item names before the world sees them; the test hands over the names
    # (minimum_konpeito 0: the filler pieces stay small instead of being raised to meet the target)
    options = {"local_items": {i["name"] for i in ITEMS if i["type"] in ("utility", "currency", "consumable")},
               "minimum_konpeito": 0}
    def placed(self):
        return [loc.item for loc in self.multiworld.get_locations(self.player)
                if loc.item is not None and loc.locked and loc.item.player == self.player and loc.address is not None]

    def test_small_konpeito_is_placed_and_not_pooled(self):
        placed = [item for item in self.placed() if item.name in KONPEITO_VALUES and not item.advancement]
        self.assertTrue(placed)
        pooled_small = [item for item in self.multiworld.itempool
                        if item.player == self.player and item.name in KONPEITO_VALUES and not item.advancement]
        self.assertFalse(pooled_small)

    def test_whatever_fits_a_shop_stays_in_the_pool(self):
        pooled = {item.name for item in self.multiworld.itempool if item.player == self.player}
        for name in ("Full Heal", "Ultimate Charge", "Special Hanami Dango"):
            self.assertIn(name, pooled)

    def test_counted_pieces_are_pooled(self):
        counted = [item for item in self.multiworld.itempool
                   if item.player == self.player and item.name in KONPEITO_VALUES and item.advancement]
        self.assertTrue(counted)


class TestNonLocalGroupThatIsNotShuffled(HomuraHimeTestBase):
    """Items of a group with shuffle_* off sit at their vanilla places in this world; asking for them to be non-local is
    dropped instead of being broken by the placement."""
    # the generator expands item groups into item names before the world sees them; the test hands over the names
    options = {"shuffle_combos": False, "shuffle_skills": True,
               "non_local_items": {i["name"] for i in ITEMS if i["type"] in ("combo", "skill", "gun")}}

    def test_only_the_unshuffled_group_is_dropped(self):
        remaining = self.world.options.non_local_items.value
        self.assertFalse(remaining & self.world.item_name_groups["Combos"])
        self.assertTrue(self.world.item_name_groups["Skills"] <= remaining)
        self.assertTrue(self.world.item_name_groups["Guns"] <= remaining)


class TestDangoGivesWayToFewLocations(HomuraHimeTestBase):
    """Without encounter clears (about 110 fewer locations) the most Dango do not fit; the count is lowered instead of the
    generation of every player's game stopping, and the Senbei the shops cost keep their room."""
    options = {"encounter_clears": False, "dango_count": 99, "senbei_count": 80}

    def test_the_dango_count_is_lowered_to_what_fits(self):
        self.assertLess(self.world.options.dango_count.value, 99)
        names = [item.name for item in self.owned_items()]
        self.assertEqual(names.count("Special Hanami Dango"), self.world.options.dango_count.value)

    def test_the_senbei_total_is_kept(self):
        values = {"Senbei": 1, "2 Senbei": 2}
        self.assertEqual(sum(values.get(item.name, 0) for item in self.owned_items()), 80)


class TestExcludedLocationsNeedFiller(HomuraHimeTestBase):
    """Only filler can fill an excluded location, so a low importance of the optional locations cannot exclude more of them
    than the pool has filler items (this combination used to stop the fill)."""
    options = {"encounter_clears": False, "encounter_rewards": True, "optional_location_importance": 0}

    def test_no_more_excluded_than_filler(self):
        excluded = [loc for loc in self.multiworld.get_locations(self.player)
                    if loc.address is not None and loc.progress_type == LocationProgressType.EXCLUDED]
        filler = [item for item in self.owned_items() if item.classification.name == "filler"]
        self.assertLessEqual(len(excluded), len(filler))


class TestAdvancedMovementRequired(HomuraHimeTestBase):
    options = {"advanced_movement_required": True}


class TestTutorialLocations(HomuraHimeTestBase):
    """The tutorial is always part of the seed (the Skip Tutorial button of the pause menu completes its checks)."""

    def test_tutorial_locations_present(self):
        names = self.location_keys()
        self.assertIn("tutorial/clear", names)
        self.assertIn("encounter/Level_0_Main/main", names)


class TestEncounterClears(HomuraHimeTestBase):
    options = {"encounter_clears": True}

    def test_encounter_locations_present(self):
        names = self.location_keys()
        self.assertEqual(len([n for n in names if n.startswith("encounter/")]), 110)


class TestOptionalLocationsAvoidImportantItems(HomuraHimeTestBase):
    options = {"optional_location_importance": 0}

    def test_optional_locations_are_excluded(self):
        optional = [loc for loc in self.multiworld.get_locations(self.player)
                    if KEY_OF_NAME.get(loc.name, "").startswith(("trial/", "ambush/", "head/"))]
        self.assertTrue(optional)
        for loc in optional:
            self.assertEqual(loc.progress_type, LocationProgressType.EXCLUDED, loc.name)

    def test_mandatory_locations_are_not_excluded(self):
        for loc in self.multiworld.get_locations(self.player):
            if KEY_OF_NAME.get(loc.name, "").startswith(("chest/", "stage/")):
                self.assertNotEqual(loc.progress_type, LocationProgressType.EXCLUDED, loc.name)


class TestFixedItemsExactlyOnce(HomuraHimeTestBase):
    """Every item the game has once (a combo, a skill, a key item, ...) is in the multiworld exactly that many times."""

    def test_counts_match_the_catalogue(self):
        owned = Counter(item.name for item in self.owned_items())
        fixed = [i for i in ITEMS if i["classification"] != "filler" and i["type"] not in ("unlock", "trap") and not i.get("option")]
        self.assertGreater(len(fixed), 50)
        for item in fixed:
            self.assertEqual(owned[item["name"]], item["count"], item["name"])


class TestKonpeitoPayoutsKept(HomuraHimeTestBase):
    """The plugin takes the vanilla Konpeito payouts away, so the pool holds every one of them as an item."""

    def test_pool_holds_the_fixed_payouts(self):
        owned = Counter(item.name for item in self.owned_items())
        expected = Counter(f"{loc['konpeito']} Konpeito" for loc in self.world.active_locations if "konpeito" in loc)
        self.assertEqual(expected["3000 Konpeito"], 28)
        for name, count in expected.items():
            self.assertGreaterEqual(owned[name], count, name)


class TestMinimumKonpeito(HomuraHimeTestBase):
    """The pool holds at least the requested Konpeito in total (default 550,000)."""
    minimum = 550000

    def total(self):
        return sum(KONPEITO_VALUES.get(item.name, 0) for item in self.owned_items())

    def test_minimum_is_reached(self):
        self.assertGreaterEqual(self.total(), self.minimum)


class TestHigherMinimumKonpeito(TestMinimumKonpeito):
    options = {"minimum_konpeito": 900000}
    minimum = 900000


class TestNoMinimumKonpeito(TestMinimumKonpeito):
    options = {"minimum_konpeito": 0}

    def test_minimum_is_reached(self):
        # without the minimum only the vanilla payouts and the small filler remain
        self.assertLess(self.total(), 550000)


class TestUtilityAndDangoCounts(HomuraHimeTestBase):
    """Full Heal, Ultimate Charge and Special Hanami Dango counts follow their options (defaults 10, 10 and 15)."""

    def test_default_counts(self):
        owned = Counter(item.name for item in self.owned_items())
        self.assertEqual(owned["Full Heal"], 10)
        self.assertEqual(owned["Ultimate Charge"], 10)
        self.assertEqual(owned["Special Hanami Dango"], 15)


class TestSenbeiTotal(HomuraHimeTestBase):
    """The default 55 Senbei come as 25 single Senbei and 15 "2 Senbei" items (40 items)."""

    def test_default_senbei(self):
        owned = Counter(item.name for item in self.owned_items())
        self.assertEqual((owned["Senbei"], owned["2 Senbei"]), (25, 15))


class TestUnshuffledGroups(HomuraHimeTestBase):
    """A group that is not shuffled keeps its items at the locations that give them in the base game."""
    options = {"shuffle_combos": False, "shuffle_skills": False, "shuffle_omamori": False, "shuffle_guns": False,
               "shuffle_shards": False, "shuffle_upgrades": False, "shuffle_key_items": False}

    def _held(self):
        return {KEY_OF_NAME[loc.name]: loc.item.name for loc in self.multiworld.get_locations(self.player)
                if loc.item is not None and loc.item.player == self.player and loc.name in KEY_OF_NAME}

    def test_shop_combos_and_omamori_stay_in_their_slots(self):
        held = self._held()
        kept = {i["game"] for i in ITEM_BY_NAME.values() if i["type"] in ("combo", "omamori", "upgrade")}
        checked = 0
        for loc in LOCATIONS:
            vanilla = loc.get("vanilla") or ""
            if loc["category"] != "shop" or vanilla not in kept:
                continue
            item = next(i for i in ITEM_BY_NAME.values() if i["game"] == vanilla)
            self.assertEqual(held[loc["key"]], item["name"], loc["key"])
            checked += 1
        self.assertGreater(checked, 15)

    def test_key_items_stay_with_their_hand_overs(self):
        held = self._held()
        self.assertEqual(held["handover/flower"], "Pretty Little Flower")
        self.assertEqual(held["handover/launcher"], "Elevator Lever")


class TestSomeGroupsUnshuffled(HomuraHimeTestBase):
    options = {"shuffle_omamori": False, "shuffle_shards": False}


class TestOptionalLocationsHoldImportantItemsByDefault(HomuraHimeTestBase):
    """The level select is always available, so optional locations are not excluded unless the option asks for it."""

    def test_nothing_is_excluded(self):
        excluded = [loc.name for loc in self.multiworld.get_locations(self.player)
                    if loc.progress_type == LocationProgressType.EXCLUDED]
        self.assertEqual(excluded, [])
