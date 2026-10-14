"""The sample yaml (apworld/sample/Homura-Hime.yaml) stays in step with the world's options and version."""
import json
import unittest
from pathlib import Path

import yaml

from ..options import HomuraHimeOptions

# the world package (apworld/homura_hime) and the sample file next to it
PACKAGE = Path(__file__).resolve().parents[1]
SAMPLE = PACKAGE.parent / "sample" / "Homura-Hime.yaml"


@unittest.skipUnless(SAMPLE.exists(), "the sample yaml is not next to the package")
class TestSampleYaml(unittest.TestCase):
    """apworld/sample/Homura-Hime.yaml lists every option of the world (tools/make_sample_yaml.py writes it)."""

    def setUp(self):
        self.text = SAMPLE.read_text(encoding="utf-8")
        self.data = yaml.safe_load(self.text)

    def test_every_option_is_in_the_sample(self):
        options = set(self.data["Homura-Hime"])
        missing = set(HomuraHimeOptions.type_hints) - options
        self.assertFalse(missing, f"regenerate the sample (tools/make_sample_yaml.py): {sorted(missing)}")

    def test_the_sample_names_no_option_the_world_lacks(self):
        unknown = set(self.data["Homura-Hime"]) - set(HomuraHimeOptions.type_hints)
        self.assertFalse(unknown, f"regenerate the sample (tools/make_sample_yaml.py): {sorted(unknown)}")

    def test_it_asks_for_this_version_of_the_world(self):
        manifest = json.loads((PACKAGE / "archipelago.json").read_text(encoding="utf-8"))
        self.assertEqual(self.data["requires"]["game"]["Homura-Hime"], manifest["world_version"],
                         "regenerate the sample (tools/make_sample_yaml.py)")

    def test_it_asks_for_the_oldest_archipelago_the_world_supports(self):
        manifest = json.loads((PACKAGE / "archipelago.json").read_text(encoding="utf-8"))
        self.assertEqual(self.data["requires"]["version"], manifest["minimum_ap_version"],
                         "regenerate the sample (tools/make_sample_yaml.py)")

    def test_it_is_for_this_game(self):
        self.assertEqual(self.data["game"], "Homura-Hime")
