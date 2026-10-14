# Extraction scripts

These scripts read the **installed game** (nothing from it is stored in this repository) and write the scene tables that
`tools/build_data.py` turns into the data contract. You only need them after a game update changes the maps; the results of
the last run are checked in as `data/locations_chests.json` and `data/battlerounds.json`.
(`locations_senbei.json` and `locations_journals.json` were produced the same way with throw-away scripts and are kept as
plain source data.)

```
pip install UnityPy
set HOMURA_HIME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Homura Hime     (optional, this is the default)

python tools/extract/build_names.py     # 0. names of the shared assets  -> build/extract/names.json
python tools/extract/histo.py           # 1. which script classes each scene has -> build/extract/histo.json (slow)
python tools/extract/chests.py          # 2a. treasure chests  -> build/extract/chests.json
python tools/extract/battlerounds.py    # 2b. fights           -> build/extract/battlerounds.json
```

Copy the results over the files in `data/` (`chests.json` becomes `locations_chests.json`) and run `tools/build_data.py`.
`libscene.py` holds the shared scene helpers and explains how the game's Unity files are read.
