import sys, os, json, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import start, finish
from objs_wheat import *
from objs_items import *
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'build2.py')).read().split("CAT = [")[0].split("bpy.ops.wm.read_factory_settings")[0])
src=open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'build2.py')).read()
exec(src[src.index("def zfight("):src.index("report = []")])
OUT = sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT=[(n,f) for n,f in [("ITEM_AlmondSack", item_almond_sack), ("ITEM_FlourSack", item_flour_sack), ("ITEM_StrawberryJam", item_jam_jar), ("HOB_Island_Main", island_main)]]
for name, fn in CAT:
    start(); fn(); o = finish(name)
    if name.startswith("ITEM_"):
        bb=[o.matrix_world @ Vector(c) for c in o.bound_box]; cz=(min(v.z for v in bb)+max(v.z for v in bb))/2
        o.data.transform(__import__('mathutils').Matrix.Translation((0,0,-cz))); o.data.update()
    if name == "HOB_Island_Main":
        o.data.transform(__import__('mathutils').Matrix.Translation((0,0,-30.0))); o.data.update()
    bad, ex = zfight(o); tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d,2) for d in o.dimensions), "tris", tris, "zfight", bad, ex, "cols", [c.name for c in o.data.color_attributes], flush=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_Items_Island.blend"))
