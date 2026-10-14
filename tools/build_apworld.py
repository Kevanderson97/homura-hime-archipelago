"""Packs apworld/homura_hime into dist/homura_hime.apworld, the file players put into Archipelago's custom_worlds folder.

    python tools/build_apworld.py

An .apworld is a zip archive whose top-level folder is the world package. The tests and Python caches are left out;
the data contract (apworld/homura_hime/data) must be current, so run tools/build_data.py first after changing data.
"""
import os
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE = os.path.join(ROOT, "apworld", "homura_hime")
TARGET = os.path.join(ROOT, "dist", "homura_hime.apworld")
EXCLUDED_DIRS = {"__pycache__", "test"}


def package_files():
    """Yields (path on disk, path inside the archive) for every file that belongs in the .apworld."""
    for folder, dirs, files in os.walk(PACKAGE):
        dirs[:] = sorted(d for d in dirs if d not in EXCLUDED_DIRS)
        for name in sorted(files):
            path = os.path.join(folder, name)
            yield path, os.path.join("homura_hime", os.path.relpath(path, PACKAGE)).replace(os.sep, "/")


def main():
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with zipfile.ZipFile(TARGET, "w", zipfile.ZIP_DEFLATED) as archive:
        for path, name in package_files():
            archive.write(path, name)
    print("written", TARGET)


if __name__ == "__main__":
    main()
