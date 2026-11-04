"""puffer_swarm_traps: small filler items of the pool become Puffer Swarm Traps; nothing else changes."""
from BaseClasses import ItemClassification

from ..game_data import ITEMS
from .test_rules import HomuraHimeLogicBase
from .test_shop_budget import generate

TRAP = "Puffer Swarm Trap"
DOLLS = "Hexed Doll Trio Trap"


class TestNoTraps(HomuraHimeLogicBase):
    options = {"puffer_swarm_traps": 0}

    def test_zero_means_none(self):
        self.assertNotIn(TRAP, [i.name for i in self.multiworld.itempool])

    def test_a_yaml_can_say_disabled(self):
        from ..options import PufferSwarmTraps
        self.assertEqual(0, PufferSwarmTraps.from_text("disabled").value)


class TestDefaultTraps(HomuraHimeLogicBase):
    def test_twenty_by_default(self):
        self.assertEqual(20, [i.name for i in self.multiworld.itempool].count(TRAP))


class TestTraps(HomuraHimeLogicBase):
    options = {"puffer_swarm_traps": 12}

    def traps(self):
        return [i for i in self.multiworld.itempool if i.player == self.player and i.name == TRAP]

    def test_the_requested_number_is_in_the_pool_as_traps(self):
        traps = self.traps()
        self.assertEqual(12, len(traps))
        self.assertTrue(all(i.classification == ItemClassification.trap for i in traps))

    def test_the_trap_item_is_in_the_traps_group(self):
        self.assertIn(TRAP, self.world.item_name_groups["Traps"])

    def test_the_slot_data_tells_the_plugin(self):
        self.assertEqual(12, self.world.fill_slot_data()["puffer_swarm_traps"])


def test_many_traps_generate_with_every_filler_local():
    """The most traps with all filler kept local still generates (traps are sized like the Dango and take free locations, not the room of needed items); the count is lowered to fit."""
    local = {i["name"] for i in ITEMS if i["type"] in ("utility", "currency", "consumable", "trap")}
    for seed in range(5):
        generate(seed * 7 + 1, {"puffer_swarm_traps": 99, "local_items": local}, 1, check=False)


class TestTrapsGiveWayWithoutEncounterClears(HomuraHimeLogicBase):
    """With 110 fewer locations the traps are the optional group that gives way when the Konpeito target needs room."""
    options = {"encounter_clears": False, "minimum_konpeito": 1000000}

    def test_only_a_few_are_left(self):
        count = [i.name for i in self.multiworld.itempool if i.player == self.player].count(TRAP)
        self.assertLessEqual(count, 5)


class TestDollTraps(HomuraHimeLogicBase):
    options = {"puffer_swarm_traps": 0, "hexed_doll_trio_traps": 7}

    def test_the_requested_number_is_in_the_pool_as_traps(self):
        traps = [i for i in self.multiworld.itempool if i.player == self.player and i.name == DOLLS]
        self.assertEqual(7, len(traps))
        self.assertTrue(all(i.classification == ItemClassification.trap for i in traps))
        self.assertNotIn(TRAP, [i.name for i in self.multiworld.itempool])

    def test_the_slot_data_tells_the_plugin(self):
        data = self.world.fill_slot_data()
        self.assertEqual((0, 7), (data["puffer_swarm_traps"], data["hexed_doll_trio_traps"]))

    def test_a_yaml_can_say_disabled(self):
        from ..options import HexedDollTrioTraps
        self.assertEqual(0, HexedDollTrioTraps.from_text("disabled").value)


class TestBothTrapsByDefault(HomuraHimeLogicBase):
    def test_twenty_puffer_and_five_doll_traps(self):
        names = [i.name for i in self.multiworld.itempool]
        self.assertEqual((20, 5), (names.count(TRAP), names.count(DOLLS)))
