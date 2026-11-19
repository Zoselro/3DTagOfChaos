import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from lib import start, finish
from objs_wheat import *
src=open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'build2.py')).read()
exec(src[src.index("def zfight("):src.index("report = []")])
OUT = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT=[("HOB_WheatPatch_LOD0", lambda: wheat_patch(2.0, 110, 0, 7)), ("HOB_WheatPatch_LOD1", lambda: wheat_patch(2.0, 110, 1, 7))]
for name, fn in CAT:
    start(); fn(); o = finish(name); smooth_grains(o)
    tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d,2) for d in o.dimensions), "tris", tris, "cols", [c.name for c in o.data.color_attributes], flush=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_WheatPatch.blend"))
