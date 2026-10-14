"""Step 0: name every ScriptableObject/MonoBehaviour of the game's shared asset files.

Scenes refer to item definitions and similar assets by (file, path id) only. This script reads the name of every such
asset once and writes `names.json` (`{asset file: {path id: name}}`) to the work folder, which `Scene.ptr_name` uses.

    python tools/extract/build_names.py
"""
import glob
import json
import os

import UnityPy

from libscene import DATA_DIR, read_object_name, work_path


def main():
    files = glob.glob(os.path.join(DATA_DIR, "sharedassets*.assets"))
    files += [os.path.join(DATA_DIR, "resources.assets"), os.path.join(DATA_DIR, "globalgamemanagers.assets")]
    names = {}
    for path in sorted(files):
        file_name = os.path.basename(path)
        try:
            env = UnityPy.load(path)
        except Exception as error:
            print("skip", file_name, error)
            continue
        file_names = {}
        for obj in env.objects:
            if obj.type.name != "MonoBehaviour":
                continue
            try:
                name = read_object_name(obj.get_raw_data())
            except Exception:
                continue  # not a named object
            if name is not None:
                file_names[str(obj.path_id)] = name
        names[file_name] = file_names
        print(file_name, len(file_names), flush=True)
    with open(work_path("names.json"), "w", encoding="utf-8") as f:
        json.dump(names, f, ensure_ascii=False)


if __name__ == "__main__":
    main()
