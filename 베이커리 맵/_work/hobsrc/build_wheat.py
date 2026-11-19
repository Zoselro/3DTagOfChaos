import sys, os, json, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import start, finish
from objs_wheat import *
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'build2.py')).read().split("CAT = [")[0].split("bpy.ops.wm.read_factory_settings")[0])
src=open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'build2.py')).read()
exec(src[src.index("def zfight("):src.index("report = []")])
OUT = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT=[("HOB_WheatEar_Hero", wheat_ear_hero), ("HOB_WheatClump_LOD0", lambda: wheat_clump(9,4,0)), ("HOB_WheatClump_LOD1", lambda: wheat_clump(9,4,1))]
for name, fn in CAT:
    start(); fn(); o = finish(name); smooth_grains(o)
    bad, ex = zfight(o); tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d,2) for d in o.dimensions), "tris", tris, "zfight", bad, ex, "cols", [c.name for c in o.data.color_attributes], flush=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_Wheat.blend"))
