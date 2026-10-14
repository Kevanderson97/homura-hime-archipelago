"""Packs apworld/homura_hime into dist/homura_hime.apworld, the file players put into Archipelago's custom_worlds folder.

    python tools/build_apworld.py

An .apworld is a zip archive whose top-level folder is the world package. The tests and Python caches are left out;
the data contract (apworld/homura_hime/data) must be current, so run tools/build_data.py first after changing data.
"""
import json
import os
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE = os.path.join(ROOT, "apworld", "homura_hime")
TARGET = os.path.join(ROOT, "dist", "homura_hime.apworld")
EXCLUDED_DIRS = {"__pycache__", "test"}
# What Archipelago itself adds to the manifest of an .apworld (APWorldContainer.get_manifest, container version 7). Without
# these keys the loader rejects the manifest and the world is loaded with the version 0.0.0.
CONTAINER_VERSION = 7


def package_files():
    """Yields (path on disk, path inside the archive) for every file that belongs in the .apworld."""
    for folder, dirs, files in os.walk(PACKAGE):
        dirs[:] = sorted(d for d in dirs if d not in EXCLUDED_DIRS)
        for name in sorted(files):
            path = os.path.join(folder, name)
            yield path, os.path.join("homura_hime", os.path.relpath(path, PACKAGE)).replace(os.sep, "/")


def manifest(path):
    """The manifest of the package with the container keys of an .apworld."""
    with open(path, encoding="utf-8") as file:
        content = json.load(file)
    return {**content, "version": CONTAINER_VERSION, "compatible_version": CONTAINER_VERSION}


def main():
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with zipfile.ZipFile(TARGET, "w", zipfile.ZIP_DEFLATED) as archive:
        for path, name in package_files():
            if name == "homura_hime/archipelago.json":
                archive.writestr(name, json.dumps(manifest(path), indent=4) + "\n")
            else:
                archive.write(path, name)
    print("written", TARGET)


if __name__ == "__main__":
    main()
