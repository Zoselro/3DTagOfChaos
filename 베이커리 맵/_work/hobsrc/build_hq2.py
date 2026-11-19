import sys, os, math, time; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import lib
from lib import start, finish
from hq_props import *
from hq_ao import vertex_ao
import hqmat, hqbake
OUT = sys.argv[sys.argv.index("--")+1]
only = sys.argv[sys.argv.index("--")+2].split(",") if len(sys.argv) > sys.argv.index("--")+2 and sys.argv[sys.argv.index("--")+2] else None
bpy.ops.wm.read_factory_settings(use_empty=True)
hqmat.PREVIEW[0] = True
CAT = [("HOB_Fruit_Strawberry", hq_strawberry, 0.5), ("HOB_Fruit_Apple", hq_apple, 0.5), ("HOB_Fruit_Orange", hq_orange, 0.45),
       ("HOB_Cupcake_Pink", hq_cupcake, 0.35), ("HOB_LampPost_Tall", hq_lamp, 0.4), ("HOB_Tree_Fruit", hq_blossom_tree, 0.9), ("HOB_Tile_Star", hq_tile_star, 0.08)]
def export(o, path):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, colors_type='LINEAR', path_mode='STRIP')
x = 0.0
for name, fn, aod in CAT:
    if only and name not in only: continue
    t0 = time.time(); start(); fn(); o = finish(name)
    me = o.data
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(42))
    hqbake.hq_materials(o)
    aom = vertex_ao(o, dist=aod)
    tris = sum(len(p.vertices)-2 for p in me.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d,2) for d in o.dimensions), "tris", tris, "mats", len(me.materials), "ao %.2f" % aom, "%.0fs" % (time.time()-t0), flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.5) + 1.0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_HQ_pilot2.blend"))
