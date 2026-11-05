"""Packs apworld/homura_hime into dist/homura_hime.apworld, the file players put into Archipelago's custom_worlds
folder.

    python tools/build_apworld.py --ap-root <Archipelago source folder>

This runs the "Build APWorlds" component of the Archipelago launcher, the way Archipelago asks worlds to be packaged: it
writes the container version keys of the manifest itself and leaves out what apworld/homura_hime/.apignore and
Archipelago's global ignore list name (the tests, the Python caches). It needs an Archipelago source folder in which
worlds/homura_hime is this repository's apworld/homura_hime (a link or junction, see the README), and the Python of that
folder's environment. The folder can also be given as the ARCHIPELAGO_DIR environment variable.

The data contract (apworld/homura_hime/data) must be current, so run tools/build_data.py first after changing data.
"""
import argparse
import logging
import os
import shutil
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE = os.path.join(ROOT, "apworld", "homura_hime")
TARGET = os.path.join(ROOT, "dist", "homura_hime.apworld")
GAME = "Homura-Hime"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--ap-root", default=os.environ.get("ARCHIPELAGO_DIR"),
                        help="the Archipelago source folder (default: the ARCHIPELAGO_DIR environment variable)")
    args = parser.parse_args()
    if not args.ap_root or not os.path.isfile(os.path.join(args.ap_root, "Launcher.py")):
        sys.exit("Give the Archipelago source folder (the one with Launcher.py) as --ap-root or ARCHIPELAGO_DIR.")

    # the component works from the current folder and finds the worlds through the import path
    os.chdir(args.ap_root)
    sys.path.insert(0, args.ap_root)
    logging.getLogger().setLevel(logging.WARNING)

    from worlds import AutoWorldRegister
    from worlds.LauncherComponents import components

    world = AutoWorldRegister.world_types.get(GAME)
    if world is None:
        sys.exit(f"Archipelago did not load {GAME}: link worlds/homura_hime to {PACKAGE} and try again.")
    if os.path.realpath(os.path.dirname(world.__file__)) != os.path.realpath(PACKAGE):
        sys.exit(
            f"worlds/homura_hime in {args.ap_root} is not {PACKAGE}: the package would not be this repository's world."
        )

    component = next(c for c in components if c.display_name == "Build APWorlds")
    component.func(GAME, "--skip_open_folder")

    built = os.path.join(args.ap_root, "build", "apworlds", "homura_hime.apworld")
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    shutil.copyfile(built, TARGET)
    print("written", TARGET)


if __name__ == "__main__":
    main()
