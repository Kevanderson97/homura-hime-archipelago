"""
Regenerates apworld/sample/Homura-Hime.yaml: a yaml with every option of the world, written by Archipelago's own template
generator (so the options, their values and their descriptions are exactly what the world defines).

    python tools/make_sample_yaml.py <path to the Archipelago source>

The path can also be given in the ARCHIPELAGO_DIR environment variable. The world must be installed in that Archipelago
(worlds/homura_hime, or a junction/symlink to apworld/homura_hime). Run it again whenever an option changes:
apworld/homura_hime/test/test_sample_yaml.py fails while the sample is out of date.
"""
import json
import os
import re
import sys
import tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = os.path.join(ROOT, "apworld", "sample", "Homura-Hime.yaml")

ARCHIPELAGO = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("ARCHIPELAGO_DIR")
if not ARCHIPELAGO:
    sys.exit(__doc__)

# Archipelago's modules expect to run from its own folder.
os.chdir(ARCHIPELAGO)
sys.path.insert(0, ARCHIPELAGO)
import ModuleUpdate  # noqa: E402

ModuleUpdate.update_ran = True  # do not try to install requirements
import Options  # noqa: E402

# Let Archipelago write the template of every installed world into a temporary folder and keep only ours.
with tempfile.TemporaryDirectory() as folder:
    Options.generate_yaml_templates(folder, generate_hidden=False)
    with open(os.path.join(folder, "Homura-Hime.yaml"), encoding="utf-8") as template:
        text = template.read()

text = text.replace("description: Default Homura-Hime Template",
                    "description: Sample Homura-Hime yaml with every option (tools/make_sample_yaml.py writes it)")
# the generator writes the version of the Archipelago it runs in; the file asks for the oldest one the world supports
with open(os.path.join(ROOT, "apworld", "homura_hime", "archipelago.json"), encoding="utf-8") as manifest_file:
    minimum = json.load(manifest_file)["minimum_ap_version"]
text = re.sub(r"(  version: )\d+\.\d+\.\d+( # Version of Archipelago required)", rf"\g<1>{minimum}\g<2>", text, count=1)
os.makedirs(os.path.dirname(TARGET), exist_ok=True)
with open(TARGET, "w", encoding="utf-8", newline="\n") as target:
    target.write(text)
print("written", TARGET)
