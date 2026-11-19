import sys, os, math, time; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import start, finish
from objs_v2 import *
OUT = sys.argv[sys.argv.index("--")+1]
only = sys.argv[sys.argv.index("--")+2].split(",") if len(sys.argv) > sys.argv.index("--")+2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT = [("HOB_Tree_Fruit", tree_fruit_v2, 1.2), ("HOB_Tree_Round", tree_round_v2, 1.2), ("HOB_Fruit_Strawberry", strawberry_v2, 0.8),
       ("HOB_Cupcake_Pink", cupcake_v2, 0.6), ("HOB_Cupcake_Cream", lambda: cupcake_v2(top='Cream'), 0.6),
       ("HOB_MarketStall", market_stall_v2, 0.6), ("HOB_House_Cake", house_cake_v2, 1.5)]
def export(o, path):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, colors_type='SRGB')
x = 0.0
for name, fn, aod in CAT:
    if only and name not in only: continue
    t0 = time.time(); start(); fn(); o = finish(name)
    polish(o, angle=40.0, ao=True, ao_dist=aod)
    tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d,2) for d in o.dimensions), "tris", tris, "%.1fs" % (time.time()-t0), flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.5) + 2.0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_v2_pilot.blend"))
