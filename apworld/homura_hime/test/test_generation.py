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


class TestUnlockItemsVanilla(HomuraHimeTestBase):
    options = {"level_unlock_items": True}


class TestUnlockItemsRandom(HomuraHimeTestBase):
    options = {"level_unlock_items": True, "level_order": "randomized"}


class TestEverything(HomuraHimeTestBase):
    options = {
        "level_unlock_items": True, "level_order": "randomized", "include_cut_content": True,
        "encounter_rewards": True, "dango_count": 99, "dash_skill_start": False,
    }


class TestNoDashStart(HomuraHimeTestBase):
    options = {"dash_skill_start": False}


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
        fixed = [i for i in ITEMS if i["classification"] != "filler" and i["type"] != "unlock" and not i.get("option")]
        self.assertGreater(len(fixed), 50)
        for item in fixed:
            started = 1 if item["name"] == self.world.start_dash else 0
            self.assertEqual(owned[item["name"]] + started, item["count"], item["name"])


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
