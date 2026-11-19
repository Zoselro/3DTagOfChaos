# §89.22 베이커리 내부 소품(hq_bakery.BAKERY) — build_hq_all과 같은 마무리(부드러운 면·HQ 재질·정점 AO·FBX)
import sys, os, math, time; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Matrix
import lib
from lib import start, finish
from hq_bakery import BAKERY
from hq_ao import vertex_ao
import hqmat, hqbake
OUT = sys.argv[sys.argv.index("--") + 1]; os.makedirs(OUT, exist_ok=True)
only = sys.argv[sys.argv.index("--") + 2].split(",") if len(sys.argv) > sys.argv.index("--") + 2 and sys.argv[sys.argv.index("--") + 2] else None
bpy.ops.wm.read_factory_settings(use_empty=True)
hqmat.PREVIEW[0] = True
def export(o, path):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, colors_type='LINEAR', path_mode='STRIP')
x = 0.0
for name in (only or sorted(BAKERY)):
    t0 = time.time(); start(); BAKERY[name](); o = finish(name)
    me = o.data
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(42))
    hqbake.hq_materials(o)
    aom = vertex_ao(o, dist=min(3.0, max(0.25, max(o.dimensions) * 0.12)))
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d, 2) for d in o.dimensions), "tris", tris, "ao %.2f" % aom, "mats", ",".join(m.name[6:] for m in me.materials), "%.0fs" % (time.time() - t0), flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.0) + 1.0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_Bakery_props.blend"))
