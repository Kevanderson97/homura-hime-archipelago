"""
The local_items / non_local_items lists, through Archipelago's real Generate and Main (the world tests' own generate
helper does not apply them). Forcing items home can never fail; forcing items out of a world can be impossible when the
other worlds have no free spots left (their own local items fill them), and then the generator refuses, as it should.
"""
import json
import os
import random
import shutil
import sys
import tempfile
import unittest

from .. import HomuraHimeWorld

GROUPS = ["Key Items", "Dash Skills", "Chapter Unlocks", "Guns", "Omamori", "Omamori Upgrades", "Combos", "Skills",
          "Abilities", "Shards And Crystals", "Currency", "Utility"]


def generate(seed: int, players: list) -> None:
    """Runs Generate.py's and Main.py's code for these players (a list of option dicts of Homura-Hime players)."""
    import Generate
    from Main import main as generate_multiworld

    folder, out = tempfile.mkdtemp(prefix="hh_players_"), tempfile.mkdtemp(prefix="hh_out_")
    saved_argv = sys.argv
    try:
        for index, options in enumerate(players):
            with open(os.path.join(folder, f"P{index + 1}.yaml"), "w", encoding="utf-8") as file:
                json.dump({"name": f"P{index + 1}", "game": HomuraHimeWorld.game,
                           HomuraHimeWorld.game: {"progression_balancing": 0, **options}}, file)  # json is yaml
        sys.argv = ["Generate.py", "--player_files_path", folder, "--outputpath", out, "--seed", str(seed),
                    "--skip_output", "--spoiler", "0"]
        arguments, seed = Generate.main()
        generate_multiworld(arguments, seed)
    finally:
        sys.argv = saved_argv
        shutil.rmtree(folder, ignore_errors=True)
        shutil.rmtree(out, ignore_errors=True)


class TestLocalAndNonLocalItems(unittest.TestCase):
    def test_the_group_names_used_here_exist(self):
        self.assertEqual(set(GROUPS), set(GROUPS) & set(HomuraHimeWorld.item_name_groups))

    def test_forcing_items_home_always_generates(self):
        for seed in range(1, 7):
            rng = random.Random(seed)
            players = [
                {"local_items": rng.sample(GROUPS, rng.randint(1, len(GROUPS)))} for _ in range(rng.randint(2, 3))
            ]
            with self.subTest(seed=seed, players=players):
                generate(seed, players)

    def test_a_request_that_fits_generates(self):
        generate(1, [{"local_items": ["Currency", "Utility"]}, {"non_local_items": ["Key Items"]}])

    def test_forcing_everything_out_of_a_world_whose_partner_is_full_is_refused(self):
        from Fill import FillError
        with self.assertRaises(FillError):
            generate(1, [{"local_items": GROUPS}, {"non_local_items": GROUPS}])
