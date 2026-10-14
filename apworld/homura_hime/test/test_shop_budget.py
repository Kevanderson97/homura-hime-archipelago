"""
The Konpeito logic of the shops, on whole generated multiworlds (solo, and two Homura-Hime players so that one player's
progression can land in the other's shop) and on the arithmetic of the budget itself.
"""
import unittest
from unittest import mock

from BaseClasses import CollectionState, Item, ItemClassification
from Fill import balance_multiworld_progression, distribute_items_restrictive
from test.general import setup_multiworld
from worlds import AutoWorld

from .. import HomuraHimeWorld, shop_budget
from ..game_data import KONPEITO_VALUES


def generate(seed, options, players, check=True):
    """Generates a multiworld like Main.py does; with check=False the shop budget check is left out."""
    multiworld = setup_multiworld([HomuraHimeWorld] * players, seed=seed, options=options)
    distribute_items_restrictive(multiworld)
    # the same order as Main.py: the check runs after progression balancing, which can still move items
    AutoWorld.call_all(multiworld, "post_fill")
    balance_multiworld_progression(multiworld)
    if check:
        AutoWorld.call_all(multiworld, "finalize_multiworld")
    return multiworld


def homura_hime_worlds(multiworld):
    """The Homura-Hime worlds of a multiworld that only holds Homura-Hime players."""
    return [multiworld.worlds[player] for player in multiworld.player_ids]


class Slot:
    """A stand-in for a shop slot: only the price matters to the budget arithmetic."""

    def __init__(self, name, price, senbei=0):
        self.name, self.price, self.senbei = name, price, senbei


class TestBudgetArithmetic(unittest.TestCase):
    """The example: the shop holds A, B, C and D, player 2 needs A and B at sphere 3 and C and D only at sphere 7."""

    def setUp(self):
        self.a, self.b, self.c, self.d = Slot("A", 18000), Slot("B", 12000), Slot("C", 40000), Slot("D", 25000)
        self.required = {self.a: 3, self.b: 3, self.c: 7, self.d: 7}
        # 20,000 Konpeito are paid out before sphere 3 (30,000 money), 200,000 before sphere 7
        self.money = [0, 0, 0, 30000, 30000, 30000, 30000, 300000]

    def test_what_is_needed_together_must_fit_the_money_by_then(self):
        self.assertIsNone(shop_budget.first_violation(self.required, self.money))  # A + B = 30,000

    def test_one_more_Konpeito_of_price_breaks_it(self):
        self.a.price = 18001
        sphere, due = shop_budget.first_violation(self.required, self.money)
        self.assertEqual(sphere, 3)
        self.assertEqual({slot.name for slot in due}, {"A", "B"})

    def test_items_needed_later_do_not_count_before_they_are_needed(self):
        self.assertIsNone(shop_budget.first_violation({self.c: 7}, self.money))  # 40,000 against 300,000 by sphere 7
        self.assertIsNotNone(shop_budget.first_violation({self.c: 3}, self.money))  # the same item needed at sphere 3

    def test_an_item_needed_early_counts_in_every_later_sphere(self):
        money = [0, 0, 0, 30000, 30000, 30000, 30000, 50000]
        self.assertIsNotNone(shop_budget.first_violation(self.required, money))  # sphere 7 needs all four: 95,000


class TestSenbeiBudget(unittest.TestCase):
    """Senbei cannot be farmed: what the needed purchases cost in Senbei must have been received by then."""

    def setUp(self):
        self.a, self.b = Slot("A", 8000, senbei=2), Slot("B", 8000, senbei=3)
        self.required = {self.a: 3, self.b: 3}
        self.money = [0, 0, 0, 100000]
        self.senbei = [0, 0, 0, 5]

    def test_enough_senbei(self):
        self.assertIsNone(shop_budget.first_violation(self.required, self.money, self.senbei))  # 2 + 3 = 5

    def test_one_senbei_short(self):
        self.b.senbei = 4
        found = shop_budget.violations(self.required, self.money, self.senbei)
        self.assertEqual([(sphere, resource) for sphere, _, resource in found], [(3, "senbei")])

    def test_konpeito_is_reported_first_when_both_are_short(self):
        found = shop_budget.violations(self.required, [0, 0, 0, 1000], [0, 0, 0, 0])
        self.assertEqual(found[0][2], "konpeito")

    def test_senbei_are_ignored_when_not_given(self):
        self.assertIsNone(shop_budget.first_violation(self.required, self.money))


class TestWhatIsBought(unittest.TestCase):
    """The players buy the progression in shop slots, whatever game it belongs to, except Homura-Hime's Konpeito."""

    def item(self, name, classification, game):
        """A bare item of the given game, enough for shop_budget.needed."""
        return type("TestItem", (Item,), {"game": game})(name, classification, 1, 1)

    def test_progression_of_any_kind_is_bought(self):
        self.assertTrue(shop_budget.needed(self.item("Key", ItemClassification.progression, "Other")))
        # collectibles of other games are often progression_skip_balancing (power seals, grubs) and still needed
        self.assertTrue(shop_budget.needed(self.item("Seal", ItemClassification.progression_skip_balancing, "Other")))
        self.assertTrue(shop_budget.needed(self.item("50 Sakura Petals", ItemClassification.progression_skip_balancing,
                                                     "Homura-Hime")))

    def test_konpeito_and_filler_are_not_bought(self):
        self.assertFalse(shop_budget.needed(self.item("3000 Konpeito", ItemClassification.progression_skip_balancing,
                                                      "Homura-Hime")))
        self.assertFalse(shop_budget.needed(self.item("Junk", ItemClassification.filler, "Other")))


class TestGeneratedShops(unittest.TestCase):
    """Whole generated multiworlds: the budget holds, with the repair step doing its job where needed."""
    SEEDS = range(10)

    def assert_affordable(self, multiworld):
        """The generated multiworld respects the shop budget, is beatable, and no shop sells its own world's Konpeito."""
        run = shop_budget.Playthrough(multiworld, homura_hime_worlds(multiworld))
        self.assertIsNone(shop_budget.check(run),
                          "shop purchases exceed the Konpeito available by the time they are needed")
        self.assertTrue(multiworld.can_beat_game(CollectionState(multiworld)), "the seed is no longer beatable")
        for world in homura_hime_worlds(multiworld):
            for slot in world.shop_slots:
                item = slot.item
                if item is not None and item.player == world.player:
                    self.assertNotIn(item.name, KONPEITO_VALUES, f"{item.name} in {slot.name}")

    def test_solo_seeds(self):
        for seed in self.SEEDS:
            self.assert_affordable(generate(seed, {}, 1))

    def test_randomized_order_with_unlock_items(self):
        for seed in self.SEEDS:
            self.assert_affordable(generate(seed, {"level_order": "randomized", "level_unlock_items": True}, 1))

    def test_two_homura_hime_players(self):
        for seed in self.SEEDS:
            self.assert_affordable(generate(seed, {}, 2))

    def test_the_fill_repairs_a_shop_that_asks_too_much(self):
        # two players, seeds whose first placement needs more Konpeito than there is when the items are needed
        # (found by trying)
        swaps = 0
        original = shop_budget.swap

        def counting(a, b):
            nonlocal swaps
            swaps += 1
            original(a, b)

        with mock.patch.object(shop_budget, "swap", counting):
            for seed in (21, 23):
                self.assert_affordable(generate(seed, {}, 2))
        self.assertGreater(swaps, 0, "nothing needed repairing, the test shows nothing (pick other seeds)")


class TestShortcutsMatchFullReplays(unittest.TestCase):
    """The shortcuts that keep the check fast in large multiworlds give the same answers as doing it the long way."""

    @staticmethod
    def full_replay_delay(run, slot):
        """The first sphere not completed without the slot's item, replaying the whole seed from the start."""
        state = CollectionState(run.multiworld)
        remaining = list(run.multiworld.get_locations())
        reached = set()
        for sphere in range(len(run.spheres)):
            reachable = [location for location in remaining if location.can_reach(state)]
            reached.update(reachable)
            if any(location not in reached for location in run.spheres[sphere]):
                return sphere
            remaining = [location for location in remaining if location not in reached]
            for location in reachable:
                run.collect(state, location, slot.item)
        return None

    def test_replays_from_the_slot_sphere_match_full_replays(self):
        compared = 0
        for seed in range(4):
            multiworld = generate(seed, {}, 2, check=False)
            run = shop_budget.Playthrough(multiworld, homura_hime_worlds(multiworld))
            for slot in run.shop_slots:
                if slot in run.sphere_of and run.bought(slot):
                    self.assertEqual(run.first_delay(slot, len(run.spheres)), self.full_replay_delay(run, slot),
                                     f"seed {seed}, {slot.name}")
                    compared += 1
        self.assertGreater(compared, 10)

    def test_a_resumed_playthrough_matches_a_new_one(self):
        multiworld = generate(21, {}, 2, check=False)  # needs a repair (see test_the_fill_repairs_...)
        run = shop_budget.Playthrough(multiworld, homura_hime_worlds(multiworld))
        violation = shop_budget.check(run)
        self.assertIsNotNone(violation)
        run.resume(shop_budget.repair(multiworld, run, violation))
        fresh = shop_budget.Playthrough(multiworld, homura_hime_worlds(multiworld))
        self.assertEqual(run.sphere_of, fresh.sphere_of)
        self.assertEqual(run.money, fresh.money)
        self.assertEqual(sorted(run.states), sorted(fresh.states))
