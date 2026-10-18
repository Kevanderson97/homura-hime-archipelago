"""The shape of the location table: what is left out of the randomizer, and one location per thing a reward hands out."""
import json
import unittest
from pathlib import Path

DATA = Path(__file__).resolve().parents[1] / "data"
LOCATIONS = json.loads((DATA / "locations.json").read_text(encoding="utf-8"))

# Notes that only exist in the post-game hub: they are not part of the randomizer (nor of its logic)
POST_GAME_NOTES = ("Codex_Note_FinalLevel_ObservationRecords1", "Codex_Note_FinalLevel_ObservationRecords3",
                   "Codex_Note_FinalLevel_CLIO")
# The reward screens that hand out several things
MULTI_REWARD_CATEGORIES = ("trial", "tournament", "stage_clear")


class TestLocationTable(unittest.TestCase):
    def test_post_game_notes_are_not_locations(self):
        for loc in LOCATIONS:
            for note in POST_GAME_NOTES:
                self.assertNotIn(note, loc["vanilla"] or "", loc["key"])
                self.assertNotEqual(loc["key"], "journal/" + note)
            self.assertFalse(loc.get("scene", "").startswith("HubWorld") and loc["category"] == "journal", loc["key"])

    def test_every_reward_screen_has_a_location_for_each_thing_it_pays(self):
        bundles = {}
        for loc in LOCATIONS:
            if loc["category"] in MULTI_REWARD_CATEGORIES:
                bundles.setdefault(loc["key"].partition("#")[0], []).append(loc)
        self.assertEqual(len(bundles), 10 + 1 + 8)  # ten trials, the tournament, the eight chapter boards
        for base, parts in bundles.items():
            for part in parts:
                self.assertNotIn(";", part["vanilla"], f"{part['key']} holds more than one thing")
            keys = sorted(part["key"] for part in parts)
            self.assertEqual(keys, sorted([base] + [f"{base}#{n}" for n in range(2, len(parts) + 1)]))

    def test_the_trials_pay_two_things_each(self):
        for n in range(1, 11):
            base = f"trial/FC{n:02d}"
            parts = [loc for loc in LOCATIONS if loc["key"].partition("#")[0] == base]
            self.assertEqual(len(parts), 2, base)  # the currency and the note
            self.assertTrue(parts[1]["vanilla"].startswith("Codex_Note_"), base)

    def test_the_tournament_pays_three_things_and_the_first_chapter_board_two(self):
        self.assertEqual(len([l for l in LOCATIONS if l["key"].partition("#")[0] == "tournament/reward"]), 3)
        self.assertEqual(len([l for l in LOCATIONS if l["key"].partition("#")[0] == "stage/Level1"]), 2)

    def test_names_and_ids_stay_unique(self):
        self.assertEqual(len({l["name"] for l in LOCATIONS}), len(LOCATIONS))
        self.assertEqual(len({l["id"] for l in LOCATIONS}), len(LOCATIONS))
