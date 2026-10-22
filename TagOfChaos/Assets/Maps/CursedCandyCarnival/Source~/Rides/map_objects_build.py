# Cursed Candy Carnival map objects (2026-10-22), from the object guide sheet, sized to our characters
# (cookie ~2 m tall, monster ~3 m; props 3~6 m like the guide's 기본 크기 예시):
#   materials  MO_CherryJuice, MO_CottonCandy, MO_Popcorn, MO_Candy
#   decoration MO_Tent, MO_Bench, MO_StreetLamp, MO_CrateBarrel, MO_Fountain, MO_Fence
#   monster room MO_MonsterRoom (round wall, doors at 12/3/6/9) + MO_MonsterRoomDoor
#   exit gate  MO_ExitGate (pillars + arch) + MO_ExitGateDoorL / MO_ExitGateDoorR (origin on the hinge, swing open about Z)
#              + MO_ExitLever (origin on the lever pivot)
# Full-detail geometry, world-scale tiling 1024 px textures (box / cylinder / disc UVs on one "UVMap"), no decimation.
# Each object is exported on its own (origin = centre of its footprint on the ground) — FBX + a lineup picture with a
# 2 m mannequin, per-object pictures, the exit gate closed / open.
# python map_objects_build.py -- <tex dir> <out dir> [render 1|0]
import bpy, bmesh, sys, os, math, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ride_common as rc
from ride_common import box, cyl, sphere, torus, bar, tube, plane, medallion, ring_of, mesh_obj

argv = sys.argv[sys.argv.index("--") + 1:]
TEX, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
RENDER = len(argv) < 3 or argv[2] != "0"
os.makedirs(OUT, exist_ok=True)

CHARACTER_H = 2.0
rc.reset(TEX)
M = rc.material
M("stone", "MO_PurpleStone", "GD_PurpleStone", rough=0.85, uv=2.0)
M("purple", "MO_PurpleMetal", "GD_PurpleMetal", metallic=0.3, rough=0.45, uv=2.0)
M("red", "MO_RedMetal", "GD_RedMetal", metallic=0.3, rough=0.4, uv=2.0)
M("gold", "MO_Gold", "GD_Gold", metallic=0.9, rough=0.3, uv=1.0)
M("iron", "MO_Iron", "MO_Iron", metallic=0.6, rough=0.45, uv=1.0)
M("wood", "MO_Wood", "VK_BarrelWood", rough=0.75, uv=1.2)
M("hullwood", "MO_RedWood", "VK_HullWood", rough=0.7, uv=1.5)
M("tent", "MO_TentStripes", "MO_TentStripes", rough=0.85)
M("stripes", "MO_RedWhiteStripes", "MO_RedWhiteStripes", rough=0.8)
M("fstone", "MO_FountainStone", "MO_FountainStone", rough=0.8, uv=1.5)
M("water", "MO_Water", "MO_Water", rough=0.05, uv=2.0, emission=(0.1, 0.45, 0.6), strength=0.6)
M("cotton", "MO_CottonCandyFluff", "MO_CottonCandy", rough=0.9, uv=1.0)
M("popcorn", "MO_PopcornPile", "MO_Popcorn", rough=0.8, uv=0.8)
M("juice", "MO_CherryJuiceLiquid", "MO_CherryJuice", rough=0.1, uv=1.0, emission=(0.9, 0.05, 0.12), strength=3.0)
M("sign", "MO_SignBoard", "MO_SignBoard", rough=0.6)
M("emblem", "MO_SkullEmblem", "VK_SkullEmblem", rough=0.5)
M("banner", "MO_Banner", "VK_Banner", rough=0.85)
M("flag", "MO_Flag", "GD_Flag", rough=0.8)
M("bone", "MO_Bone", "VK_Bone", rough=0.55, uv=1.0)
M("seat", "MO_RedPaint", "GD_RedSeat", rough=0.5, uv=0.8)
M("glass", "MO_LampGlass", None, (1.0, 0.65, 0.3), rough=0.2, emission=(1.0, 0.55, 0.18), strength=10.0)
M("flame", "MO_Flame", None, (1.0, 0.75, 0.3), rough=0.3, emission=(1.0, 0.55, 0.1), strength=25.0)
M("glow", "MO_RedGlow", None, (1.0, 0.2, 0.25), rough=0.3, emission=(1.0, 0.12, 0.18), strength=8.0)
M("candyR", "MO_CandyRed", None, (0.95, 0.1, 0.2), rough=0.15, emission=(0.6, 0.02, 0.06), strength=1.0)
M("candyY", "MO_CandyYellow", None, (1.0, 0.8, 0.1), rough=0.15, emission=(0.6, 0.4, 0.02), strength=1.0)
M("candyG", "MO_CandyGreen", None, (0.2, 0.85, 0.3), rough=0.15, emission=(0.05, 0.4, 0.1), strength=1.0)
M("candyB", "MO_CandyBlue", None, (0.25, 0.45, 1.0), rough=0.15, emission=(0.05, 0.15, 0.6), strength=1.0)
M("dark", "MO_Shadow", None, (0.03, 0.02, 0.04), rough=0.9)
M("dummy", "MO_ScaleDummy", None, (0.7, 0.72, 0.78), rough=0.6)


def cylw(part, r, depth, loc, mat, verts=32, r2=None, rep=8.0, vsize=4.0, rot=(0, 0, 0)):
    """Cylinder / cone whose texture wraps around (stripes)."""
    o = cyl(part, r, depth, loc, mat, verts=verts, r2=r2, rot=rot, bevel=0.0, smooth=True)
    o["rc_uv"] = "cyl"; o["rc_rep"] = rep; o["rc_vsize"] = vsize
    return o


def lantern(part, base, post_h, s=1.0, post=True):
    x, y, z = base
    if post and post_h > 0:
        cyl(part, 0.09 * s, post_h, (x, y, z + post_h / 2), "iron", verts=10, bevel=0.0, smooth=True)
    z += post_h
    box(part, (0.5 * s, 0.5 * s, 0.1 * s), (x, y, z + 0.05 * s), "gold", bevel=0.02)
    box(part, (0.38 * s, 0.38 * s, 0.55 * s), (x, y, z + 0.38 * s), "glass", bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(part, (0.06 * s, 0.06 * s, 0.6 * s), (x + sx * 0.2 * s, y + sy * 0.2 * s, z + 0.38 * s), "iron", bevel=0.0)
    cyl(part, 0.4 * s, 0.35 * s, (x, y, z + 0.83 * s), "purple", verts=4, r2=0.0, rot=(0, 0, math.pi / 4), bevel=0.0)
    sphere(part, 0.08 * s, (x, y, z + 1.05 * s), "gold", seg=8)


def pennant(part, base, height, yaw_deg, mat="flag"):
    x, y, z = base
    cyl(part, 0.05, height, (x, y, z + height / 2), "gold", verts=8, bevel=0.0, smooth=True)
    sphere(part, 0.1, (x, y, z + height + 0.06), "gold", seg=8)
    a = math.radians(yaw_deg); d = Vector((math.cos(a), math.sin(a), 0))
    c = Vector((x, y, z + height - 0.35)) + d * 0.5
    plane(part, 0.95, 0.55, c, (math.pi / 2, 0, a), mat)


def wheel(part, center, r, axis_y=True):
    rot = (math.pi / 2, 0, 0) if axis_y else (0, math.pi / 2, 0)
    torus(part, r, 0.07, center, "gold", rot=rot, seg=24)
    cyl(part, r * 0.95, 0.12, center, "purple", verts=24, rot=rot, bevel=0.0)
    cyl(part, r * 0.2, 0.2, center, "gold", verts=12, rot=rot, bevel=0.0)
    for k in range(6):
        a = k * math.pi / 3
        off = Vector((math.cos(a), 0, math.sin(a))) if axis_y else Vector((0, math.cos(a), math.sin(a)))
        bar(part, center, Vector(center) + off * r * 0.92, 0.06, "gold", round_=True)


# ---------------------------------------------------------------- materials (재료 4종)
def cherry_juice():
    P = "MO_CherryJuice"
    cyl(P, 1.0, 0.25, (0, 0, 0.125), "purple", verts=24, bevel=0.03)
    cyl(P, 1.05, 0.08, (0, 0, 0.27), "gold", verts=24, bevel=0.01)
    for a, p in ring_of(3, 0.75, 90):                                     # stand legs
        bar(P, (p.x, p.y, 0.3), (p.x * 0.75, p.y * 0.75, 1.15), 0.14, "gold", round_=True)
        sphere(P, 0.1, (p.x, p.y, 0.32), "gold", seg=8)
    torus(P, 0.62, 0.07, (0, 0, 1.15), "gold", seg=28)                    # cradle ring
    sphere(P, 0.8, (0, 0, 1.75), "juice", seg=28)                         # flask body (glowing juice)
    torus(P, 0.8, 0.05, (0, 0, 1.75), "gold", seg=32)
    cyl(P, 0.24, 0.65, (0, 0, 2.75), "juice", verts=16, bevel=0.0, smooth=True)   # neck
    torus(P, 0.26, 0.05, (0, 0, 2.5), "gold", seg=16)
    cyl(P, 0.3, 0.25, (0, 0, 3.15), "gold", verts=16, bevel=0.02)          # cap
    sphere(P, 0.18, (0, 0, 3.42), "gold", seg=12)
    cyl(P, 0.08, 0.45, (0, 0, 3.75), "gold", verts=8, r2=0.0, bevel=0.0)
    for s in (-1, 1):                                                      # little wings on the cap
        bar(P, (s * 0.25, 0, 3.2), (s * 0.7, 0, 3.55), 0.08, "gold", round_=True)
        sphere(P, 0.09, (s * 0.72, 0, 3.57), "gold", seg=8)
    for p in ((0.35, -0.6, 1.9), (-0.3, -0.65, 1.55), (0.1, -0.72, 2.15), (-0.45, -0.55, 2.0)):
        sphere(P, 0.08, p, "glass", seg=8)                                 # bubbles
    medallion(P, (0, -1.02, 0.15), -math.pi / 2, 0.12, "emblem")


def cotton_candy():
    P = "MO_CottonCandy"
    box(P, (2.1, 1.2, 0.9), (0, 0, 1.05), "red", bevel=0.08)               # cart body
    box(P, (2.25, 1.32, 0.12), (0, 0, 1.55), "gold", bevel=0.03)
    box(P, (2.2, 1.3, 0.1), (0, 0, 0.6), "gold", bevel=0.03)
    plane(P, 1.6, 0.6, (0, -0.62, 1.05), (math.pi / 2, 0, 0), "sign")      # front sign
    for x in (-0.75, 0.75):
        for y in (-0.62, 0.62):
            wheel(P, (x, y, 0.42), 0.4)
    bar(P, (1.05, 0, 1.3), (1.7, 0, 1.45), 0.08, "gold", round_=True)       # push handle
    bar(P, (1.7, -0.4, 1.45), (1.7, 0.4, 1.45), 0.1, "gold", round_=True)
    cylw(P, 0.75, 0.55, (0, 0, 1.88), "stripes", verts=28, rep=6, vsize=1.0)   # machine bowl
    torus(P, 0.75, 0.06, (0, 0, 2.15), "gold", seg=28)
    cyl(P, 0.12, 0.6, (0, 0, 2.3), "gold", verts=10, bevel=0.0)             # spindle
    for p, r in (((0, 0, 2.85), 0.75), ((0.45, 0.2, 2.65), 0.5), ((-0.45, -0.15, 2.7), 0.55), ((0.15, -0.4, 3.1), 0.5),
                 ((-0.2, 0.35, 3.15), 0.5), ((0.05, 0.05, 3.45), 0.45), ((0.5, -0.25, 3.0), 0.35)):
        sphere(P, r, p, "cotton", seg=18)                                  # pink cotton cloud
    for x, y in ((-0.85, -0.45), (0.85, -0.45), (-0.85, 0.45), (0.85, 0.45)):
        lantern(P, (x, y, 1.6), 0.0, 0.5)


def popcorn():
    P = "MO_Popcorn"
    box(P, (1.9, 1.3, 1.0), (0, 0, 1.0), "red", bevel=0.08)
    box(P, (2.05, 1.42, 0.12), (0, 0, 1.55), "gold", bevel=0.03)
    plane(P, 1.6, 0.65, (0, -0.67, 1.0), (math.pi / 2, 0, 0), "sign")
    for x in (-0.65, 0.65):
        wheel(P, (x, -0.72, 0.5), 0.48)
        wheel(P, (x, 0.72, 0.5), 0.48)
    # glass case: gold frame + popcorn pile (no transparent glass needed)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(P, (0.09, 0.09, 1.2), (sx * 0.85, sy * 0.58, 2.2), "gold", bevel=0.01)
    box(P, (1.8, 1.25, 0.1), (0, 0, 2.82), "gold", bevel=0.02)
    for p, r in (((0, 0, 1.8), 0.6), ((0.45, 0.2, 1.75), 0.45), ((-0.45, -0.1, 1.75), 0.45), ((0.1, -0.25, 2.1), 0.42), ((-0.2, 0.25, 2.15), 0.35)):
        sphere(P, r, p, "popcorn", seg=16, scale=(1.1, 0.95, 0.75))
    cylw(P, 1.45, 0.9, (0, 0, 3.32), "stripes", verts=4, r2=0.05, rep=8, vsize=1.0, rot=(0, 0, math.pi / 4))  # striped roof
    for a, p in ring_of(4, 1.0, 45):
        sphere(P, 0.1, (p.x, p.y, 2.92), "glass", seg=8)
    sphere(P, 0.14, (0, 0, 3.82), "gold", seg=10)
    pennant(P, (0, 0, 3.85), 1.0, 0, "flag")
    plane(P, 1.5, 0.55, (0, -0.65, 3.12), (math.pi / 2 + 0.35, 0, 0), "sign")       # roof sign
    bar(P, (-0.95, 0, 1.3), (-1.6, 0, 1.4), 0.08, "gold", round_=True)
    bar(P, (-1.6, -0.45, 1.4), (-1.6, 0.45, 1.4), 0.1, "gold", round_=True)


def candy():
    P = "MO_Candy"
    for a, p in ring_of(3, 0.85, 90):
        bar(P, (p.x * 1.15, p.y * 1.15, 0.05), (p.x * 0.7, p.y * 0.7, 0.9), 0.16, "iron", round_=True)
        sphere(P, 0.14, (p.x * 1.15, p.y * 1.15, 0.08), "gold", seg=8)
    sphere(P, 1.05, (0, 0, 1.35), "purple", seg=28, scale=(1, 1, 0.8))      # cauldron belly
    cyl(P, 0.98, 0.35, (0, 0, 1.95), "purple", verts=28, bevel=0.03)
    torus(P, 1.0, 0.1, (0, 0, 2.12), "gold", seg=32)
    cyl(P, 0.9, 0.05, (0, 0, 2.05), "dark", verts=28, bevel=0.0)
    colors = ("candyR", "candyY", "candyG", "candyB")
    for i, (a, p) in enumerate(ring_of(14, 0.6, 0)):                         # candies heaped on top
        sphere(P, 0.17, (p.x, p.y, 2.18 + 0.05 * (i % 3)), colors[i % 4], seg=12)
    for i, (a, p) in enumerate(ring_of(7, 0.3, 20)):
        sphere(P, 0.18, (p.x, p.y, 2.4), colors[(i + 1) % 4], seg=12)
    sphere(P, 0.2, (0, 0, 2.6), "candyR", seg=12)
    for i, (a, p) in enumerate(ring_of(5, 0.75, 10)):                        # wrapped candies (twisted ends)
        q = Vector((p.x, p.y, 2.3))
        sphere(P, 0.14, q, colors[(i + 2) % 4], seg=10, scale=(1.4, 1, 1))
    torus(P, 1.05, 0.06, (0, 0, 1.35), "gold", seg=32)
    medallion(P, (0, -1.06, 1.35), -math.pi / 2, 0.32, "emblem")
    # crank pipe + lantern pole at the back
    bar(P, (0.6, 0.75, 1.9), (0.9, 1.1, 2.8), 0.16, "gold", round_=True)
    sphere(P, 0.22, (0.92, 1.12, 2.85), "red", seg=12)
    cyl(P, 0.1, 3.2, (-0.95, 0.8, 1.6), "iron", verts=10, bevel=0.0, smooth=True)
    lantern(P, (-0.95, 0.8, 3.2), 0.0, 0.8)


# ---------------------------------------------------------------- decoration
# Hide-in tent (2026-10-22 §34): cookies (capsule Ø0.92 m, body 1.9 m wide, 2.3 m tall) and the monster (capsule Ø0.63 m)
# walk in through a front (-Y) and a back (+Y) doorway. Doors are torn canvas: side flaps tied back to the posts with ragged
# hems and holes, frayed strips hanging from the lintel above head height (no collision on cloth). Markers for the shelter
# prefab (same rules as the V4 shelters): COL_* boxes = walls / props, Light_Inside / Light_Exit_* = lights, Zone_Inside = IndoorZone.
TENT_R, TENT_WALL, TENT_DOOR_W, TENT_DOOR_H = 4.2, 3.2, 2.8, 2.9
TENT_DOOR_HALF = math.asin(TENT_DOOR_W / 2 / TENT_R)
TENT_MARKERS = []


def _uv_mesh(part, name, verts, faces, uvs, mat, thickness=0.025):
    o = mesh_obj(part, name, verts, faces, mat, smooth=True)
    bm = bmesh.new(); bm.from_mesh(o.data)
    lay = bm.loops.layers.uv.new("UVMap")
    for f, fuv in zip(bm.faces, uvs):
        for l, uv in zip(f.loops, fuv): l[lay].uv = uv
    bm.to_mesh(o.data); bm.free()
    o["rc_uv"] = "keep"
    sol = o.modifiers.new("solid", "SOLIDIFY"); sol.thickness = thickness; sol.offset = 0
    return o


def _tent_wall(P):
    """Wall ring with the two door gaps (lintel above each door), canvas both sides (solidify)."""
    door = [(-math.pi / 2, TENT_DOOR_HALF), (math.pi / 2, TENT_DOOR_HALF)]
    angs = sorted(set([i * 2 * math.pi / 72 - math.pi for i in range(72)] +
                      [c + s * h for c, h in door for s in (-1, 1)]))
    angs.append(angs[0] + 2 * math.pi)
    zs = (0.0, TENT_DOOR_H, TENT_WALL)
    verts, faces = [], []
    for a in angs:
        for z in zs: verts.append((TENT_R * math.cos(a), TENT_R * math.sin(a), z))
    for i in range(len(angs) - 1):
        mid = (angs[i] + angs[i + 1]) / 2
        in_door = any(abs(math.atan2(math.sin(mid - c), math.cos(mid - c))) < h for c, h in door)
        b = i * 3
        if not in_door: faces.append((b, b + 3, b + 4, b + 1))
        faces.append((b + 1, b + 4, b + 5, b + 2))
    o = mesh_obj(P, "TentWall", verts, faces, "tent", smooth=True)
    o["rc_uv"] = "cyl"; o["rc_rep"] = 8; o["rc_vsize"] = TENT_WALL
    sol = o.modifiers.new("solid", "SOLIDIFY"); sol.thickness = 0.08; sol.offset = 0


def _torn_flap(P, rng, door_y, side, name):
    """Side flap hanging from the lintel corner, gathered by a rope at 1.35 m against the post, ragged hem, a few rips."""
    post_x = side * TENT_DOOR_W / 2
    nu, nv = 9, 14
    hem = [rng.uniform(0.15, 0.75) for _ in range(nu + 1)]
    hem = [min(h, 0.35 + 0.4 * (1 - k / nu)) if k % 2 else h + 0.08 for k, h in enumerate(hem)]   # saw-tooth tears
    def width(v):
        return 1.05 + (0.24 - 1.05) * min(1, v / 0.52) if v < 0.52 else 0.24 + (v - 0.52) / 0.48 * 0.3
    out = 1 if door_y > 0 else -1
    verts, uvs_v = [], []
    for j in range(nv + 1):
        v = j / nv
        for k in range(nu + 1):
            u = k / nu
            z_bot = hem[k]
            z = TENT_DOOR_H - v * (TENT_DOOR_H - z_bot)
            x = post_x - side * (u * width(v))
            y = door_y + out * (0.12 + 0.07 * math.sin(u * 9.0 + v * 5.0) + 0.05 * v)
            verts.append((x, y, z)); uvs_v.append((0.5 * u + (0.5 if side > 0 else 0.0), 1.0 - v * 0.9))
    faces, uvs = [], []
    holes = {(rng.randrange(2, nu - 1), rng.randrange(2, nv - 2)) for _ in range(5)}
    for j in range(nv):
        for k in range(nu):
            if (k, j) in holes: continue
            a = j * (nu + 1) + k
            f = (a, a + 1, a + nu + 2, a + nu + 1)
            faces.append(f); uvs.append([uvs_v[i] for i in f])
    _uv_mesh(P, name, verts, faces, uvs, "tent")
    # rope tie + brass ring on the post
    tz = TENT_DOOR_H - 0.52 * (TENT_DOOR_H - 0.45)
    torus(P, 0.17, 0.035, (post_x - side * 0.18, door_y + out * 0.14, tz), "wood", rot=(math.pi / 2, 0, 0), seg=16)
    bar(P, (post_x - side * 0.05, door_y + out * 0.1, tz), (post_x - side * 0.05, door_y + out * 0.1, tz - 0.55), 0.05, "wood", round_=True)


def _torn_strips(P, rng, door_y, name):
    """Frayed strips left of the torn door curtain, hanging from the lintel (bottom ends at 2.35~2.65 m, above heads)."""
    out = 1 if door_y > 0 else -1
    x = -TENT_DOOR_W / 2 + 1.0
    k = 0
    while x < TENT_DOOR_W / 2 - 1.0:
        w = rng.uniform(0.14, 0.26)
        length = rng.uniform(0.25, 0.55)
        n = 5
        verts, uvs_v = [], []
        for j in range(n + 1):
            v = j / n
            taper = 1 - 0.65 * v
            for s_ in (-1, 1):
                verts.append((x + s_ * w / 2 * taper + 0.04 * math.sin(v * 3 + k), door_y + out * (0.1 + 0.05 * v),
                              TENT_DOOR_H - v * length))
                uvs_v.append((0.3 + 0.1 * (s_ + 1) / 2 + 0.07 * k, 1 - v * 0.2))
        tip = len(verts); verts.append((x + rng.uniform(-0.04, 0.04), door_y + out * 0.16, TENT_DOOR_H - length - rng.uniform(0.06, 0.15)))
        uvs_v.append((0.35 + 0.07 * k, 0.75))
        faces = [(2 * j, 2 * j + 1, 2 * j + 3, 2 * j + 2) for j in range(n)] + [(2 * n, 2 * n + 1, tip)]
        _uv_mesh(P, f"{name}_{k}", verts, faces, [[uvs_v[i] for i in f] for f in faces], "tent", thickness=0.02)
        x += w + rng.uniform(0.02, 0.1); k += 1


def tent():
    P = "MO_Tent"
    import random
    rng = random.Random(34)
    _tent_wall(P)
    torus(P, TENT_R + 0.03, 0.08, (0, 0, TENT_WALL), "gold", seg=72)
    cylw(P, TENT_R + 0.45, 3.9, (0, 0, TENT_WALL + 1.95), "tent", verts=48, r2=0.14, rep=8, vsize=3.9)        # roof (its base = ceiling)
    for a_, p in ring_of(20, TENT_R + 0.4, 0):                                                                  # scalloped valance
        cyl(P, 0.38, 0.5, (p.x, p.y, TENT_WALL - 0.22), "red", verts=4, r2=0.0, rot=(math.pi, 0, a_ + math.pi / 4), bevel=0.0)
        sphere(P, 0.08, (p.x * 1.01, p.y * 1.01, TENT_WALL + 0.02), "glass", seg=8)
    cyl(P, 0.07, 1.3, (0, 0, TENT_WALL + 4.4), "gold", verts=8, bevel=0.0)
    sphere(P, 0.17, (0, 0, TENT_WALL + 5.1), "gold", seg=10)
    pennant(P, (0, 0, TENT_WALL + 3.8), 1.5, 0, "flag")
    cyl(P, TENT_R - 0.05, 0.05, (0, 0, 0.025), "wood", verts=48, bevel=0.0)                                    # plank floor (flush)
    for door_y in (-TENT_R, TENT_R):
        yy = door_y * math.cos(TENT_DOOR_HALF)
        for s_ in (-1, 1):
            bar(P, (s_ * TENT_DOOR_W / 2, yy, 0.0), (s_ * TENT_DOOR_W / 2, yy, TENT_DOOR_H + 0.15), 0.16, "gold", round_=True)
        bar(P, (-TENT_DOOR_W / 2 - 0.1, yy, TENT_DOOR_H + 0.05), (TENT_DOOR_W / 2 + 0.1, yy, TENT_DOOR_H + 0.05), 0.14, "gold", round_=True)
        plane(P, 1.6, 0.42, (0, yy + math.copysign(0.14, door_y), TENT_DOOR_H + 0.4), (math.pi / 2, 0, 0 if door_y < 0 else math.pi), "sign")
        side_name = "Front" if door_y < 0 else "Back"
        for s_ in (-1, 1):
            _torn_flap(P, rng, yy, s_, f"Flap{side_name}{'L' if s_ < 0 else 'R'}")
        _torn_strips(P, rng, yy, f"Strip{side_name}")
    # hiding clutter along the side walls (doorway path x in +-1.6 stays clear)
    for (x, y, sz) in ((2.9, 0.9, 1.0), (2.9, -0.2, 0.9), (2.55, 1.6, 0.8), (3.0, 0.35, 0.7)):
        box(P, (sz, sz, sz), (x, y, sz / 2 + (1.0 if sz == 0.7 else 0)), "wood", bevel=0.04)
    for (x, y) in ((-2.9, 0.6), (-2.85, -0.5), (-2.3, 1.5)):
        cyl(P, 0.45, 1.05, (x, y, 0.53), "hullwood", verts=16, bevel=0.05, smooth=True)
        for zz in (0.18, 0.88): cyl(P, 0.47, 0.06, (x, y, zz), "iron", verts=16, bevel=0.0)
    for a_, p in ring_of(6, TENT_R + 1.0, 30):                                                                   # stakes + ropes
        cyl(P, 0.06, 0.4, (p.x, p.y, 0.2), "wood", verts=6, bevel=0.0)
        bar(P, (p.x, p.y, 0.35), (p.x * 0.8, p.y * 0.8, TENT_WALL - 0.1), 0.04, "wood", round_=True)
    # lantern hanging from the centre (bottom at 2.6 m)
    cyl(P, 0.02, 0.5, (0, 0, TENT_WALL - 0.05), "iron", verts=6, bevel=0.0)
    lantern(P, (0, 0, 2.55), 0.0, 0.9, post=False)


def tent_markers(tent_obj):
    """COL_ boxes (walls in arcs between the doors, props), lights and the indoor zone, parented to the tent."""
    def marker(name, loc, size=None, rot_z=0.0, empty=False):
        if empty:
            o = bpy.data.objects.new(name, None); o.empty_display_size = 0.3
            rc.scene.collection.objects.link(o)
        else:
            bpy.ops.mesh.primitive_cube_add(size=1); o = rc.active(); o.name = name; o.data.name = name
            o.scale = size; bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        o.location = loc; o.rotation_euler = (0, 0, rot_z)
        if empty and size: o.scale = size
        o.parent = tent_obj; o.hide_render = True
        TENT_MARKERS.append(o)
        return o
    k = 0
    for start, end in ((-math.pi / 2 + TENT_DOOR_HALF, math.pi / 2 - TENT_DOOR_HALF),
                       (math.pi / 2 + TENT_DOOR_HALF, 3 * math.pi / 2 - TENT_DOOR_HALF)):
        n = 7; step = (end - start) / n
        for i in range(n):
            a = start + (i + 0.5) * step
            chord = 2 * TENT_R * math.sin(step / 2) + 0.12
            r = TENT_R * math.cos(step / 2)
            marker(f"COL_Wall_{k:02d}", (r * math.cos(a), r * math.sin(a), TENT_WALL / 2), (0.3, chord, TENT_WALL), a); k += 1
    marker("COL_Crates", (2.85, 0.65, 0.75), (1.2, 2.4, 1.5))
    marker("COL_Barrels", (-2.65, 0.5, 0.55), (1.6, 2.9, 1.1))
    marker("Light_Inside", (0, 0, 2.4), empty=True)
    marker("Light_Exit_Front", (0, -TENT_R - 0.6, TENT_DOOR_H + 0.3), empty=True)
    marker("Light_Exit_Back", (0, TENT_R + 0.6, TENT_DOOR_H + 0.3), empty=True)
    marker("Zone_Inside", (0, 0, 1.6), (2 * TENT_R * 0.92, 2 * TENT_R * 0.92, 3.2), empty=True)


def bench():
    P = "MO_Bench"
    for k in range(3):                                                         # seat planks
        box(P, (2.0, 0.16, 0.07), (0, -0.2 + k * 0.19, 0.48), "wood", bevel=0.02)
    for k in range(2):                                                         # back planks
        box(P, (2.0, 0.07, 0.16), (0, 0.3, 0.72 + k * 0.22), "wood", rot=(0.15, 0, 0), bevel=0.02)
    for x in (-0.85, 0.85):
        bar(P, (x, -0.25, 0.0), (x, -0.2, 0.45), 0.07, "iron")
        bar(P, (x, 0.25, 0.0), (x, 0.3, 1.0), 0.07, "iron")
        bar(P, (x, -0.28, 0.65), (x, 0.28, 0.66), 0.06, "iron")                 # arm rest
        torus(P, 0.12, 0.025, (x, -0.28, 0.66), "iron", rot=(0, math.pi / 2, 0), seg=12)
        sphere(P, 0.05, (x, 0.3, 1.03), "gold", seg=8)


def street_lamp():
    P = "MO_StreetLamp"
    cyl(P, 0.38, 0.3, (0, 0, 0.15), "iron", verts=8, bevel=0.03)
    cyl(P, 0.26, 0.4, (0, 0, 0.5), "iron", verts=8, r2=0.14, bevel=0.02)
    cyl(P, 0.1, 3.6, (0, 0, 2.5), "iron", verts=12, bevel=0.0, smooth=True)
    for z in (1.0, 2.6):
        torus(P, 0.14, 0.03, (0, 0, z), "gold", seg=12)
    for s in (-1, 1):                                                          # curls under the lantern
        torus(P, 0.25, 0.035, (s * 0.25, 0, 4.05), "iron", rot=(math.pi / 2, 0, 0), seg=16)
    lantern(P, (0, 0, 4.3), 0.0, 1.1, post=False)


def crate_barrel():
    P = "MO_CrateBarrel"
    box(P, (1.1, 1.1, 1.1), (0, 0, 0.55), "wood", bevel=0.04)
    for z in (0.05, 1.05):
        for s in (-1, 1):
            box(P, (1.16, 0.08, 0.1), (0, s * 0.54, z), "iron", bevel=0.01)
            box(P, (0.08, 1.16, 0.1), (s * 0.54, 0, z), "iron", bevel=0.01)
    bar(P, (-0.52, -0.56, 0.08), (0.52, -0.56, 1.02), 0.1, "wood")              # diagonal brace
    box(P, (0.75, 0.75, 0.75), (0.05, 0.05, 1.48), "wood", rot=(0, 0, 0.35), bevel=0.03)   # small crate on top
    torus(P, 0.28, 0.06, (0.05, 0.05, 1.89), "wood", seg=20)                     # rope coil
    cyl(P, 0.5, 1.15, (1.25, 0.1, 0.575), "wood", verts=20, bevel=0.06, smooth=True)          # barrel
    for z in (0.2, 0.95):
        cyl(P, 0.53, 0.08, (1.25, 0.1, z), "iron", verts=20, bevel=0.0)
    cyl(P, 0.45, 0.04, (1.25, 0.1, 1.16), "dark", verts=20, bevel=0.0)


def fountain():
    P = "MO_Fountain"
    from ride_common import scene
    # basin wall (ring) + water
    import bmesh
    segs = 32
    verts, faces = [], []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        for r, z in ((2.4, 0), (2.4, 0.7), (2.05, 0.7), (2.05, 0.1)):
            verts.append((r * math.cos(a), r * math.sin(a), z))
    for i in range(segs):
        j = (i + 1) % segs
        for k in range(4):
            faces.append((i * 4 + k, j * 4 + k, j * 4 + (k + 1) % 4, i * 4 + (k + 1) % 4))
    mesh_obj(P, "Basin", verts, faces, "fstone", bevel=0.03)
    torus(P, 2.23, 0.12, (0, 0, 0.74), "gold", seg=40)
    cyl(P, 2.1, 0.08, (0, 0, 0.5), "water", verts=32, bevel=0.0)
    cyl(P, 0.45, 1.3, (0, 0, 1.15), "fstone", verts=16, bevel=0.03)                # pedestal
    sphere(P, 1.0, (0, 0, 1.9), "fstone", seg=24, scale=(1, 1, 0.35))               # upper bowl
    cyl(P, 0.88, 0.06, (0, 0, 2.08), "water", verts=24, bevel=0.0)
    torus(P, 0.98, 0.07, (0, 0, 2.05), "gold", seg=28)
    # jester statue holding a ball
    cyl(P, 0.32, 0.4, (0, 0, 2.3), "fstone", verts=12, bevel=0.02)
    cyl(P, 0.3, 0.9, (0, 0, 2.95), "fstone", verts=12, r2=0.18, bevel=0.02)       # body
    sphere(P, 0.25, (0, 0, 3.6), "fstone", seg=14)                                  # head
    for s in (-1, 1):
        d = Vector((s * 0.35, 0, 0.55)).normalized()
        cyl(P, 0.14, 0.55, Vector((s * 0.1, 0, 3.75)) + d * 0.27, "fstone", verts=8, r2=0.0, rot=d.to_track_quat("Z", "Y").to_euler(), bevel=0.0)
        sphere(P, 0.07, (s * 0.32, 0, 4.25), "gold", seg=8)
        bar(P, (s * 0.25, 0, 3.2), (s * 0.4, 0, 3.75), 0.1, "fstone", round_=True)  # raised arms
    sphere(P, 0.22, (0, 0, 4.15), "gold", seg=14)                                   # ball held up
    for a, p in ring_of(6, 0.6, 0):                                                 # water spouts
        bar(P, (p.x * 0.5, p.y * 0.5, 2.0), (p.x * 2.0, p.y * 2.0, 0.55), 0.06, "water", round_=True)


def fence():
    P = "MO_Fence"
    for x in (-1.5, 1.5):
        box(P, (0.28, 0.28, 2.0), (x, 0, 1.0), "purple", bevel=0.03)
        box(P, (0.34, 0.34, 0.1), (x, 0, 2.05), "gold", bevel=0.01)
        sphere(P, 0.12, (x, 0, 2.2), "gold", seg=10)
        box(P, (0.36, 0.36, 0.25), (x, 0, 0.12), "stone", bevel=0.02)
    for z in (0.3, 1.6):
        box(P, (2.8, 0.07, 0.08), (0, 0, z), "iron", bevel=0.01)
    for k in range(13):
        x = -1.3 + k * 2.6 / 12
        h = 1.75 + 0.18 * math.cos((x / 1.3) * math.pi / 2) ** 2                      # arched top
        cyl(P, 0.028, h - 0.1, (x, 0, 0.1 + (h - 0.1) / 2), "iron", verts=6, bevel=0.0)
        cyl(P, 0.06, 0.16, (x, 0, h + 0.08), "gold", verts=6, r2=0.0, bevel=0.0)
    torus(P, 0.22, 0.035, (0, 0, 1.05), "gold", rot=(math.pi / 2, 0, 0), seg=16)    # centre ornament
    sphere(P, 0.09, (0, 0, 1.05), "glow", seg=8)


# ---------------------------------------------------------------- monster room (round, doors at 12/3/6/9)
ROOM_R, ROOM_WALL, ROOM_H, DOOR_W, DOOR_H = 12.0, 0.6, 5.0, 4.0, 4.5


def monster_room():
    P = "MO_MonsterRoom"
    r_out, r_in = ROOM_R + ROOM_WALL / 2, ROOM_R - ROOM_WALL / 2
    gap = math.asin((DOOR_W / 2 + 0.4) / ROOM_R)                                  # half angle of each doorway (+ jambs)
    import bmesh
    for q in range(4):                                                             # four wall arcs between the doors
        a0 = q * math.pi / 2 + gap; a1 = (q + 1) * math.pi / 2 - gap
        n = 18
        verts, faces = [], []
        for i in range(n + 1):
            a = a0 + (a1 - a0) * i / n
            for r, z in ((r_out, 0), (r_out, ROOM_H), (r_in, ROOM_H), (r_in, 0)):
                verts.append((r * math.cos(a), r * math.sin(a), z))
        for i in range(n):
            for k in range(4):
                faces.append((i * 4 + k, (i + 1) * 4 + k, (i + 1) * 4 + (k + 1) % 4, i * 4 + (k + 1) % 4))
        faces.append((0, 1, 2, 3)); faces.append((n * 4 + 3, n * 4 + 2, n * 4 + 1, n * 4))
        mesh_obj(P, f"Wall_{q}", verts, faces, "stone", bevel=0.04)
        pts = [((r_out + 0.12) * math.cos(a0 + (a1 - a0) * i / 24), (r_out + 0.12) * math.sin(a0 + (a1 - a0) * i / 24), ROOM_H - 0.4) for i in range(25)]
        tube(P, pts, 0.18, "red", name=f"Band_{q}")
        pts = [((r_out + 0.05) * math.cos(a0 + (a1 - a0) * i / 24), (r_out + 0.05) * math.sin(a0 + (a1 - a0) * i / 24), 0.25) for i in range(25)]
        tube(P, pts, 0.12, "gold", name=f"Skirt_{q}")
        for i in range(1, 5):                                                         # wall emblems + lamps between
            a = a0 + (a1 - a0) * i / 5
            c = Vector((math.cos(a), math.sin(a), 0))
            if i % 2:
                medallion(P, tuple(c * (r_out + 0.1) + Vector((0, 0, 2.4))), a, 0.7, "emblem")
            else:
                lantern(P, tuple(c * (r_out + 0.35) + Vector((0, 0, 2.6))), 0.0, 0.8, post=False)
    for q in range(4):                                                             # door pillars (jambs) with flame tops
        a = q * math.pi / 2
        c = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-c.y, c.x, 0))
        for s in (-1, 1):
            p = c * ROOM_R + t * s * (DOOR_W / 2 + 0.45)
            box(P, (0.9, 0.9, ROOM_H + 0.8), (p.x, p.y, (ROOM_H + 0.8) / 2), "purple", rot=(0, 0, a), bevel=0.06)
            box(P, (1.05, 1.05, 0.15), (p.x, p.y, ROOM_H + 0.85), "gold", rot=(0, 0, a), bevel=0.02)
            lantern(P, (p.x, p.y, ROOM_H + 0.9), 0.0, 0.9, post=False)
        lintel = c * ROOM_R + Vector((0, 0, DOOR_H + 0.35))
        box(P, (0.7, DOOR_W + 1.8, 0.7), tuple(lintel), "red", rot=(0, 0, a), bevel=0.05)
        medallion(P, tuple(c * (ROOM_R + 0.4) + Vector((0, 0, DOOR_H + 0.4))), a, 0.45, "emblem")
    # centre ornament: stepped dais, skull altar with flame
    cyl(P, 2.2, 0.3, (0, 0, 0.15), "stone", verts=24, bevel=0.03)
    cyl(P, 1.5, 0.3, (0, 0, 0.45), "purple", verts=24, bevel=0.03)
    torus(P, 1.52, 0.06, (0, 0, 0.6), "gold", seg=28)
    cyl(P, 0.5, 1.1, (0, 0, 1.15), "purple", verts=12, bevel=0.04)
    sphere(P, 0.55, (0, 0, 2.0), "bone", seg=18, scale=(1, 0.95, 1))
    for s in (-1, 1):
        sphere(P, 0.12, (s * 0.2, -0.47, 2.05), "glow", seg=8)
    cyl(P, 0.25, 0.7, (0, 0, 2.75), "flame", verts=10, r2=0.0, bevel=0.0)
    cyl(P, 11.6, 0.04, (0, 0, 0.02), "stone", verts=48, bevel=0.0)                  # floor


def monster_room_door():
    P = "MO_MonsterRoomDoor"                                                         # one leaf; origin = bottom centre
    box(P, (DOOR_W, 0.25, DOOR_H), (0, 0, DOOR_H / 2), "iron", bevel=0.04)
    for k in range(7):
        x = -DOOR_W / 2 + 0.35 + k * (DOOR_W - 0.7) / 6
        box(P, (0.12, 0.4, DOOR_H - 0.3), (x, 0, DOOR_H / 2), "red", bevel=0.02)
    for z in (0.6, DOOR_H - 0.6):
        box(P, (DOOR_W - 0.2, 0.38, 0.18), (0, 0, z), "gold", bevel=0.02)
    medallion(P, (0, -0.25, DOOR_H * 0.55), -math.pi / 2, 0.55, "emblem")
    for s in (-1, 1):
        sphere(P, 0.12, (s * 0.9, -0.25, DOOR_H * 0.55), "glow", seg=8)


# ---------------------------------------------------------------- exit gate (opens)
GATE_W, GATE_H, PILLAR_W = 5.6, 5.0, 1.6


def exit_gate():
    P = "MO_ExitGate"
    for s in (-1, 1):
        x = s * (GATE_W / 2 + PILLAR_W / 2)
        box(P, (PILLAR_W + 0.3, PILLAR_W + 0.3, 0.5), (x, 0, 0.25), "stone", bevel=0.05)
        box(P, (PILLAR_W, PILLAR_W, 6.2), (x, 0, 3.6), "stone", bevel=0.06)
        for z in (1.2, 4.0, 6.6):
            box(P, (PILLAR_W + 0.12, PILLAR_W + 0.12, 0.18), (x, 0, z), "gold", bevel=0.02)
        medallion(P, (x, -PILLAR_W / 2 - 0.05, 2.6), -math.pi / 2, 0.5, "emblem")
        cyl(P, 0.85, 1.6, (x, 0, 7.5), "purple", verts=8, r2=0.0, bevel=0.02)       # pillar spire
        lantern(P, (x, -0.9, 5.2), 0.0, 0.8, post=False)
        pennant(P, (x, 0, 8.2), 1.4, 90 if s > 0 else 90)
    # arch over the opening
    pts = []
    for i in range(25):
        t = i / 24
        a = math.pi * t
        pts.append((-(GATE_W / 2 + 0.2) * math.cos(a), 0, 6.2 + 1.6 * math.sin(a)))
    tube(P, pts, 0.45, "purple", name="Arch")
    tube(P, [(p[0], -0.3, p[2] + 0.35) for p in pts], 0.12, "gold", name="ArchTrim")
    for i in range(1, 12, 2):
        sphere(P, 0.12, (pts[i * 2][0], -0.45, pts[i * 2][2]), "glass", seg=8)
    box(P, (GATE_W + 0.4, 0.5, 0.6), (0, 0, 5.6), "red", bevel=0.05)                # lintel beam
    plane(P, 2.6, 0.9, (0, -0.3, 6.6), (math.pi / 2, 0, 0), "sign")               # sign board
    medallion(P, (0, -0.4, 7.9), -math.pi / 2, 0.6, "emblem")
    cyl(P, 0.1, 0.8, (0, 0, 8.6), "gold", verts=8, r2=0.0, bevel=0.0)


def gate_leaf(name, side):
    """Origin on the hinge (outer edge, ground); the leaf extends toward the middle. Open = rotate about Z."""
    P = name
    w = GATE_W / 2 - 0.05
    x0 = -side * 0.0
    d = -side                                            # leaf extends toward the centre (side=-1 left leaf extends +x)
    for z in (0.35, GATE_H / 2, GATE_H - 0.35):
        box(P, (w, 0.14, 0.16), (d * w / 2, 0, z), "iron", bevel=0.02)
    for k in range(7):
        x = d * (0.15 + k * (w - 0.3) / 6)
        h = GATE_H - 0.2 + 0.35 * math.sin((k / 6) * math.pi / 2 if side < 0 else (1 - k / 6) * math.pi / 2)
        cyl(P, 0.045, h, (x, 0, h / 2), "iron", verts=8, bevel=0.0)
        cyl(P, 0.09, 0.25, (x, 0, h + 0.12), "gold", verts=6, r2=0.0, bevel=0.0)
    box(P, (0.16, 0.18, GATE_H), (0, 0, GATE_H / 2), "iron", bevel=0.02)            # hinge stile
    box(P, (0.16, 0.18, GATE_H), (d * w, 0, GATE_H / 2), "iron", bevel=0.02)        # meeting stile
    torus(P, 0.5, 0.06, (d * w * 0.5, 0, GATE_H / 2), "gold", rot=(math.pi / 2, 0, 0), seg=24)
    sphere(P, 0.14, (d * w * 0.5, 0, GATE_H / 2), "glow", seg=8)


def exit_lever():
    P = "MO_ExitLever"
    box(P, (0.9, 0.7, 0.9), (0, 0, 0.45), "purple", bevel=0.06)
    box(P, (1.0, 0.8, 0.12), (0, 0, 0.95), "gold", bevel=0.02)
    box(P, (0.6, 0.45, 0.15), (0, 0, 1.08), "red", bevel=0.03)
    medallion(P, (0, -0.37, 0.45), -math.pi / 2, 0.22, "emblem")


def exit_lever_arm():
    P = "MO_ExitLeverArm"                                  # origin = pivot (lever base); pull = rotate about X
    cyl(P, 0.07, 1.0, (0, 0, 0.5), "iron", verts=10, bevel=0.0, smooth=True)
    sphere(P, 0.16, (0, 0, 1.05), "glow", seg=12)
    cyl(P, 0.14, 0.5, (0, 0, 0), "gold", verts=12, rot=(0, math.pi / 2, 0), bevel=0.0)


BUILDERS = [("MO_CherryJuice", cherry_juice), ("MO_CottonCandy", cotton_candy), ("MO_Popcorn", popcorn), ("MO_Candy", candy),
            ("MO_Tent", tent), ("MO_Bench", bench), ("MO_StreetLamp", street_lamp), ("MO_CrateBarrel", crate_barrel),
            ("MO_Fountain", fountain), ("MO_Fence", fence), ("MO_MonsterRoom", monster_room), ("MO_MonsterRoomDoor", monster_room_door),
            ("MO_ExitGate", exit_gate), ("MO_ExitGateDoorL", lambda: gate_leaf("MO_ExitGateDoorL", -1)),
            ("MO_ExitGateDoorR", lambda: gate_leaf("MO_ExitGateDoorR", 1)), ("MO_ExitLever", exit_lever), ("MO_ExitLeverArm", exit_lever_arm)]

objs = {}
for name, fn in BUILDERS:
    fn()
    objs[name] = rc.finish(name, (0, 0, 0))
tent_markers(objs["MO_Tent"])
info = {}
for name, o in objs.items():
    lo, hi = rc.Stage.bbox([o])
    info[name] = dict(size=[round(v, 2) for v in (hi - lo)], tris=rc.tris(o))
    print(name, info[name], flush=True)
    rc.export([o] + (TENT_MARKERS if name == "MO_Tent" else []), os.path.join(OUT, name + ".fbx"),
              types={"MESH", "EMPTY"} if name == "MO_Tent" else {"MESH"})

# gate assembly pieces in place (for the pictures and the assembled FBX): leaves hinge at the pillars, lever beside
gate_l, gate_r = objs["MO_ExitGateDoorL"], objs["MO_ExitGateDoorR"]
lever, arm = objs["MO_ExitLever"], objs["MO_ExitLeverArm"]
json.dump(dict(character_height=CHARACTER_H, objects=info,
               tent=dict(radius=TENT_R, wall=TENT_WALL, door_width=TENT_DOOR_W, door_height=TENT_DOOR_H, doors="front -Y, back +Y",
                         markers=[m.name for m in TENT_MARKERS]),
               exit_gate=dict(opening=GATE_W, leaf_height=GATE_H, hinge_left=[-GATE_W / 2, 0, 0], hinge_right=[GATE_W / 2, 0, 0],
                              open_angle_deg=100, lever_offset=[GATE_W / 2 + 2.6, -1.6, 0], lever_arm_pivot_z=1.15),
               monster_room=dict(radius=ROOM_R, wall=ROOM_WALL, height=ROOM_H, door_width=DOOR_W, door_height=DOOR_H,
                                 doors="12/3/6/9 o'clock on the wall circle"),
               note="Blender z up, metres, each object origin = bottom centre (gate leaves / lever arm: on their hinge / pivot)."),
          open(os.path.join(OUT, "map_objects.json"), "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "MO_MapObjects.blend"))

if not RENDER:
    sys.exit(0)

# ---------------------------------------------------------------- pictures
def mannequin(x, y):
    P = "MO_ScaleMannequin"
    cyl(P, 0.32, 1.1, (x, y, 0.75), "dummy", verts=16, bevel=0.0, smooth=True)
    sphere(P, 0.3, (x, y, 1.65), "dummy", seg=14)
    for s in (-1, 1):
        cyl(P, 0.11, 0.75, (x + s * 0.13, y, 0.37), "dummy", verts=10, bevel=0.0, smooth=True)
    sphere(P, 0.32, (x, y, 0.2 + 1.1), "dummy", seg=12, scale=(1, 1, 0.3))
    return rc.finish(P, (x, y, 0))


all_objs = list(objs.values())
man = mannequin(0, 0)
all_objs.append(man)
st = rc.Stage(OUT, all_objs, samples=40)


def place(o, x, y, yaw=0.0):
    o.location = (x, y, 0); o.rotation_euler = (0, 0, yaw)
    bpy.context.view_layer.update()


# gate assembled
place(objs["MO_ExitGate"], 0, 0); place(gate_l, -GATE_W / 2, 0); place(gate_r, GATE_W / 2, 0)
place(lever, GATE_W / 2 + 2.6, -1.6); place(arm, GATE_W / 2 + 2.6, -1.6); arm.location.z = 1.15
gate_set = [objs["MO_ExitGate"], gate_l, gate_r, lever, arm, man]
place(man, -GATE_W / 2 - 3.2, -2.0)
arm.rotation_euler = (math.radians(-35), 0, 0)
st.shoot("mo_gate_closed", gate_set, 10, 18, res=(1100, 900))
gate_l.rotation_euler.z = math.radians(100); gate_r.rotation_euler.z = math.radians(-100)
arm.rotation_euler = (math.radians(35), 0, 0)
st.shoot("mo_gate_open", gate_set, 10, 18, res=(1100, 900))
gate_l.rotation_euler.z = 0; gate_r.rotation_euler.z = 0

# singles with the 2 m mannequin
for name in ("MO_CherryJuice", "MO_CottonCandy", "MO_Popcorn", "MO_Candy", "MO_Tent", "MO_Bench", "MO_StreetLamp",
             "MO_CrateBarrel", "MO_Fountain", "MO_Fence", "MO_MonsterRoomDoor"):
    o = objs[name]
    place(o, 0, 0)
    lo, hi = rc.Stage.bbox([o])
    place(man, lo.x - 0.9, 0.4)
    st.shoot(name.replace("MO_", "mo_"), [o, man], 12, 25, res=(900, 900), fill=1.15)
place(objs["MO_MonsterRoom"], 0, 0)
d = objs["MO_MonsterRoomDoor"]
door_copies = []
for q in range(4):
    a = q * math.pi / 2
    c = d.copy(); c.data = d.data; bpy.context.scene.collection.objects.link(c)
    c.location = (math.cos(a) * ROOM_R, math.sin(a) * ROOM_R, 0); c.rotation_euler = (0, 0, a + math.pi / 2)
    door_copies.append(c)
st.all = all_objs + door_copies
place(man, 0, -ROOM_R - 2.5)
st.shoot("mo_monster_room", [objs["MO_MonsterRoom"], man] + door_copies, 35, 20, res=(1100, 900), fill=1.0)
for c in door_copies: c.hide_render = True
# lineup of the materials + decoration with the mannequin
xs = 0.0
line = []
for name in ("MO_CherryJuice", "MO_CottonCandy", "MO_Popcorn", "MO_Candy", "MO_Tent", "MO_Bench", "MO_StreetLamp", "MO_CrateBarrel", "MO_Fountain", "MO_Fence"):
    o = objs[name]
    lo, hi = rc.Stage.bbox([o]); w = hi.x - lo.x
    place(o, xs + w / 2, 0); xs += w + 1.2
    line.append(o)
place(man, -1.2, 0)
st.shoot("mo_lineup", line + [man], 8, 0, res=(1800, 650), fill=1.02, lens=60)
print("DONE", flush=True)
