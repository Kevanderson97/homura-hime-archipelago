"""Step 2b: list every fight (BattleRound) of the game.

Each BattleRound becomes an "encounter" location: it is sent when the round finishes. A round is identified by its
scene and the names of its parent objects (`chain`). Writes `battlerounds.json` to the work folder; the checked-in
`data/battlerounds.json` is a copy of this output that tools/build_data.py reads.

    python tools/extract/battlerounds.py     (needs histo.py to have run)
"""
import json

from libscene import SCENE_NAMES, Scene, work_path

# Script classes that mark a scene as containing fights.
FIGHT_CLASSES = ("BattleRound", "FreeBattleRound", "ExtraChallengeController")


def main():
    with open(work_path("histo.json"), encoding="utf-8") as f:
        histo = json.load(f)

    rows = []
    for key, classes in histo.items():
        index = int(key.split(":")[0])
        if not any(classes.get(name) for name in FIGHT_CLASSES):
            continue
        scene = Scene(index)
        for obj in scene.by_class.get("BattleRound", []):
            fields = scene.tt(obj)
            game_object = scene.go_of(obj)
            rows.append({
                "scene": SCENE_NAMES[index], "idx": index, "type": "BattleRound",
                "chain": scene.chain(game_object, 5), "pos": scene.world_pos(game_object),
                "extra": fields.get("isExtraChallenge"), "rank": fields.get("ShowBattleRankScoreEvalutor"),
                "exitflow": fields.get("useBattleExitFlow"),
            })
        for obj in scene.by_class.get("FreeBattleRound", []):
            game_object = scene.go_of(obj)
            rows.append({"scene": SCENE_NAMES[index], "idx": index, "type": "FreeBattleRound",
                         "chain": scene.chain(game_object, 5), "pos": scene.world_pos(game_object)})
        for obj in scene.by_class.get("ExtraChallengeController", []):
            game_object = scene.go_of(obj)
            rows.append({"scene": SCENE_NAMES[index], "idx": index, "type": "ExtraChallengeController",
                         "chain": scene.chain(game_object, 4), "pos": scene.world_pos(game_object)})
        print(key, len(rows), flush=True)

    with open(work_path("battlerounds.json"), "w", encoding="utf-8") as f:
        json.dump(rows, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
