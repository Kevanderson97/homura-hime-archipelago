"""
The package rules of Archipelago's "adding games" and APWorld specification documents that tests of the logic do not
cover: the folder layout, the docs, the manifest and what the build leaves out.
"""
import json
import os
import re
import unittest

from .. import HomuraHimeWorld

PACKAGE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class TestPackage(unittest.TestCase):
    def test_every_folder_with_python_files_has_an_init(self):
        for folder, dirs, files in os.walk(PACKAGE):
            dirs[:] = [d for d in dirs if d != "__pycache__"]
            if any(name.endswith(".py") for name in files):
                with self.subTest(folder=os.path.relpath(folder, PACKAGE)):
                    self.assertIn("__init__.py", files, "needed for the frozen build")

    def test_there_is_a_game_info_doc_and_a_setup_doc_the_web_world_lists(self):
        docs = os.listdir(os.path.join(PACKAGE, "docs"))
        self.assertIn(f"en_{HomuraHimeWorld.game}.md", docs)
        self.assertEqual(["setup_en.md"], [t.file_name for t in HomuraHimeWorld.web.tutorials])
        for tutorial in HomuraHimeWorld.web.tutorials:
            self.assertIn(tutorial.file_name, docs)

    def test_the_manifest_is_one_archipelago_accepts(self):
        with open(os.path.join(PACKAGE, "archipelago.json"), encoding="utf-8") as file:
            manifest = json.load(file)
        self.assertEqual(HomuraHimeWorld.game, manifest["game"])
        for key in ("world_version", "minimum_ap_version"):
            self.assertRegex(manifest[key], r"^\d+\.\d+\.\d+$", f"{key} must be major.minor.build")
        self.assertIsInstance(manifest["authors"], list)
        # the container versions are written by Archipelago's Build APWorlds component, never by hand
        self.assertNotIn("version", manifest)
        self.assertNotIn("compatible_version", manifest)

    def test_the_build_leaves_the_tests_out(self):
        with open(os.path.join(PACKAGE, ".apignore"), encoding="utf-8") as file:
            patterns = [line.strip() for line in file if line.strip() and not line.startswith("#")]
        self.assertIn("/test", patterns)

    def test_the_folder_name_is_lower_case_like_the_apworld_file(self):
        self.assertTrue(re.fullmatch(r"[a-z0-9_]+", os.path.basename(PACKAGE)))

    def test_python_and_markdown_stay_within_120_columns_without_trailing_whitespace(self):
        """Archipelago's style guide; the rest of it is checked by ruff (see ruff.toml and docs/TESTING.md)."""
        for folder, dirs, files in os.walk(PACKAGE):
            dirs[:] = [d for d in dirs if d != "__pycache__"]
            for name in files:
                if not name.endswith((".py", ".md")):
                    continue
                path = os.path.join(folder, name)
                with open(path, encoding="utf-8") as file:  # text mode: the line ends are already "\n"
                    for number, line in enumerate(file.read().split("\n"), 1):
                        with self.subTest(file=os.path.relpath(path, PACKAGE), line=number):
                            self.assertLessEqual(len(line), 120, "longer than 120 columns")
                            self.assertEqual(line, line.rstrip(), "trailing whitespace")
