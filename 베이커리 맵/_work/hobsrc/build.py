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
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else "out"
only = sys.argv[sys.argv.index("--")+2].split(",") if "--" in sys.argv and len(sys.argv) > sys.argv.index("--")+2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
CAT = [
 # (name, builder, category)
 ("HOB_SpiralRamp_9m", lambda: spiral_ramp(9.0), "구조"), ("HOB_SpiralRamp_12m", lambda: spiral_ramp(12.0), "구조"), ("HOB_SpiralRamp_15m", lambda: spiral_ramp(15.0), "구조"), ("HOB_Stairs_3m", lambda: stairs(3.0), "구조"), ("HOB_Stairs_6m", lambda: stairs(6.0, landing=2.5), "구조"),
 ("HOB_Bridge_10m", lambda: bridge(), "구조"),
 ("HOB_House_Cake", house_cake, "출입 건물"), ("HOB_House_TierCake", house_tier, "출입 건물"), ("HOB_House_Strawberry", house_strawberry, "출입 건물"),
 ("HOB_LampPost_Tall", lambda: lamp_post(h=3.2), "울타리·기둥"), ("HOB_LampPost_Short", lambda: lamp_post(h=1.6, scale=0.85), "울타리·기둥"),
 ("HOB_CandyCaneLamp", candy_cane_lamp, "울타리·기둥"), ("HOB_Pillar_Lamp", lambda: pillar(0,0,0,h=1.4), "울타리·기둥"), ("HOB_Pillar", lambda: pillar(0,0,0,h=1.4,lamp=False), "울타리·기둥"),
 ("HOB_Fence_2m", fence, "울타리·기둥"), ("HOB_Gate", gate, "울타리·기둥"), ("HOB_BannerPole", banner_pole, "울타리·기둥"),
 ("HOB_Tile_Diamond", tile_diamond, "바닥 타일"), ("HOB_Tile_Circle", tile_circle, "바닥 타일"), ("HOB_Tile_Star", tile_star, "바닥 타일"),
 ("HOB_Tile_Red", tile_red, "바닥 타일"), ("HOB_Tile_Checker", tile_checker, "바닥 타일"), ("HOB_Tile_Grass", tile_grass, "바닥 타일"), ("HOB_Tile_FlowerGrass", lambda: tile_grass(True), "바닥 타일"),
 ("HOB_RoundPlaza", round_plaza, "바닥"),
 ("HOB_Bush_Round", bush_round, "조경"), ("HOB_Bush_Berry", lambda: bush_round(berries=True), "조경"), ("HOB_Tree_Round", tree_round, "조경"),
 ("HOB_Tree_Fruit", tree_fruit, "조경"), ("HOB_Tree_Pine_L", lambda: tree_pine(5.0), "조경"), ("HOB_Tree_Pine_S", lambda: tree_pine(3.0), "조경"),
 ("HOB_FlowerBed", flower_bed, "조경"), ("HOB_Hedge_2m", hedge, "조경"),
 ("HOB_Urn", urn, "기타"), ("HOB_Barrel", barrel, "기타"), ("HOB_Bench", bench, "기타"), ("HOB_MarketStall", market_stall, "기타"), ("HOB_FruitCart", fruit_cart, "기타"),
 ("HOB_Fruit_Strawberry", strawberry, "과일 조형물"), ("HOB_Fruit_Apple", apple, "과일 조형물"), ("HOB_Fruit_Orange", orange, "과일 조형물"),
 ("HOB_Fruit_Grapes", grapes, "과일 조형물"), ("HOB_Fruit_Cherries", cherries, "과일 조형물"), ("HOB_Fruit_Pumpkin", pumpkin, "과일 조형물"),
 ("HOB_Cupcake_Pink", cupcake, "과일 조형물"), ("HOB_Cupcake_Cream", lambda: cupcake(top='Cream'), "과일 조형물"),
]
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
    bad, ex = zfight(o)
    dims = tuple(round(d, 2) for d in o.dimensions)
    tris = sum(len(p.vertices)-2 for p in o.data.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    report.append({"name": name, "cat": cat, "size": dims, "tris": tris, "zfight": bad, "buried": zfight.hidden, "ex": ex, "mats": [m.name for m in o.data.materials]})
    print("OBJ", name, dims, "tris", tris, "zfight_visible", bad, "buried", zfight.hidden, ex, flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.5) + 2.0
json.dump(report, open(os.path.join(OUT, "report.json"), "w"), ensure_ascii=False, indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_Props.blend"))
