import sys, os, math, time, re; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Matrix
import lib
from lib import start, finish
from objs_struct import spiral_ramp, stairs, bridge, lamp_post, pillar
from objs_misc import *
from objs_fruit import *
from objs_house import *
from objs2 import *
import objs2
from objs_items import island_main
from hq_props import *
from hq_struct import *
from hq_nature import *
from hq_misc import *
from hq_theme import *
from hq_fondue import *
from objs_v2 import market_stall_v2
from hq_ao import vertex_ao
import hqmat, hqbake
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index("--")+1]
only = sys.argv[sys.argv.index("--")+2].split(",") if len(sys.argv) > sys.argv.index("--")+2 and sys.argv[sys.argv.index("--")+2] else None
def cat_of(fname):
    src = open(os.path.join(HERE, fname)).read(); i = src.index("CAT = ["); j = src.index("\n]\n", i) + 2
    return eval(src[i + 6:j], globals())
CAT = {n: f for n, f, _ in cat_of("build.py") + cat_of("build2.py")}
CAT["HOB_Island_Main"] = island_main
# HQ redesigns override the old builders
CAT.update({"HOB_Fruit_Strawberry": hq_strawberry, "HOB_Fruit_Apple": hq_apple, "HOB_Fruit_Orange": hq_orange, "HOB_Cupcake_Pink": hq_cupcake,
            "HOB_LampPost_Tall": hq_lamp, "HOB_Tree_Fruit": hq_blossom_tree, "HOB_Tile_Star": hq_tile_star,
            "HOB_Bridge_10m": hq_bridge, "HOB_Stairs_3m": lambda: hq_stairs(3.0), "HOB_Stairs_6m": lambda: hq_stairs(6.0, landing=2.5),
            "HOB_SpiralRamp_9m": lambda: hq_spiral_ramp(9.0), "HOB_SpiralRamp_12m": lambda: hq_spiral_ramp(12.0), "HOB_SpiralRamp_15m": lambda: hq_spiral_ramp(15.0),
            "HOB_Plateau_Almond": lambda: hq_sky_island(30.0, 6.0, 22.0, 1), "HOB_Plateau_Choco": lambda: hq_sky_island(32.0, 6.0, 24.0, 2), "HOB_SkyIslet": lambda: hq_sky_island(5.2, 1.4, 8.0, 5, bushes=False), "HOB_SwitchRamp_24m": lambda: hq_switch_ramp(24.0), "HOB_Plateau_Flour": lambda: hq_plateau(30.0, 9.0, 3),
            "HOB_Island_Main": lambda: hq_island(100.0, 30.0, 15), "HOB_RoundPlaza": hq_round_plaza,
            "HOB_Pillar_Lamp": lambda: lantern_pillar(0, 0, 0, h=1.4), "HOB_Pillar": lambda: lantern_pillar(0, 0, 0, h=1.4, lamp=False),
            "HOB_Tree_Round": hq_round_tree, "HOB_Tree_Pine_L": lambda: hq_pine(5.0, 3), "HOB_Tree_Pine_S": lambda: hq_pine(3.0, 4), "HOB_AlmondTree": hq_almond_tree,
            "HOB_Bush_Round": lambda: hq_bush(0.6, seed=11), "HOB_Bush_Berry": lambda: hq_bush(0.6, berries=True, seed=12), "HOB_FlowerBush": lambda: hq_bush(0.7, seed=13, flowers=10),
            "HOB_FlowerBed": hq_flower_bed, "HOB_Hedge_2m": hq_hedge, "HOB_RockBush": hq_rock_bush, "HOB_RockMushroom": hq_rock_mushroom,
            "HOB_Fruit_Grapes": hq_grapes, "HOB_Fruit_Cherries": hq_cherries, "HOB_Fruit_Pumpkin": hq_pumpkin, "HOB_Cupcake_Cream": hq_cupcake_cream,
            "HOB_StrawberryCupcake": hq_strawberry_cupcake, "HOB_StrawberryChocoCake": hq_choco_cake, "HOB_ChocolateBar": hq_chocolate_bar, "HOB_Truffles": hq_truffles,
            "HOB_CandyCanes": hq_candy_canes, "HOB_CandyCane_S": hq_candy_cane_s, "HOB_ChocoPillar": hq_choco_pillar, "HOB_ChocoFountain": hq_choco_fountain,
            "HOB_House_Cake": hq_house_cake, "HOB_House_TierCake": hq_house_tier, "HOB_House_Strawberry": hq_house_strawberry,
            "HOB_Crate": hq_crate, "HOB_Barrel": hq_barrel, "HOB_Bench": hq_bench, "HOB_Sack": hq_sack, "HOB_Urn": hq_urn, "HOB_Signpost": hq_signpost,
            "HOB_TreasureChest": hq_treasure_chest, "HOB_WoodFootbridge": hq_footbridge, "HOB_CandyArch": hq_candy_arch, "HOB_CandyCaneLamp": hq_candy_cane_lamp,
            "HOB_FruitCart": hq_fruit_cart, "HOB_MarketStall": market_stall_v2, "HOB_Cloud_L": lambda: hq_cloud(1.6, 5), "HOB_Cloud_S": lambda: hq_cloud(0.9, 6),
            "HOB_LampPost_Short": lambda: lantern_pillar(0, 0, 0, h=1.6), "HOB_Tile_Checker": hq_tile_checker, "HOB_Tile_Diamond": hq_tile_diamond,
            "HOB_Tile_Circle": hq_tile_circle, "HOB_Tile_Red": hq_tile_red, "HOB_Tile_Grass": hq_tile_grass, "HOB_Tile_FlowerGrass": lambda: hq_tile_grass(True)})
CAT.update({"HOB_Tile_Almond": hq_tile_almond, "HOB_Tile_AlmondPlain": hq_tile_almond_plain, "HOB_Tile_Choco": hq_tile_choco, "HOB_Tile_ChocoPlain": hq_tile_choco_plain,
            "HOB_Tile_Flour": hq_tile_flour, "HOB_Tile_FlourPlain": hq_tile_flour_plain, "HOB_GoldFence": hq_gold_fence, "HOB_GoldPost": hq_gold_post,
            "HOB_WheatBanner": hq_wheat_banner, "HOB_GoldLamp": hq_gold_lamp, "HOB_AlmondBig": hq_almond_big, "HOB_AlmondHalf": hq_almond_half,
            "HOB_AlmondCrate": hq_almond_crate, "HOB_AlmondSackDeco": hq_almond_sack, "HOB_FonduePot": hq_fondue_pot, "HOB_FondueSkewer": hq_fondue_skewer, "HOB_ChocoPuddle": hq_choco_puddle, "HOB_ChocoPuddleB": lambda: hq_choco_puddle(1.5, 9), "HOB_ChocoBlock": hq_choco_block, "HOB_Truffle": hq_truffle,
            "HOB_HeartBonbon": hq_heart_bonbon, "HOB_ChocoCube": hq_choco_cube, "HOB_WheatBundle": hq_wheat_bundle, "HOB_FlourSack": hq_flour_sack,
            "HOB_FlourScoop": hq_flour_scoop, "HOB_PinkFlowerBush": hq_pink_flower_bush, "HOB_BlossomTreeS": hq_blossom_tree_s})   # §89.16
for k, v in list(globals().items()):
    if k.startswith("hq_override_"): CAT["HOB_" + k[len("hq_override_"):]] = v
POST = {"HOB_Island_Main": lambda o: (o.data.transform(Matrix.Translation((0, 0, -30.0))), o.data.update()), **{n: (lambda h: (lambda o: (o.data.transform(Matrix.Translation((0, 0, -h))), o.data.update())))(h) for n, h in (("HOB_Plateau_Almond", 6.0), ("HOB_Plateau_Choco", 6.0), ("HOB_SkyIslet", 1.4))}}
bpy.ops.wm.read_factory_settings(use_empty=True)
hqmat.PREVIEW[0] = True
def export(o, path):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, colors_type='LINEAR', path_mode='STRIP')
x = 0.0
names = only or sorted(CAT)
for name in names:
    if name not in CAT: print("SKIP", name); continue
    t0 = time.time(); start(); CAT[name](); o = finish(name)
    if name in POST: POST[name](o)
    me = o.data
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(42))
    hqbake.hq_materials(o)
    size = max(o.dimensions)
    aom = vertex_ao(o, dist=min(3.0, max(0.25, size * 0.12)))
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    export(o, os.path.join(OUT, name + ".fbx"))
    print("OBJ", name, tuple(round(d, 1) for d in o.dimensions), "tris", tris, "ao %.2f" % aom, "mats", ",".join(m.name[6:] for m in me.materials), "%.0fs" % (time.time() - t0), flush=True)
    o.location.x = x; x += max(o.dimensions.x, 1.5) + 2.0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "HOB_HQ_batch.blend"))
