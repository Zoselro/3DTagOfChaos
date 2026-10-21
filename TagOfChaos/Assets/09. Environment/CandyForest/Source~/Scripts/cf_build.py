# CandyForest real-model assets (Request1009Plan.md §28, steps C2~C3).
# Reads the split sets (_split/<Set>.blend from cf_split.py), scales every piece to its real size, cuts doorways,
# decimates and exports one FBX per asset to Models/, plus <asset>.json metadata (door sizes, collider rings, pedestal)
# that CandyForestAssets reads. Meshy models face -Y; with this export Blender (x, y, z) lands in Unity as (-x, z, -y),
# so the front ends up facing Unity +Z. Metadata positions/bounds are written in Unity axes.
# blender -b --factory-startup --python cf_build.py -- <Source~ dir> <Models dir> [asset,asset,...]
import bpy, bmesh, sys, os, json, math, traceback
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]
OUT = argv[1]
ONLY = set(argv[2].split(",")) if len(argv) > 2 and argv[2] else None
SPLIT = os.path.join(SRC, "_split")
os.makedirs(OUT, exist_ok=True)
LOG = open(os.path.join(SPLIT, "build_log.txt"), "a", encoding="utf-8")

DOOR_W, DOOR_H = 7.2, 7.9          # monster doorway (cut); colliders leave 6.6 x 7.6 (plan: >= 6.5 x 7.4)
HOLE_W, HOLE_H = 1.7, 2.9          # cookie-only back hole (cut); colliders leave 1.4 x 2.6 (§28.11.7 H2)


def log(*a):
    LOG.write(" ".join(str(x) for x in a) + "\n"); LOG.flush()


# ---------------------------------------------------------------- helpers

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_piece(setname, idx, name):
    path = os.path.join(SPLIT, setname + ".blend")
    want = "%s_%02d" % (setname, idx)
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n == want]
    o = dst.objects[0]
    bpy.context.scene.collection.objects.link(o)
    o.data = o.data.copy()
    o.name = name
    o.data.name = name
    return o


def verts_world(o):
    m = o.matrix_world
    return [m @ v.co for v in o.data.vertices]


def bbox(o):
    vs = verts_world(o)
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))


def bake(o, m):
    o.data.transform(m)
    o.data.update()


def normalize(o, mode, size, turn=False):
    """Uniform scale so width ('w' = max horizontal) or height ('h') == size; bottom-center to origin (front stays -Y)."""
    lo, hi = bbox(o)
    dims = hi - lo
    s = size / (max(dims.x, dims.y) if mode == "w" else dims.z)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    m = Matrix.Scale(s, 4) @ Matrix.Translation(-c)
    if turn: m = Matrix.Rotation(math.pi, 4, "Z") @ m
    bake(o, m)
    return s


def skirt_top(o, zmax):
    """Dominant height of up-facing area below zmax (the ground ring / floor the model stands on)."""
    hist = {}
    for p in o.data.polygons:
        if p.normal.z < 0.85 or p.center.z > zmax: continue
        k = round(p.center.z / 0.1) * 0.1
        hist[k] = hist.get(k, 0.0) + p.area
    return max(hist.items(), key=lambda kv: kv[1])[0] if hist else 0.0


def drop(o, dz):
    bake(o, Matrix.Translation((0, 0, -dz)))


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    t = tris(o)
    if t <= target: return
    bpy.context.view_layer.objects.active = o
    md = o.modifiers.new("dec", "DECIMATE")
    md.ratio = max(0.002, target / t)
    md.use_collapse_triangulate = True
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=md.name)


def cut(o, test):
    """Delete faces whose center passes test(center)."""
    bm = bmesh.new(); bm.from_mesh(o.data)
    dead = [f for f in bm.faces if test(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(o.data); bm.free(); o.data.update()
    return len(dead)


def door_test(front, back):
    """front/back: (half width, height) or None. Front is -Y (Unity +Z)."""
    def t(c):
        if front and c.y < 0 and abs(c.x) < front[0] and c.z < front[1]: return True
        if back and c.y > 0 and abs(c.x) < back[0] and c.z < back[1]: return True
        return False
    return t


def simple_material(name, rgb, emission=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = 0.9
    return m


def inner_shell(src, name, shrink, target):
    """Dark inside of a hollow shell: a copy with flipped normals, pulled slightly inward."""
    o = src.copy(); o.data = src.data.copy(); o.name = name; o.data.name = name
    bpy.context.scene.collection.objects.link(o)
    decimate(o, target)
    bake(o, Matrix.Diagonal((shrink, shrink, 0.995, 1)))
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.to_mesh(o.data); bm.free()
    o.data.materials.clear(); o.data.materials.append(simple_material("CF_Inner", (0.09, 0.05, 0.08)))
    return o


def floor_disc(name, radius, z=0.02, segs=40):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    ring = [bm.verts.new((radius * math.cos(2 * math.pi * i / segs), radius * math.sin(2 * math.pi * i / segs), z)) for i in range(segs)]
    bm.faces.new(ring)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(simple_material("CF_InnerFloor", (0.18, 0.1, 0.12)))
    return o


def empty(name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    return e


def sector_radii(o, zmax, sectors=36):
    """Max horizontal radius of geometry below zmax per angular sector (degrees from +X, CCW)."""
    r = [0.0] * sectors
    for v in o.data.vertices:
        if v.co.z > zmax: continue
        a = math.atan2(v.co.y, v.co.x) % (2 * math.pi)
        k = int(a / (2 * math.pi) * sectors) % sectors
        r[k] = max(r[k], math.hypot(v.co.x, v.co.y))
    return [round(x, 2) for x in r]


def collision_copy(src, name, zmin, target):
    """Collision mesh for Unity (MeshCollider): the visual mesh without the faces near the ground (height < zmin),
    so floor-level bumps stay walkable (monster climbs only 0.15 m) while everything a player could hide in is solid."""
    o = src.copy(); o.data = src.data.copy(); o.name = name; o.data.name = name
    bpy.context.scene.collection.objects.link(o)
    cut(o, lambda c: c.z < zmin)
    decimate(o, target)
    o.data.materials.clear(); o.data.materials.append(simple_material("CF_Collision", (1.0, 0.0, 1.0)))
    return o


def export(name, objs, meta=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             object_types={"EMPTY", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False, use_mesh_modifiers=True,
                             mesh_smooth_type="FACE", use_tspace=False, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", embed_textures=False, use_custom_props=False)
    info = dict(name=name, tris={o.name: tris(o) for o in objs if o.type == "MESH"})
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for o in objs:
        if o.type != "MESH": continue
        a, b = bbox(o)
        lo = Vector(tuple(min(lo[k], a[k]) for k in range(3))); hi = Vector(tuple(max(hi[k], b[k]) for k in range(3)))
    # Unity axes: x = -x, y = z, z = -y
    info["min"] = [round(-hi.x, 3), round(lo.z, 3), round(-hi.y, 3)]; info["max"] = [round(-lo.x, 3), round(hi.z, 3), round(-lo.y, 3)]
    if meta: info.update(meta)
    json.dump(info, open(os.path.join(OUT, name + ".json"), "w"), indent=1)
    log("EXPORT", name, info["tris"], "size", [round(hi[k] - lo[k], 2) for k in range(3)])
    render(name, objs)


def render(name, objs):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 900; sc.render.resolution_y = 700
    sc.render.image_settings.file_format = "JPEG"
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for o in objs:
        if o.type != "MESH" or o.name.startswith("COL_"): continue
        a, b = bbox(o)
        lo = Vector(tuple(min(lo[k], a[k]) for k in range(3))); hi = Vector(tuple(max(hi[k], b[k]) for k in range(3)))
    c = (lo + hi) / 2; ext = (hi - lo).length
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = ext * 1.05
    # front three-quarter view from -Y (the front)
    cam.rotation_euler = (math.radians(65), 0, math.radians(-30))
    bpy.context.view_layer.update()
    cam.location = c + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * ext * 3
    sc.camera = cam
    hidden = [o for o in sc.objects if o.type == "MESH" and (o not in objs or o.name.startswith("COL_"))]
    for o in hidden: o.hide_render = True
    sc.render.filepath = os.path.join(SPLIT, "out_" + name + ".jpg")
    bpy.ops.render.render(write_still=True)
    for o in hidden: o.hide_render = False


def wanted(name):
    return ONLY is None or name in ONLY


# ---------------------------------------------------------------- simple props

PROPS = {
    "CF_LollipopTree": ("SwirlTree", 0, "h", 9.0, 7000),
    "CF_CottonTree": ("BubblegumTree", 0, "h", 8.0, 7000),
    "CF_PurpleTree": ("Trinkets", 2, "h", 8.5, 7000),
    "CF_Mushrooms": ("Trinkets", 0, "h", 3.0, 10000),
    "CF_RingFence": ("Trinkets", 1, "w", 5.0, 10000),
    "CF_Shop": ("Trinkets", 3, "w", 9.0, 20000),
    "CF_Cottage": ("Cottage", 0, "w", 14.0, 16000),
    "CF_Pond": ("Park", 0, "w", 12.0, 10000),
    "CF_Lamp": ("Park", 1, "h", 5.0, 3000),
    "CF_FenceGate": ("Park", 2, "w", 6.0, 1800),
    "CF_Signpost": ("Park", 3, "h", 3.5, 4000),
    "CF_Bench": ("Park", 4, "w", 3.0, 4000),
    "CF_CandyCane": ("Whimsical", 2, "h", 8.0, 4500),
    "CF_CandyPile": ("Tiles", 3, "w", 5.0, 6000),
    "CF_Arch": ("Tiles", 4, "w", 18.0, 6000),
    "CF_MonsterAltar": ("GateItems", 0, "w", 6.0, 5000),
    "CF_MonsterBox": ("GateItems", 1, "h", 4.0, 5000),
    "CF_Lollipop_Red": ("GateItems", 2, "h", 0.9, 1000),
    "CF_Lollipop_Blue": ("GateItems", 3, "h", 0.9, 1000),
    "CF_Lollipop_Green": ("GateItems", 4, "h", 0.9, 1000),
    "CF_Lollipop_Yellow": ("GateItems", 5, "h", 0.9, 1000),
    "CF_ExitOpen": ("GateItems", 6, "w", 9.0, 4000),
    "CF_ExitClosed": ("GateItems", 7, "w", 9.0, 4000),
}

TILES = {"CF_Tile_Grass": 0, "CF_Tile_Path": 1, "CF_Tile_Lava": 2}


def build_prop(name, spec):
    setname, idx, mode, size, target = spec
    reset()
    o = load_piece(setname, idx, name)
    normalize(o, mode, size)
    decimate(o, target)
    meta = {}
    if name.startswith("CF_Arch"):
        # clearance under the arch along its axis (x = 0 column): lowest geometry above 1 m
        under = [v.co.z for v in o.data.vertices if abs(v.co.x) < 1.0 and v.co.z > 1.0]
        meta["clearance"] = round(min(under), 2) if under else 0
        legs = [abs(v.co.x) for v in o.data.vertices if v.co.z < 1.0]
        meta["inner_half_span"] = round(min(legs), 2) if legs else 0
    objs = [o]
    if not (name in NO_COLLISION or name.startswith("CF_Lollipop_")):
        objs.append(collision_copy(o, "COL_Mesh", 0.25, min(6000, target)))
    export(name, objs, meta)


# walk-over or moving pieces: no mesh collision (pond water, item models, exit doors that sink)
NO_COLLISION = {"CF_Pond", "CF_ExitOpen", "CF_ExitClosed"}


def ground_block(o, fit, size, bump_max, target):
    """Floor block (2026-10-21, §28.16): one uniform scale (the model's own proportions), the walking face 4 cm above
    the floor collider, everything more than 0.6 m below the face cut away (it is buried), and only what rises above
    the face lowered to bump_max (no collider on these — nothing tall enough to hide in or walk through)."""
    lo, hi = bbox(o)
    d = hi - lo
    s = size / (d.y if fit == "y" else max(d.x, d.y))
    bake(o, Matrix.Scale(s, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    face = skirt_top(o, 1e6)
    rise = max(v.co.z for v in o.data.vertices) - face
    k = min(1.0, bump_max / rise) if rise > 0 else 1.0
    for v in o.data.vertices:
        if v.co.z > face: v.co.z = face + (v.co.z - face) * k
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, face - 0.6), plane_no=(0, 0, 1), clear_inner=True)
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    drop(o, face - 0.04)
    decimate(o, target)
    log("GROUND", o.name, "scale", round(s, 2), "bump", round(rise, 2), "->", round(rise * k, 2), "tris", tris(o))


def build_moss_tile(name):
    """New green floor block (2026-10-20): 10 x 9 m, the grass top 4 cm above the floor collider, bushes kept low."""
    reset()
    o = load_piece("GreenFloor", 0, name)
    ground_block(o, "max", 10.0, 0.8, 3500)
    export(name, [o])


TILE_SPEC = {  # bump limit, triangle target
    "CF_Tile_Grass": (0.4, 2500),
    "CF_Tile_Path": (0.6, 3000),  # candy balls are fused to the block — kept as low gumdrops
    "CF_Tile_Lava": (0.4, 2500),
}


def build_tile(name, idx):
    reset()
    o = load_piece("Tiles", idx, name)
    bump, target = TILE_SPEC[name]
    ground_block(o, "y", 10.0, bump, target)  # 10 m across the road, length from the model (about 5.5~5.9 m)
    export(name, [o])


# ---------------------------------------------------------------- enterable shells (edge groups, §28.11.5~7)

SHELLS = {
    "CF_MonsterHouse": ("Whimsical", 1, 16.0, 18000),
    "CF_Tent": ("Whimsical", 0, 14.0, 14000),
}


def build_shell(name, spec):
    setname, idx, width, target = spec
    reset()
    o = load_piece(setname, idx, "Shell")
    normalize(o, "w", width)
    drop(o, skirt_top(o, 4.0))
    removed = cut(o, door_test((DOOR_W / 2, DOOR_H), (HOLE_W / 2, HOLE_H)))
    decimate(o, target)
    inner = inner_shell(o, "Inner", 0.97, 5000)
    col = collision_copy(o, "COL_Shell", 0.25, 8000)
    radii = sector_radii(o, 3.0)
    wall = round(sorted(radii)[len(radii) // 4] * 0.9, 2)      # collider ring inside the visual wall
    flo = floor_disc("Floor", wall + 0.3)
    meta = dict(kind="shell", ring_radius=wall, door=[6.6, 7.6], hole=[1.4, 2.6], faces_cut=removed)
    export(name, [o, inner, flo, col], meta)


# ---------------------------------------------------------------- material buildings (exterior + interior, §28.4)

BUILDINGS = {
    # quadrant: exterior (MatBuildings idx), interior set/idx    (§28.11 Q2·F3)
    "CF_Bld_Q1": (0, "Interior1", 0),     # cherry cupcake + heart room (red)
    "CF_Bld_Q2": (2, "Interior234", 1),   # gumball jar + purple colorful (blue)
    "CF_Bld_Q3": (1, "Interior234", 0),   # purple tree house + purple pink (green)
    "CF_Bld_Q4": (3, "Interior234", 2),   # candy castle + brown gold (yellow)
}
EXT_W, INT_W = 23.0, 17.0


def find_pedestal(o, cell=0.25, reach=3.5, seed_r=2.0, min_h=0.45, rel=0.6):
    """Pedestal = the raised lump connected to the tallest point near the room center (height map + flood fill).
    Averaging every high vertex near the center mixed in nearby decor and missed low pedestals (10-21)."""
    grid = {}
    for v in o.data.vertices:
        if math.hypot(v.co.x, v.co.y) > reach + 1.0: continue
        k = (math.floor(v.co.x / cell), math.floor(v.co.y / cell))
        grid[k] = max(grid.get(k, -1.0), v.co.z)
    near = [k for k, h in grid.items() if h > min_h and math.hypot((k[0] + 0.5) * cell, (k[1] + 0.5) * cell) < seed_r]
    if not near:
        return 0.0, 0.0, 1.0, 1.0
    seed = max(near, key=lambda k: grid[k])
    sx, sy = (seed[0] + 0.5) * cell, (seed[1] + 0.5) * cell
    level = max(min_h, grid[seed] * rel)   # the pedestal body, not the low platform ring around it
    filled, todo = {seed}, [seed]
    while todo:
        cx, cy = todo.pop()
        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
            if (nx, ny) in filled or grid.get((nx, ny), -1.0) <= level: continue
            if math.hypot((nx + 0.5) * cell - sx, (ny + 0.5) * cell - sy) > reach: continue
            filled.add((nx, ny)); todo.append((nx, ny))
    xs = [(k[0] + 0.5) * cell for k in filled]; ys = [(k[1] + 0.5) * cell for k in filled]
    px, py = sum(xs) / len(xs), sum(ys) / len(ys)
    pr = max(math.hypot(x - px, y - py) for x, y in zip(xs, ys)) + cell / 2
    top = max(grid[k] for k in filled)
    log("pedestal", round(px, 2), round(py, 2), "top", round(top, 2), "r", round(pr, 2), "cells", len(filled), "seed", round(grid[seed], 2))
    return px, py, top, min(pr, 3.0)


def build_building(name, spec):
    ext_idx, int_set, int_idx = spec
    reset()
    ext = load_piece("MatBuildings", ext_idx, "Exterior")
    normalize(ext, "w", EXT_W)
    drop(ext, skirt_top(ext, 6.0))
    inn = load_piece(int_set, int_idx, "Interior")
    normalize(inn, "w", INT_W)
    floor = skirt_top(inn, 3.0)
    drop(inn, floor - 0.02)
    ilo, ihi = bbox(inn)
    px, py, top, pr = find_pedestal(inn)
    keep = max(abs(py) + pr + 0.5, 3.0)
    # doorways: through the exterior shell anywhere on that side, through the interior rim only (keep the floor + pedestal)
    n_ext = cut(ext, door_test((DOOR_W / 2, DOOR_H), (DOOR_W / 2, DOOR_H)))
    n_int = cut(inn, lambda c: abs(c.x) < DOOR_W / 2 and abs(c.y) > keep and 0.25 < c.z < DOOR_H + 2)
    decimate(ext, 22000)
    decimate(inn, 36000)
    inner = inner_shell(ext, "Inner", 0.97, 6000)
    col_ext = collision_copy(ext, "COL_Exterior", 0.25, 8000)
    # inside the room the wall ring + pedestal collide; exterior pieces poking into the room (tree trunk ...) would leave cookie-only gaps
    cut(col_ext, lambda c: ilo.x + 0.5 < c.x < ihi.x - 0.5 and ilo.y + 0.5 < c.y < ihi.y - 0.5)
    col_int = collision_copy(inn, "COL_Interior", 1.0, 12000)   # room floor rails/bumps (< 1 m) stay walk-through
    radii = sector_radii(ext, 3.0)
    rp = empty("Repair_Point", (px, py, top + 0.15))
    # nearest decor (taller than 1 m, outside the pedestal) per 5-degree sector in Unity axes (x = -x, z = -y):
    # the room's wall collider ring is thickened inward to this radius, so nobody can stand inside / behind decor
    decor = [999.0] * 72
    for v in col_int.data.vertices:
        ux, uz = -v.co.x, -v.co.y
        r = math.hypot(ux, uz)
        if r < pr + 1.2: continue
        k = int((math.degrees(math.atan2(uz, ux)) % 360.0) / 5.0) % 72
        decor[k] = min(decor[k], r)
    meta = dict(kind="building", outer_radii=radii, inner_half=[round((ihi.x - ilo.x) / 2, 2), round((ihi.y - ilo.y) / 2, 2)],
                pedestal=[round(-px, 2), round(-py, 2), round(top, 2), round(pr, 2)], door=[6.6, 7.6],
                faces_cut=[n_ext, n_int], interior_floor=round(floor, 2), decor_radii=[round(x, 2) for x in decor])
    export(name, [ext, inn, inner, rp, col_ext, col_int], meta)


# ---------------------------------------------------------------- main

jobs = []
for k, v in PROPS.items():
    if wanted(k): jobs.append((k, lambda k=k, v=v: build_prop(k, v)))
for k, v in TILES.items():
    if wanted(k): jobs.append((k, lambda k=k, v=v: build_tile(k, v)))
if wanted("CF_Tile_Moss"): jobs.append(("CF_Tile_Moss", lambda: build_moss_tile("CF_Tile_Moss")))
for k, v in SHELLS.items():
    if wanted(k): jobs.append((k, lambda k=k, v=v: build_shell(k, v)))
for k, v in BUILDINGS.items():
    if wanted(k): jobs.append((k, lambda k=k, v=v: build_building(k, v)))
for name, job in jobs:
    try:
        job(); log("OK", name)
    except Exception:
        log("FAIL", name, traceback.format_exc())
log("DONE", len(jobs))
LOG.close()
