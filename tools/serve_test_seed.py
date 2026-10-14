"""Generates the seed the in-game tests run against and serves it on localhost:38281.

    python tools/serve_test_seed.py <path to the Archipelago source>

The seed has two players (tools/gametest_seed/): "HimeTest", the Homura-Hime slot the game tests connect to, and
"Quester" (APQuest), a second world that exchanges items with it. tools/run_tests.ps1 starts this script for you.
Run it with the Archipelago virtual environment's Python, from any folder.
"""
import glob
import os
import shutil
import subprocess
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PLAYERS = os.path.join(REPO_ROOT, "tools", "gametest_seed")
PORT = "38281"

if len(sys.argv) != 2:
    sys.exit(__doc__)
archipelago = os.path.abspath(sys.argv[1])
os.chdir(archipelago)  # Archipelago's scripts expect to run from their own folder

# Start from an empty output folder so the newest zip is the seed generated now.
output = os.path.join(archipelago, "output")
shutil.rmtree(output, ignore_errors=True)
subprocess.run([sys.executable, "Generate.py", "--player_files_path", PLAYERS, "--outputpath", output], check=True)
newest = max(glob.glob(os.path.join(output, "AP_*.zip")), key=os.path.getmtime)

# Run the server in this process (it does not return until stopped).
sys.argv = ["MultiServer.py", newest.replace("\\", "/"), "--host", "0.0.0.0", "--port", PORT]
import runpy  # noqa: E402
import ModuleUpdate  # noqa: E402

ModuleUpdate.update_ran = True  # do not try to install requirements
runpy.run_path("MultiServer.py", run_name="__main__")
