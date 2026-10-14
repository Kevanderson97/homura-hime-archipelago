"""Step 2a: list every treasure chest of the game and what it holds.

Writes `chests.json` to the work folder; the checked-in `data/locations_chests.json` is a copy of this output that
tools/build_data.py reads (it keeps only the "TreasureChest" rows).

    python tools/extract/chests.py     (needs build_names.py and histo.py to have run)
"""
import json

from libscene import SCENE_NAMES, Scene, work_path


def definitions(scene, infos, owner=None):
    """["<item definition> x<amount>", ...] for a list of DefinitionInfos entries."""
    result = []
    for info in infos or []:
        definition = info.get("Definition")
        result.append("%s x%s" % (scene.ptr_name(definition, owner) if definition else None, info.get("Amount")))
    return result


def scene_chests(scene, name, index):
    """The rows for the chests of one scene."""
    rows = []

    # Konpeito chests carry their payout in a separate "view" component on the same GameObject.
    konpeito_by_object = {scene.go_of(obj): scene.tt(obj).get("Value")
                          for obj in scene.by_class.get("TreasureChestView_Konpeitou", [])}

    for obj in scene.by_class.get("TreasureChest", []):
        fields = scene.tt(obj)
        game_object = scene.go_of(obj)

        konpeito = None
        view = fields.get("treasureChestView")
        if view and view["m_FileID"] == 0:
            view_obj = scene.local.get(view["m_PathID"])
            if view_obj is not None:
                konpeito = konpeito_by_object.get(scene.go_of(view_obj))

        # The contents: items listed on the chest itself plus the ones of the loot table it points at.
        table_name, items = None, []
        add_item = fields.get("treasureChestAddItem")
        if add_item and add_item["m_FileID"] == 0:
            add_obj = scene.local.get(add_item["m_PathID"])
            if add_obj is not None:
                add_fields = scene.tt(add_obj)
                table = add_fields.get("scriptTable")
                table_name = scene.ptr_name(table) if table else None
                items = definitions(scene, add_fields.get("DefinitionInfos"))
                if table:
                    table_obj, table_fields = scene.ext_obj(table), scene.ext_tt(table)
                    if table_fields and "DefinitionInfos" in table_fields:
                        items += ["[table] " + x
                                  for x in definitions(scene, table_fields["DefinitionInfos"], table_obj.assets_file)]
                    elif table_fields:
                        items.append("[table err] " + str(table_fields.get("__err__")))
        rows.append({"scene": name, "idx": index, "type": "TreasureChest", "go": scene.chain(game_object),
                     "pos": scene.world_pos(game_object), "konpeitou": konpeito, "table": table_name, "items": items})

    # Chests spawned by a flow (for example the reward of an ambush) only name their loot table.
    for obj in scene.by_class.get("CreatTreasureChestFlow", []):
        fields = scene.tt(obj)
        game_object = scene.go_of(obj)
        table = fields.get("treasureChestAddItemTable")
        items = []
        if table:
            table_obj, table_fields = scene.ext_obj(table), scene.ext_tt(table)
            if table_fields and "DefinitionInfos" in table_fields:
                items = definitions(scene, table_fields["DefinitionInfos"], table_obj.assets_file)
        rows.append({"scene": name, "idx": index, "type": "CreatTreasureChestFlow", "go": scene.chain(game_object),
                     "pos": scene.world_pos(game_object), "table": scene.ptr_name(table) if table else None,
                     "items": items})
    return rows


def main():
    with open(work_path("histo.json"), encoding="utf-8") as f:
        histo = json.load(f)

    out = []
    for key, classes in histo.items():
        index = int(key.split(":")[0])
        if not (classes.get("TreasureChest") or classes.get("CreatTreasureChestFlow")):
            continue
        out += scene_chests(Scene(index), SCENE_NAMES[index], index)
        print(key, "done", flush=True)

    with open(work_path("chests.json"), "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print("rows", len(out))


if __name__ == "__main__":
    main()
