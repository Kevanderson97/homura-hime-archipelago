"""Shared helpers for the extraction scripts: open one Unity scene of the game and query it.

Homura Hime is a Unity game. Its levels are stored as numbered scene files (`HomuraHime_Data/level<N>`) whose objects
reference each other by (file id, path id) pairs. `Scene` loads such a file with all the asset files it points at (so
ScriptableObjects such as item definitions can be read too) and offers small lookups on top of UnityPy:

- which MonoBehaviours of a given script class exist (`by_class`),
- the name, parent chain and position of the GameObject a component sits on (`chain`, `world_pos`),
- the name of an object another asset file refers to (`ptr_name`).

Configuration (environment variables, both optional):
    HOMURA_HIME_DIR   the game install folder (default: the usual Steam location)
    HOMURA_HIME_WORK  where the scripts keep their intermediate files (default: <repo>/build/extract)

Requires `pip install UnityPy`. The game files are only read; nothing from them is copied into this repository except
the small tables the scripts write (see tools/extract/README.md).
"""
import json
import os
import struct

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
GAME_DIR = os.environ.get("HOMURA_HIME_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\Homura Hime")
DATA_DIR = os.path.join(GAME_DIR, "HomuraHime_Data")
WORK_DIR = os.environ.get("HOMURA_HIME_WORK", os.path.join(REPO_ROOT, "build", "extract"))

# The Unity version the game was built with; UnityPy needs it to generate type trees for the game's own scripts.
UNITY_VERSION = "6000.0.28f1"

# Scene names in build order: SCENE_NAMES[n] is the scene stored in the file `level<n>`.
SCENE_NAMES = [
    "StartScreen", "MainCore", "HubWorld_Main", "HubWorld_PostGame_Main", "HubWorld_TrainingGround_Main", "Level_0_Main",
    "Level1Boss_Main", "Level_1_Area_1_Main", "Level_1_Area_2_Main", "Level_1_Area_3_Main", "Level_1_BossPass2",
    "Level_1_Ikai_Part_1_Main", "Level2Boss_Main", "Level_2_Area_1_Main", "Level_2_Area_2_Main", "Level_2_Area_3_Main",
    "Level_2_Area_4_Main", "Level_2_Area_5_2F_Main", "Level_2_Area_5_Main", "Level_2_Area_6_Main", "Level_2_Area_7_Main",
    "Level_2_Area_8_Main", "Level_2_Area_9_Main", "Level_2_Ikai_Part1_Main", "Level_2_MidBoss_Main", "Level3Boss_Main",
    "Level_3_Area_1_Main", "Level_3_Area_2_Main", "Level_3_Area_3_Main", "Level_3_Area_4_Main", "Level_3_Area_5_Main",
    "Level_3_Area_6_Main", "Level_3_Area_7_Main", "Level_3_Ikai_Part1_Main", "Level_3_Ikai_Part2_Main",
    "Level3_SecretZone_Main", "Level_4_Area_1_Main", "Level_4_Area_2_Main", "Level_4_Area_3_Main", "Level_4_Area_4_Main",
    "Level_4_Area_Arena_Main", "Level_4_Boss", "Level_4_BossPhase2", "Level_4_Ikai_Part1_Main", "Level_4_Ikai_Part2_Main",
    "Level_5_Area_1_Main", "Level_5_Area_2_Main", "Level_5_Area_3_Main", "Level_5_Area_4_Main", "Level_5_Area_5_Main",
    "Level_5_Boss", "Level_5_Ikai_Part1_Main", "Level_5_Ikai_Part2_Main", "Level_FinalP0_Jinguu_Main",
    "Level_FinalP1_L1_A3_Main", "Level_FinalP1_L1_Ikai_Main", "Level_FinalP1_L1Boss_Main", "Level_FinalP1_L2_A5_Main",
    "Level_FinalP1_L2_Ikai_Main", "Level_FinalP1_L2Boss_Main", "Level_FinalP2_L3_A5_Main", "Level_FinalP2_L3Boss_Main",
    "Level_FinalP2_L4_A2_Main", "Level_FinalP2_L4_Ikai_Main", "Level_FinalP2_L4Boss_Phase1_Main",
    "Level_FinalP2_L4Boss_Phase2_Main", "Level_FinalP3_L5_A2_Main", "Level_FinalP3_L5_A2_Phase2_Main",
    "Level_FinalP3_L5_A5_Main", "Level_FinalP3_L5_Ikai_Main", "Level_FinalP3_L5Boss_Main", "Level_FinalP4_EndFinale_Main",
    "Level_FinalP4_Jinguu_Ending_Main", "Level_FinalP4_Jinguu_Phase1_Main", "Level_FinalP4_Jinguu_Phase2_Main",
    "Level_FinalP4_Passage_Main", "LoadingScene", "Level_3_Area_5_Boss_Main",
]

# Byte layout of a serialized MonoBehaviour header, used to read a few fields without building the whole (slow) type tree:
#   offset 4   the PPtr<GameObject> the component sits on (path id, 8 bytes)
#   offset 16  the PPtr<MonoScript> of its class (file id 4 bytes + path id 8 bytes)
#   offset 28  length of the object's name, followed by the UTF-8 name itself at offset 32
_GAME_OBJECT_OFFSET = 4
_SCRIPT_OFFSET = 16
_NAME_LENGTH_OFFSET = 28
_NAME_OFFSET = 32

_type_tree_generator = None


def type_tree_generator():
    """The (expensive, so created once) generator of type trees for the game's own script classes."""
    global _type_tree_generator
    if _type_tree_generator is None:
        _type_tree_generator = TypeTreeGenerator(UNITY_VERSION)
        _type_tree_generator.load_local_game(GAME_DIR)
    return _type_tree_generator


def read_object_name(raw):
    """The name stored in a serialized ScriptableObject/MonoBehaviour, or None if the bytes do not hold a sane one."""
    (length,) = struct.unpack_from("<i", raw, _NAME_LENGTH_OFFSET)
    if 0 <= length < 200:
        return raw[_NAME_OFFSET:_NAME_OFFSET + length].decode("utf-8", "replace")
    return None


def work_path(name):
    """Path of an intermediate file in the work folder (the folder is created on demand)."""
    os.makedirs(WORK_DIR, exist_ok=True)
    return os.path.join(WORK_DIR, name)


class Scene:
    """One scene file of the game, with the objects it references indexed for lookups."""

    def __init__(self, index):
        self.idx = index
        self.scene = "level%d" % index
        env = UnityPy.load(os.path.join(DATA_DIR, "globalgamemanagers.assets"), os.path.join(DATA_DIR, self.scene))

        # Pull in the asset files the scene refers to (they hold the item definitions and other ScriptableObjects).
        for loaded in list(env.files.values()):
            for external in getattr(loaded, "externals", []):
                path = os.path.join(DATA_DIR, os.path.basename(external.path))
                if os.path.exists(path) and path not in env.files:
                    try:
                        env.load_file(path)
                    except Exception:
                        pass  # an asset file UnityPy cannot read; names from it simply stay unresolved
        env.typetree_generator = type_tree_generator()
        self.env = env
        self.sf = [f for k, f in env.files.items() if os.path.basename(k) == self.scene][0]
        self.ext = [os.path.basename(e.path) for e in self.sf.externals]  # file id n (n >= 1) is self.ext[n - 1]

        # scripts: (file, path id) -> script class name;  local: path id -> object of this scene;
        # extobj: (file, path id) -> object of another asset file;  ext_names: names of those objects
        self.scripts, self.local, self.extobj, self.ext_names = {}, {}, {}, {}
        for obj in env.objects:
            file_name = os.path.basename(obj.assets_file.name)
            if obj.type.name == "MonoScript":
                try:
                    self.scripts[(file_name, obj.path_id)] = obj.read(check_read=False).m_ClassName
                except Exception:
                    pass
            if file_name == self.scene:
                self.local[obj.path_id] = obj
            else:
                self.extobj[(file_name, obj.path_id)] = obj
                if obj.type.name == "MonoBehaviour":
                    try:
                        name = read_object_name(obj.get_raw_data())
                        if name is not None:
                            self.ext_names[(file_name, obj.path_id)] = name
                    except Exception:
                        pass
        self._game_object_names = {}

        # class name -> the MonoBehaviours of this scene with that class
        self.by_class = {}
        for obj in self.local.values():
            if obj.type.name == "MonoBehaviour":
                self.by_class.setdefault(self.class_of(obj), []).append(obj)

    # ---------------------------------------------------------------- objects
    def class_of(self, obj):
        """The script class name of a MonoBehaviour ("?" when its script is unknown)."""
        raw = obj.get_raw_data()
        file_id, path_id = struct.unpack_from("<iq", raw, _SCRIPT_OFFSET)
        file_name = self.scene if file_id == 0 else os.path.basename(obj.assets_file.externals[file_id - 1].path)
        return self.scripts.get((file_name, path_id), "?")

    def go_of(self, obj):
        """Path id of the GameObject a component sits on."""
        (path_id,) = struct.unpack_from("<q", obj.get_raw_data(), _GAME_OBJECT_OFFSET)
        return path_id

    def gname(self, game_object_id):
        """Name of a GameObject of this scene."""
        if game_object_id not in self._game_object_names:
            obj = self.local.get(game_object_id)
            name = "?"
            if obj is not None:
                try:
                    name = obj.read(check_read=False).m_Name
                except Exception:
                    pass
            self._game_object_names[game_object_id] = name
        return self._game_object_names[game_object_id]

    def tt(self, obj):
        """The type tree (all serialized fields) of an object, or {"__err__": ...} when it cannot be read."""
        try:
            return obj.read_typetree()
        except Exception as error:
            return {"__err__": repr(error)}

    # ---------------------------------------------------------------- hierarchy and position
    def transform(self, game_object_id):
        """The Transform (or RectTransform) component of a GameObject."""
        obj = self.local.get(game_object_id)
        if obj is None:
            return None
        for component in obj.read(check_read=False).m_Component:
            candidate = self.local.get(component.component.m_PathID)
            if candidate is not None and candidate.type.name in ("Transform", "RectTransform"):
                return candidate
        return None

    def _parent(self, transform_data):
        """GameObject id of the parent of a transform, or None for a root object."""
        if transform_data.m_Father.m_PathID == 0:
            return None
        parent = self.local.get(transform_data.m_Father.m_PathID)
        if parent is None:
            return None
        return parent.read(check_read=False).m_GameObject.m_PathID

    def chain(self, game_object_id, depth=6):
        """Names of a GameObject and its parents, innermost first, joined with " < " (at most `depth` names)."""
        names, current = [], game_object_id
        for _ in range(depth):
            names.append(self.gname(current))
            transform = self.transform(current)
            if transform is None:
                break
            current = self._parent(transform.read(check_read=False))
            if current is None:
                break
        return " < ".join(names)

    def world_pos(self, game_object_id):
        """Sum of the local positions up the hierarchy. Rotation and scale are ignored, so this is only good enough to
        tell objects of one scene apart (it is what makes a location's `pos` a stable identity)."""
        x = y = z = 0.0
        current = game_object_id
        for _ in range(40):  # bounded in case of a broken hierarchy
            transform = self.transform(current)
            if transform is None:
                break
            data = transform.read(check_read=False)
            x += data.m_LocalPosition.x
            y += data.m_LocalPosition.y
            z += data.m_LocalPosition.z
            current = self._parent(data)
            if current is None:
                break
        return [round(x, 1), round(y, 1), round(z, 1)]

    # ---------------------------------------------------------------- references between objects
    _global_names = None

    @classmethod
    def global_names(cls):
        """{asset file: {path id: name}} for the game's shared asset files, written by build_names.py."""
        if cls._global_names is None:
            with open(work_path("names.json"), encoding="utf-8") as f:
                cls._global_names = json.load(f)
        return cls._global_names

    def ptr_name(self, pointer, owner=None):
        """
        The name of the object a PPtr dict ({"m_FileID", "m_PathID"}) points at.
        `owner` is the asset file the PPtr is stored in; None means the scene file itself.
        """
        file_id, path_id = pointer["m_FileID"], pointer["m_PathID"]
        if path_id == 0:
            return None
        if owner is None:
            if file_id == 0:  # an object of this scene
                obj = self.local.get(path_id)
                if obj is None:
                    return "?%d" % path_id
                if obj.type.name == "GameObject":
                    return "GO:" + self.gname(path_id)
                return "%s:%d" % (obj.type.name, path_id)
            file_name = self.ext[file_id - 1]
        elif file_id == 0:
            file_name = os.path.basename(owner.name)
        else:
            file_name = os.path.basename(owner.externals[file_id - 1].path)
        return self.global_names().get(file_name, {}).get(str(path_id), "%s:?%d" % (file_name, path_id))

    def ext_obj(self, pointer):
        """The object in another asset file that a PPtr of the scene points at."""
        file_id, path_id = pointer["m_FileID"], pointer["m_PathID"]
        if file_id == 0 or path_id == 0:
            return None
        return self.extobj.get((self.ext[file_id - 1], path_id))

    def ext_tt(self, pointer):
        """Type tree of an object referenced in an external asset file (for example a ScriptableObject)."""
        obj = self.ext_obj(pointer)
        if obj is None:
            return None
        return self.tt(obj)
