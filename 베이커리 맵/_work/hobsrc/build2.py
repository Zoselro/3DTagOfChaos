import sys, os, json, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import lib
from lib import start, finish
from objs_struct import spiral_ramp, stairs, bridge, lamp_post, pillar
from objs_misc import *
from objs_fruit import *
from objs_house import *
from objs2 import *
import objs2
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "out"
only = sys.argv[sys.argv.index("--")+2].split(",") if "--" in sys.argv and len(sys.argv) > sys.argv.index("--")+2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT = [
 ("HOB_Plateau_Almond", lambda: plateau(30.0, 12.0, 1), "지형"), ("HOB_Plateau_Choco", lambda: plateau(32.0, 15.0, 2), "지형"), ("HOB_Plateau_Flour", lambda: plateau(30.0, 9.0, 3), "지형"),
 ("HOB_CliffWall_10m", lambda: cliff_wall(10.0, 6.0), "지형"), ("HOB_WaterfallCliff", waterfall_cliff, "지형"),
 ("HOB_Cloud_L", lambda: cloud(1.6, 5), "지형"), ("HOB_Cloud_S", lambda: cloud(0.9, 6), "지형"), ("HOB_RockBush", rock_bush, "지형"), ("HOB_RockMushroom", rock_mushroom, "지형"),
 ("HOB_Bld_AlmondFactory", almond_factory, "재료 건물"), ("HOB_Bld_FlourMill", flour_mill, "재료 건물"), ("HOB_Bld_FlourMill_Blades", flour_mill_blades, "재료 건물"),
 ("HOB_Bld_CakeShop", cake_shop, "재료 건물"), ("HOB_Bld_ChocoHouse", choco_house, "재료 건물"),
 ("HOB_ChocoFountain", choco_fountain, "광장"), ("HOB_ExitPortal", exit_portal, "탈출구"),
 ("HOB_AlmondTree", almond_tree, "소품"), ("HOB_WheatPatch_3m", wheat_patch, "소품"), ("HOB_CrownFence_2m", crown_fence, "소품"), ("HOB_ChocolateBar", chocolate_bar, "소품"),
 ("HOB_Truffles", truffles, "소품"), ("HOB_StrawberryChocoCake", strawberry_choco_cake, "소품"), ("HOB_CandyCanes", candy_canes, "소품"), ("HOB_CandyCane_S", small_candy_cane, "소품"),
 ("HOB_Signpost", signpost, "소품"), ("HOB_BannerLamp", banner_lamp, "소품"), ("HOB_CandyArch", candy_arch, "소품"), ("HOB_WoodFootbridge", wood_footbridge, "소품"),
 ("HOB_CrownBanner", crown_banner, "소품"), ("HOB_Crate", crate, "소품"), ("HOB_TreasureChest", treasure_chest, "소품"), ("HOB_Sack", sack, "소품"),
 ("HOB_CrownStonePillar", crown_stone_pillar, "소품"), ("HOB_ChocoPillar", choco_pillar, "소품"), ("HOB_FlowerBush", flower_bush, "소품"), ("HOB_StrawberryCupcake", strawberry_cupcake, "소품"),
 ("HOB_TaggerGazebo", tagger_gazebo, "술래방"),
]
POST = {'HOB_WaterfallCliff': lambda o: lib.uv_planar(o, 0, 2, 0.25), 'HOB_ChocoFountain': lambda o: lib.uv_planar(o, 0, 2, 0.5), 'HOB_WheatPatch_3m': objs2.wheat_post}
def zfight(o, tol=0.0008):
    """count faces that lie on another face of the SAME facing (coplanar overlap) — hidden back-to-back faces are allowed"""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    # islands
    isl = {}; cur = 0
    for f in bm.faces:
        if f.index in isl: continue
        stack = [f]; isl[f.index] = cur
        while stack:
            g = stack.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in isl: isl[h.index] = cur; stack.append(h)
        cur += 1
    tree = BVHTree.FromBMesh(bm); bad = 0; ex = []; hidden = [0]
    for f in bm.faces:
        if f.calc_area() < 1e-6: continue
        c = f.calc_center_median(); n = f.normal
        samples = [c] + [c.lerp(v.co, 0.6) for v in f.verts]
        for sp in samples:
            hits = tree.find_nearest_range(sp, tol)
            hit = False
            for (loc, nor, idx, dist) in hits:
                if idx == f.index: continue
                g = bm.faces[idx]
                if g.normal.dot(n) > 0.999 and isl[idx] != isl[f.index]: hit = True; break   # same plane, same facing (back-to-back touching faces are sealed inside)
            if hit:
                # visible? a point just in front of the face that is inside another solid means the face is buried
                q = c + n*0.003; cnt = 0; o2 = q.copy(); dirv = Vector((0.5773, 0.5774, 0.5775))
                for _ in range(64):
                    loc, nor, idx, dist = tree.ray_cast(o2, dirv)
                    if loc is None: break
                    cnt += 1; o2 = loc + dirv*1e-4
                if cnt % 2 == 0: bad += 1; ex.append((tuple(round(x,3) for x in c), f.material_index))
                else: hidden[0] += 1
                break
    bm.free(); zfight.hidden = hidden[0]; return bad, ex[:3]
def export(o, path):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
report = []
x = 0.0
for name, fn, cat in CAT:
    if only and name not in only: continue
    start(); fn(); o = finish(name)
    if name in POST: POST[name](o)
    bad, ex = zfight(o)
    dims = tuple(round(d, 2) for d in o.dimensions)
    tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    report.append({"name": name, "cat": cat, "size": dims, "tris": tris, "zfight": bad, "buried": zfight.hidden, "ex": ex, "mats": [m.name for m in o.data.materials]})
    print("OBJ", name, dims, "tris", tris, "zfight_visible", bad, "buried", zfight.hidden, ex, flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.5) + 2.0
json.dump(report, open(os.path.join(OUT, "report.json"), "w"), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_Props2.blend"))
