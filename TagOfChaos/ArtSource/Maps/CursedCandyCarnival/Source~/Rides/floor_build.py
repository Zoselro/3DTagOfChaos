# Cursed Candy Carnival floor tile objects (2026-10-22 §34): flat squares (2 m base tiles, 4 m pattern tiles), origin at the
# centre on the ground, UV 0..1 over the tile, one PBR material each (albedo + OpenGL normal + metal/smoothness from
# make_floor.py). Exports one FBX per tile, a .blend, and pictures: the 8 tiles from above, a 16 x 16 m plaza with the map
# objects and a 2 m mannequin (high angle + eye level close-up to judge the texel density).
# python floor_build.py -- <floor tex dir> <map objects .blend> <out dir> [render 1|0]
import bpy, sys, os, math, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ride_common as rc

argv = sys.argv[sys.argv.index("--") + 1:]
TEX, MO_BLEND, OUT = (os.path.abspath(p) for p in argv[:3])
RENDER = len(argv) < 4 or argv[3] != "0"
os.makedirs(OUT, exist_ok=True)

TILES = {"FL_Basic": 2.0, "FL_Wet": 2.0, "FL_Cobble": 2.0, "FL_Sewer": 2.0,
         "FL_PatternCompass": 4.0, "FL_PatternWheel": 4.0, "FL_PatternDiamond": 4.0, "FL_PatternFlower": 4.0}

rc.reset(TEX)
scene = rc.scene


def tile_material(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    def tex(suffix, color):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(os.path.join(TEX, f"{name}_{suffix}.png"))
        n.image.colorspace_settings.name = "sRGB" if color else "Non-Color"
        return n
    nt.links.new(tex("albedo", True).outputs["Color"], b.inputs["Base Color"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tex("normal", False).outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    ms = tex("metalSmooth", False)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(ms.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(ms.outputs["Alpha"], inv.inputs[1])
    nt.links.new(inv.outputs["Value"], b.inputs["Roughness"])
    return m


tiles = {}
for name, size in TILES.items():
    bpy.ops.mesh.primitive_plane_add(size=size, location=(0, 0, 0))
    o = rc.active(); o.name = name; o.data.name = name
    o.data.materials.append(tile_material(name))
    o.data.uv_layers[0].name = "UVMap"
    tiles[name] = o
    rc.export([o], os.path.join(OUT, name + ".fbx"))
json.dump(dict(tiles={n: dict(size_m=s, texture_px=int(s * 1024)) for n, s in TILES.items()},
               note="Flat squares, origin = centre on the ground, UV 0..1. Every tile border is half a grout joint, so tiles of "
                    "any kind sit side by side without a seam. Base tiles 2 m (slab rows 0.5 m, cobbles ~0.29 m), patterns 4 m."),
          open(os.path.join(OUT, "floor_tiles.json"), "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "FL_FloorTiles.blend"))
if not RENDER:
    sys.exit(0)

# ---------------------------------------------------------------- pictures
for o in tiles.values(): o.hide_render = True
# 1) all tiles from above (orthographic): base tiles on top row, 4 m patterns below
row = []
for k, (name, size) in enumerate(TILES.items()):
    c = tiles[name].copy(); scene.collection.objects.link(c)
    if size == 2.0: c.location = (-6.6 + k * 4.4 + 1.0, 3.4, 0)
    else: c.location = (-6.6 + (k - 4) * 4.4 + 2.0, -1.0, 0)
    c.hide_render = False; row.append(c)
st = rc.Stage(OUT, row, samples=32)
st.ground.hide_render = True
st.cam_data.type = "ORTHO"; st.cam_data.ortho_scale = 18.5
st.cam.location = (2.0, 1.2, 30); st.cam.rotation_euler = (0, 0, 0)
scene.render.resolution_x, scene.render.resolution_y = 2000, 1100
scene.render.filepath = os.path.join(OUT, "fl_tiles_top.png"); bpy.ops.render.render(write_still=True)
st.cam_data.type = "PERSP"
for c in row: c.hide_render = True

# 2) plaza 16 x 16 m: slabs, a cobble cross path, wet patches, two drains, compass centre, flower / wheel / diamond corners
plaza = []
def put(name, cx, cy, rot=0):
    c = tiles[name].copy(); scene.collection.objects.link(c)
    c.location = (cx, cy, 0); c.rotation_euler = (0, 0, rot); c.hide_render = False
    plaza.append(c)
used = set()
big = {(3, 3): "FL_PatternCompass", (0, 0): "FL_PatternFlower", (6, 0): "FL_PatternWheel", (0, 6): "FL_PatternDiamond"}
for (i, j), name in big.items():
    put(name, -8 + i * 2 + 2, -8 + j * 2 + 2)
    used |= {(i + a, j + b) for a in (0, 1) for b in (0, 1)}
wet = {(5, 2), (6, 3), (2, 5), (1, 3)}
drain = {(5, 1), (2, 2)}
for i in range(8):
    for j in range(8):
        if (i, j) in used: continue
        if i in (3, 4) or j in (3, 4): name = "FL_Cobble"
        elif (i, j) in wet: name = "FL_Wet"
        elif (i, j) in drain: name = "FL_Sewer"
        else: name = "FL_Basic"
        put(name, -8 + i * 2 + 1, -8 + j * 2 + 1, rot=((i * 7 + j * 3) % 4) * math.pi / 2 if name in ("FL_Basic", "FL_Wet", "FL_Cobble") else 0)

# map objects from the map objects blend (tent, bench, lamps, fountain, crates) + mannequin
props = {}
with bpy.data.libraries.load(MO_BLEND) as (src, dst):
    dst.objects = [n for n in src.objects if n in ("MO_Tent", "MO_Bench", "MO_StreetLamp", "MO_Fountain", "MO_CrateBarrel", "MO_Popcorn")]
for o in dst.objects:
    if o is None: continue
    scene.collection.objects.link(o); props[o.name] = o
def place(name, x, y, yaw=0.0, copy=False):
    o = props[name]
    if copy:
        o = o.copy(); scene.collection.objects.link(o)
    o.location = (x, y, 0); o.rotation_euler = (0, 0, yaw); return o
extra = [place("MO_Tent", 5.6, 5.4, -math.pi / 4), place("MO_Bench", -3.5, -1.6, 0), place("MO_Fountain", -5.0, 5.2),
         place("MO_CrateBarrel", 6.5, -1.8, 0.4), place("MO_Popcorn", -5.5, -2.6, 0.6),
         place("MO_StreetLamp", -1.6, -1.6), place("MO_StreetLamp", 1.6, 1.6, copy=True), place("MO_StreetLamp", 6.9, -6.9, copy=True),
         place("MO_StreetLamp", -6.9, 6.9 - 13.8, copy=True)]
mat = bpy.data.materials.new("Mannequin"); mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.72, 0.72, 0.78, 1)
men = []
for (mx, my) in ((0.6, -3.6), (-2.2, 0.8)):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.46, depth=1.4, location=(mx, my, 0.9)); b = rc.active(); b.data.materials.append(mat)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.38, location=(mx, my, 1.9)); h = rc.active(); h.data.materials.append(mat)
    men += [b, h]
# warm lamp lights + moonlight
for i, lamp in enumerate(o for o in extra if o.name.startswith("MO_StreetLamp")):
    l = bpy.data.lights.new(f"LampLight{i}", "POINT"); l.energy = 900; l.color = (1.0, 0.55, 0.25); l.shadow_soft_size = 0.3
    lo = bpy.data.objects.new(f"LampLight{i}", l); scene.collection.objects.link(lo); lo.location = lamp.location + Vector((0, 0, 4.2))
st.all = row + plaza + extra + men
vis = plaza + extra + men
for o in vis: o.hide_render = False
st.ground.hide_render = False
st.ground.location.z = -0.02
st.shoot("fl_plaza", vis, 42, 30, res=(1600, 1100), fill=0.95, frame_objs=plaza)
# eye level close-up (camera 1.7 m high) — texel density at play distance
st.cam_data.lens = 32
st.cam.location = (-1.0, -7.4, 1.7)
st.cam.rotation_euler = (Vector((1.2, 0.0, 0.0)) - st.cam.location).to_track_quat("-Z", "Y").to_euler()
for o in st.all: o.hide_render = o not in vis
scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.render.filepath = os.path.join(OUT, "fl_eye_level.png"); bpy.ops.render.render(write_still=True)
print("DONE", flush=True)
