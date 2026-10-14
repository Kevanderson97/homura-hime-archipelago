"""Step 1: count the script classes (MonoBehaviours) in every scene.

The other extraction scripts only open the scenes that contain the class they are after, so this histogram, written to
`histo.json` in the work folder, tells them where to look. Scenes already in `histo.json` are skipped, so an interrupted
run can simply be started again.

    python tools/extract/histo.py
"""
import collections
import json
import os
import struct
import time

import UnityPy

from libscene import DATA_DIR, SCENE_NAMES, work_path


def class_histogram(scene_file):
    """{script class name: number of MonoBehaviours} for one scene file."""
    env = UnityPy.load(os.path.join(DATA_DIR, "globalgamemanagers.assets"), os.path.join(DATA_DIR, scene_file))
    scripts = {}  # (asset file, path id) -> class name
    for obj in env.objects:
        if obj.type.name == "MonoScript":
            try:
                scripts[(os.path.basename(obj.assets_file.name), obj.path_id)] = obj.read(check_read=False).m_ClassName
            except Exception:
                pass
    scene = [f for k, f in env.files.items() if os.path.basename(k) == scene_file][0]
    histogram = collections.Counter()
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour" or os.path.basename(obj.assets_file.name) != scene_file:
            continue
        # the MonoScript PPtr sits at offset 16 of the serialized component (see libscene)
        file_id, path_id = struct.unpack_from("<iq", obj.get_raw_data(), 16)
        file_name = scene_file if file_id == 0 else os.path.basename(scene.externals[file_id - 1].path)
        histogram[scripts.get((file_name, path_id), "?")] += 1
    return dict(histogram)


def main():
    out_path = work_path("histo.json")
    result = {}
    if os.path.exists(out_path):
        with open(out_path, encoding="utf-8") as f:
            result = json.load(f)

    for index, name in enumerate(SCENE_NAMES):
        key = "%d:%s" % (index, name)
        scene_file = "level%d" % index
        if key in result or not os.path.exists(os.path.join(DATA_DIR, scene_file)):
            continue
        started = time.time()
        try:
            result[key] = class_histogram(scene_file)
            print("%s done %.0fs classes=%d" % (key, time.time() - started, len(result[key])), flush=True)
        except Exception as error:
            print("%s ERR %r" % (key, error), flush=True)
            result[key] = {"__error__": repr(error)}
        with open(out_path, "w", encoding="utf-8") as f:  # saved after every scene: the run takes a while
            json.dump(result, f, ensure_ascii=False)
    print("ALL DONE", flush=True)


if __name__ == "__main__":
    main()
