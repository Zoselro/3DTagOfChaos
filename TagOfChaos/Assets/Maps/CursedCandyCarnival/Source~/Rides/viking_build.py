# Viking (pirate ship swing) for the Cursed Candy Carnival (2026-10-22). Parts — ship (swing arms, origin = pivot) with the
# received Meshy hull as its child VK_ShipHull (own UVs + 2K PBR maps kept, display base plate cut away), frame (two A-frames
# with ladder legs, bearing heads, lamps, flags), axle (skull medallion, end lamps) and platform (deck, railing, flame lamps,
# stairs, barrels, banners). My parts are primitives at full detail with world-scale tiling textures (1024 px); nothing is
# decimated or rebaked (no smearing in Unity).
# The ship swings about the axle (Blender Y / Unity X): a keyed pendulum cycle for preview, numbers in viking.json.
# python viking_build.py -- <tex dir> <out dir> <render 1|0> <meshy hull dir: hull.fbx, hull.png, hull_normal/metallic/roughness.png>
import bpy, bmesh, sys, os, math, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ride_common as rc
import numpy as np
from ride_common import box, cyl, sphere, torus, bar, tube, plane, medallion, ring_of, mesh_obj

argv = sys.argv[sys.argv.index("--") + 1:]
TEX, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
HULL = os.path.abspath(argv[3])
RENDER = argv[2] != "0"
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- dimensions (m)
PF = 1.2                       # platform floor
PIVOT_Z = PF + 13.5            # axle height
ARM_L = 9.5                    # pivot -> hull reference line (gunwale middle)
SHIP_L, BEAM = 14.0, 4.7          # Meshy hull: 14 x 4.69 m at scale 7.367
FRAME_Y = BEAM / 2 + 1.35      # A-frame planes
ARM_Y = BEAM / 2 + 0.3         # swing arms
FOOT_X = 8.6                   # A-frame feet spread (along the ship)
PLAT_X, PLAT_Y, CHAMFER = 14.5, 8.8, 3.2
MAX_SWING = 75.0               # degrees
PERIOD = 6.0                   # s, ~ 2*pi*sqrt(L/g) for L ~ 9.5 m

rc.reset(TEX)
M = rc.material
M("stone", "VK_PurpleStone", "GD_PurpleStone", rough=0.85, uv=2.0)
M("purple", "VK_PurpleMetal", "GD_PurpleMetal", metallic=0.3, rough=0.45, uv=2.0)
M("red", "VK_RedMetal", "GD_RedMetal", metallic=0.3, rough=0.4, uv=2.0)
M("gold", "VK_Gold", "GD_Gold", metallic=0.9, rough=0.3, uv=1.0)
M("hull", "VK_HullWood", "VK_HullWood", rough=0.7, uv=3.0)
M("deck", "VK_DeckPlanks", "VK_DeckPlanks", rough=0.8, uv=3.0)
M("barrel", "VK_BarrelWood", "VK_BarrelWood", rough=0.75, uv=1.2)
M("bone", "VK_Bone", "VK_Bone", rough=0.55, uv=1.5)
M("horn", "VK_Horn", "VK_Horn", rough=0.5, uv=1.0)
M("seat", "VK_RedSeat", "GD_RedSeat", rough=0.5, uv=0.8)
M("pad", "VK_BlackPad", "GD_BlackPad", rough=0.7, uv=0.8)
M("banner", "VK_Banner", "VK_Banner", rough=0.85)
M("emblem", "VK_SkullEmblem", "VK_SkullEmblem", rough=0.5)
M("glass", "VK_LampGlass", None, (1.0, 0.6, 0.25), rough=0.2, emission=(1.0, 0.5, 0.15), strength=10.0)
M("flame", "VK_Flame", None, (1.0, 0.75, 0.3), rough=0.3, emission=(1.0, 0.55, 0.1), strength=25.0)
M("eye", "VK_Eye", None, (1.0, 0.45, 0.1), rough=0.3, emission=(1.0, 0.3, 0.04), strength=6.0)
M("socket", "VK_Socket", None, (0.05, 0.02, 0.04), rough=0.9)


# ---------------------------------------------------------------- shared decorations
def flame_lamp(part, base, post_h, scale=1.0):
    """Purple post, lantern with glowing glass, red roof, flame on top (상단 장식 화염등)."""
    x, y, z = base; s = scale
    if post_h > 0:
        box(part, (0.42 * s, 0.42 * s, post_h), (x, y, z + post_h / 2), "purple", bevel=0.04)
    z += post_h
    box(part, (0.75 * s, 0.75 * s, 0.14 * s), (x, y, z + 0.07 * s), "gold", bevel=0.02)
    box(part, (0.55 * s, 0.55 * s, 0.7 * s), (x, y, z + 0.49 * s), "glass", bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(part, (0.1 * s, 0.1 * s, 0.75 * s), (x + sx * 0.3 * s, y + sy * 0.3 * s, z + 0.49 * s), "purple", bevel=0.01)
    cyl(part, 0.55 * s, 0.5 * s, (x, y, z + 1.1 * s), "red", verts=4, r2=0.0, rot=(0, 0, math.pi / 4), bevel=0.0)
    sphere(part, 0.24 * s, (x, y, z + 1.45 * s), "flame", seg=10, scale=(1, 1, 1.3))
    cyl(part, 0.2 * s, 0.6 * s, (x, y, z + 1.85 * s), "flame", verts=10, r2=0.0, bevel=0.0)


def banner(part, base, height, yaw_deg):
    """Pole with a hanging purple skull banner (깃발)."""
    x, y, z = base
    cyl(part, 0.08, height, (x, y, z + height / 2), "purple", verts=10, bevel=0.0, smooth=True)
    cyl(part, 0.16, 0.5, (x, y, z + height + 0.25), "gold", verts=8, r2=0.0, bevel=0.0)
    a = math.radians(yaw_deg)
    d = Vector((math.cos(a), math.sin(a), 0))
    arm_end = Vector((x, y, z + height - 0.2)) + d * 1.25
    bar(part, (x, y, z + height - 0.2), arm_end, 0.08, "gold", round_=True)
    c = Vector((x, y, z + height - 0.2 - 1.15)) + d * 0.65
    plane(part, 1.15, 2.2, c, (math.pi / 2, 0, a), "banner")


def spike_emblem(part, center, yaw, r):
    """Skull medallion with gold spikes (장식물 해골 문양)."""
    medallion(part, center, yaw, r, "emblem", ring_mat="red")
    n = Vector((math.cos(yaw), math.sin(yaw), 0))
    side = Vector((-n.y, n.x, 0))
    for k in range(8):
        a = k * math.pi / 4
        d = side * math.cos(a) + Vector((0, 0, 1)) * math.sin(a)
        p = Vector(center) + d * (r + 0.25)
        rot = d.to_track_quat("Z", "Y").to_euler()
        cyl(part, 0.16 * r, 0.5 * r, p, "gold", verts=6, r2=0.0, rot=rot, bevel=0.0)


# ---------------------------------------------------------------- 1. ship (origin = pivot)
def hull_shape(u):
    w = BEAM / 2 * max(0.0, 1 - abs(u) ** 2.6) ** 0.55
    sheer = 0.4 + 1.7 * u * u
    keel = -1.8 + 1.25 * u ** 4
    return w, sheer, keel


def build_ship():
    P = "VK_Ship"
    zc = PIVOT_Z - ARM_L                      # hull reference line (world)
    S, Mp = 40, 14
    verts, faces = [], []
    for i in range(S + 1):
        u = -1 + 2 * i / S
        x = u * SHIP_L / 2
        w, sheer, keel = hull_shape(u)
        for j in range(2 * Mp + 1):
            t = -1 + j / Mp                    # -1 port gunwale .. 0 keel .. 1 starboard gunwale
            phi = abs(t) * math.pi / 2
            y = math.copysign(w * math.sin(phi) ** 0.6, t)
            z = keel + (sheer - keel) * (1 - math.cos(phi))
            verts.append((x, y, zc + z))
    row = 2 * Mp + 1
    for i in range(S):
        for j in range(row - 1):
            a = i * row + j
            faces.append((a, a + 1, a + row + 1, a + row))
    hull = mesh_obj(P, "Hull", verts, faces, "hull", smooth=True)
    sol = hull.modifiers.new("solid", "SOLIDIFY"); sol.thickness = 0.2; sol.offset = -1
    # deck inside
    dv, df = [], []
    for i in range(S + 1):
        u = -0.78 + 1.56 * i / S
        w, sheer, keel = hull_shape(u)
        zd = zc - 0.75
        dv += [(u * SHIP_L / 2, -w + 0.3, zd), (u * SHIP_L / 2, w - 0.3, zd)]
    for i in range(S):
        a = 2 * i
        df.append((a, a + 1, a + 3, a + 2))
    deck = mesh_obj(P, "Deck", dv, df, "deck")
    deck.modifiers.new("solid", "SOLIDIFY").thickness = 0.12
    # gunwale trims (gold), mid band (gold), rivets
    for s in (-1, 1):
        top = [(u * SHIP_L / 2, s * hull_shape(u)[0], zc + hull_shape(u)[1] + 0.05) for u in [k / 20 * 2 - 1 for k in range(21)]]
        tube(P, top, 0.14, "gold", name="Gunwale")
        mid = []
        for k in range(21):
            u = (k / 20 * 2 - 1) * 0.92
            w, sheer, keel = hull_shape(u)
            phi = math.radians(62)
            mid.append((u * SHIP_L / 2, s * (w * math.sin(phi) ** 0.6 + 0.06), zc + keel + (sheer - keel) * (1 - math.cos(phi))))
        tube(P, mid, 0.09, "gold", name="Band")
        for k in range(1, 12):
            u = -0.85 + 1.7 * k / 12
            w, sheer, keel = hull_shape(u)
            phi = math.radians(75)
            sphere(P, 0.09, (u * SHIP_L / 2, s * (w * math.sin(phi) ** 0.6 + 0.05), zc + keel + (sheer - keel) * (1 - math.cos(phi))), "gold", seg=8)
        # side skull emblems
        for xe in (-3.6, 3.6):
            w, sheer, keel = hull_shape(xe / (SHIP_L / 2))
            spike_emblem(P, (xe, s * (w + 0.12), zc - 0.35), s * math.pi / 2, 0.55)
    # seats: rows face the middle; four across in the wide middle, two near the ends
    for x in (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0):
        face = 1 if x < 0 else -1
        ys = (-1.35, -0.45, 0.45, 1.35) if abs(x) < 4 else (-0.45, 0.45)
        zd = zc - 0.75
        for y in ys:
            box(P, (0.7, 0.8, 0.45), (x, y, zd + 0.28), "seat", bevel=0.08)
            box(P, (0.22, 0.8, 1.15), (x - face * 0.38, y, zd + 0.62), "seat", bevel=0.08)
            box(P, (0.06, 0.62, 0.8), (x - face * 0.26, y, zd + 0.72), "pad", bevel=0.02)
            torus(P, 0.36, 0.07, (x + face * 0.05, y, zd + 1.0), "pad", rot=(0, math.pi / 2, 0), seg=20)
        box(P, (0.14, (max(ys) - min(ys)) + 1.0, 0.75), (x - face * 0.52, 0, zd + 0.38), "red", bevel=0.03)   # row divider
    # bow: the hull's stem rises and curls up, a big angry demon skull sits on it facing forward (+x) — reference 선체 앞부분
    w0, sh, kl = hull_shape(1.0)
    tip = SHIP_L / 2
    stem = []
    for k in range(13):                                                     # stem post: from the keel at the bow up and forward
        t = k / 12
        a = math.radians(-70 + 160 * t)
        stem.append((tip - 0.2 + 0.9 * math.cos(a) * t + 0.4 * t, 0, zc + kl + 0.3 + (sh - kl + 1.2) * t))
    tube(P, stem, 0.42, "hull", name="Stem")
    tube(P, [(p[0] + 0.05, 0, p[2]) for p in stem[4:]], 0.47, "gold", name="StemTrim")
    sx, sz = tip + 1.05, zc + sh + 0.55                                    # skull centre
    sphere(P, 1.45, (sx, 0, sz + 0.35), "bone", seg=24, scale=(0.92, 1.0, 1.0))           # cranium
    sphere(P, 0.85, (sx + 0.75, 0, sz - 0.35), "bone", seg=20, scale=(0.7, 1.05, 0.75))   # muzzle / upper jaw
    for s in (-1, 1):
        sphere(P, 0.5, (sx + 0.85, s * 0.72, sz - 0.05), "bone", seg=14, scale=(0.8, 0.7, 0.65))   # cheekbones
    # open lower jaw (hinged down), dark mouth, two rows of teeth
    box(P, (1.05, 1.45, 0.42), (sx + 0.85, 0, sz - 1.25), "bone", rot=(0, -0.28, 0), bevel=0.18)
    box(P, (0.75, 1.15, 0.55), (sx + 1.05, 0, sz - 0.85), "socket", rot=(0, -0.15, 0), bevel=0.12)
    for k in range(6):
        yt = -0.62 + k * 0.248
        cyl(P, 0.11, 0.38, (sx + 1.38, yt, sz - 0.62), "bone", verts=6, r2=0.0, rot=(math.pi, 0, 0), bevel=0.0)   # upper fangs
        cyl(P, 0.1, 0.32, (sx + 1.3, yt + 0.12, sz - 1.08), "bone", verts=6, r2=0.0, bevel=0.0)                 # lower teeth
    cyl(P, 0.16, 0.6, (sx + 1.4, -0.55, sz - 0.72), "bone", verts=6, r2=0.0, rot=(math.pi, 0, 0), bevel=0.0)       # long fangs
    cyl(P, 0.16, 0.6, (sx + 1.4, 0.55, sz - 0.72), "bone", verts=6, r2=0.0, rot=(math.pi, 0, 0), bevel=0.0)
    # nose hole, deep eye sockets with glowing eyes, angry V brows
    cyl(P, 0.2, 0.3, (sx + 1.38, 0, sz - 0.12), "socket", verts=3, rot=(0, math.pi / 2, math.pi / 2), bevel=0.0)
    for s in (-1, 1):
        sphere(P, 0.42, (sx + 1.02, s * 0.55, sz + 0.42), "socket", seg=14, scale=(0.7, 1.0, 0.85))
        sphere(P, 0.25, (sx + 1.22, s * 0.55, sz + 0.4), "eye", seg=14)
        box(P, (0.45, 0.8, 0.22), (sx + 1.12, s * 0.5, sz + 0.86), "bone", rot=(s * 0.45, 0.15, 0), bevel=0.08)
    # big horns: four segments sweeping out, up and back
    for s in (-1, 1):
        p = Vector((sx + 0.2, s * 1.15, sz + 0.95))
        for k, (r0, ln, out, up, back) in enumerate(((0.42, 0.75, 1.0, 0.35, 0.1), (0.33, 0.7, 0.7, 0.9, -0.2),
                                                     (0.24, 0.65, 0.3, 1.0, -0.6), (0.15, 0.6, -0.1, 0.9, -0.9))):
            d = Vector((back, s * out, up)).normalized()
            cyl(P, r0, ln, p + d * ln / 2, "horn", verts=12, r2=(r0 * 0.75 if k < 3 else 0.0), rot=d.to_track_quat("Z", "Y").to_euler(), bevel=0.0, smooth=True)
            p = p + d * ln * 0.92
    # red hood over the top and back of the skull, gold rim, flame lamp on the crown
    sphere(P, 1.5, (sx - 0.35, 0, sz + 0.75), "red", seg=20, scale=(0.95, 1.05, 0.75))
    torus(P, 1.32, 0.09, (sx + 0.05, 0, sz + 0.45), "gold", rot=(0, math.radians(80), 0), seg=28)
    flame_lamp(P, (sx - 0.25, 0, sz + 1.75), 0.35, 1.0)
    # gold collar where the skull meets the hull
    torus(P, 0.9, 0.14, (tip + 0.15, 0, sz - 0.35), "gold", rot=(0, math.pi / 2, 0), seg=24)
    # stern lamp (-x)
    w1, sh1, kl1 = hull_shape(-1.0)
    flame_lamp(P, (-SHIP_L / 2 + 0.4, 0, zc + sh1 - 0.1), 0.8, 0.8)
    torus(P, 0.5, 0.12, (-SHIP_L / 2 - 0.1, 0, zc + sh1 + 0.3), "gold", rot=(math.pi / 2, 0, 0), seg=20)
    # swing arms: hub on the axle -> two points on each gunwale
    for s in (-1, 1):
        y = s * ARM_Y
        cyl(P, 0.75, 0.7, (0, y, PIVOT_Z), "purple", verts=20, rot=(math.pi / 2, 0, 0), bevel=0.05)
        cyl(P, 0.82, 0.18, (0, y + s * 0.38, PIVOT_Z), "gold", verts=20, rot=(math.pi / 2, 0, 0), bevel=0.02)
        for xa in (-3.4, 3.4):
            u = xa / (SHIP_L / 2)
            w, sheer, keel = hull_shape(u)
            foot = Vector((xa, s * (w + 0.1), zc + sheer + 0.1))
            bar(P, (0, y, PIVOT_Z), foot, 0.5, "purple", bevel=0.05)
            bar(P, (0, y + s * 0.05, PIVOT_Z), foot + Vector((0, s * 0.05, 0)), 0.2, "red", bevel=0.02)
            for t in (0.2, 0.4, 0.6, 0.8):
                q = Vector((0, y, PIVOT_Z)).lerp(foot, t)
                sphere(P, 0.1, (q.x, q.y + s * 0.27, q.z), "gold", seg=8)
        bar(P, (-2.0, y, zc + 5.6), (2.0, y, zc + 5.6), 0.35, "red")                          # cross brace



def build_ship_meshy():
    """Imported Meshy hull (own UVs + 2K PBR maps, untouched) + my swing arms built to its measured gunwale."""
    P = "VK_Ship"
    zc = PIVOT_Z - ARM_L
    bpy.ops.import_scene.fbx(filepath=HULL + "/hull.fbx")
    h = [x for x in bpy.context.selected_objects if x.type == "MESH"][0]
    for x in bpy.context.selected_objects:
        if x is not h: bpy.data.objects.remove(x)
    h.name = "VK_ShipHull"; h.parent = None
    bpy.context.view_layer.objects.active = h; h.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    v = np.array([p.co for p in h.data.vertices])
    s = SHIP_L / np.ptp(v[:, 0])
    cx, cy = (v[:, 0].max() + v[:, 0].min()) / 2, (v[:, 1].max() + v[:, 1].min()) / 2
    vv = v.copy(); vv[:, 0] = -(v[:, 0] - cx) * s; vv[:, 1] = -(v[:, 1] - cy) * s; vv[:, 2] = (v[:, 2] - v[:, 2].min()) * s   # bow -> +X
    # Meshy added a display stand under the keel (side rails at |y| 0.5-0.8 m, end bars, z < 0.25 m): cut it away
    plate = (np.abs(vv[:, 0]) < 2.25) & (np.abs(vv[:, 1]) < 0.95) & ((vv[:, 2] < 0.25) | ((np.abs(vv[:, 1]) >= 0.5) & (vv[:, 2] < 0.44)))
    bm = bmesh.new(); bm.from_mesh(h.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in np.nonzero(plate)[0]], context="VERTS")
    loose = [v_ for v_ in bm.verts if not v_.link_faces]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
    holes = bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=64)
    bm.to_mesh(h.data); bm.free(); h.data.update()
    v = np.array([p.co for p in h.data.vertices])
    vv = v.copy(); vv[:, 0] = -(v[:, 0] - cx) * s; vv[:, 1] = -(v[:, 1] - cy) * s; vv[:, 2] = (v[:, 2] - v[:, 2].min()) * s
    print("MESHY plate cut", int(plate.sum()), "verts, holes filled", len(holes["faces"]), "keel now", round(vv[:, 2].min(), 3), flush=True)
    def rim(x0):
        sl = vv[np.abs(vv[:, 0] - x0) < 0.12]; ym = np.abs(sl[:, 1]).max()
        return ym, sl[np.abs(sl[:, 1]) > ym - 0.25][:, 2].max()
    dz = zc + 0.4 - rim(0.0)[1]                           # midship gunwale on the same reference line as my hull
    vv[:, 2] += dz
    h.data.vertices.foreach_set("co", vv.ravel())
    h.data.update()
    m = bpy.data.materials.new("VK_ShipHull"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    def tex(f, cs):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(HULL + "/" + f); n.image.colorspace_settings.name = cs; return n
    nt.links.new(tex("hull.png", "sRGB").outputs[0], b.inputs["Base Color"])
    nt.links.new(tex("hull_metallic.png", "Non-Color").outputs[0], b.inputs["Metallic"])
    nt.links.new(tex("hull_roughness.png", "Non-Color").outputs[0], b.inputs["Roughness"])
    nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tex("hull_normal.png", "Non-Color").outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    h.data.materials.clear(); h.data.materials.append(m)
    feet = []
    for s_ in (-1, 1):
        y = s_ * ARM_Y
        cyl(P, 0.75, 0.7, (0, y, PIVOT_Z), "purple", verts=20, rot=(math.pi / 2, 0, 0), bevel=0.05)
        cyl(P, 0.82, 0.18, (0, y + s_ * 0.38, PIVOT_Z), "gold", verts=20, rot=(math.pi / 2, 0, 0), bevel=0.02)
        for xa in (-3.0, 3.0):
            w, rz = rim(xa)
            foot = Vector((xa, s_ * (w + 0.1), rz + 0.1)); feet.append(foot)
            bar(P, (0, y, PIVOT_Z), foot, 0.5, "purple", bevel=0.05)
            bar(P, (0, y + s_ * 0.05, PIVOT_Z), foot + Vector((0, s_ * 0.05, 0)), 0.2, "red", bevel=0.02)
            for t in (0.2, 0.4, 0.6, 0.8):
                q = Vector((0, y, PIVOT_Z)).lerp(foot, t)
                sphere(P, 0.1, (q.x, q.y + s_ * 0.27, q.z), "gold", seg=8)
        bar(P, (-2.0, y, zc + 5.6), (2.0, y, zc + 5.6), 0.35, "red")
    # clearance: hull verts near the arm centre lines (excluding the last 0.5 m at the foot)
    hub = lambda f: np.array((0, math.copysign(ARM_Y, f.y), PIVOT_Z))
    clash = 0
    for f in feet:
        a0, a1 = hub(f), np.array(f); d = a1 - a0; L = np.linalg.norm(d); d /= L
        t = np.clip((vv - a0) @ d, 0, L - 0.5); dist = np.linalg.norm(vv - (a0 + t[:, None] * d), axis=1)
        clash += int((dist < 0.3).sum())
    print("MESHY scale", round(s, 3), "dz", round(dz, 2), "keel", round(vv[:, 2].min(), 2), "top", round(vv[:, 2].max(), 2),
          "halfbeam", round(np.abs(vv[:, 1]).max(), 2), "feet", [tuple(round(c, 2) for c in f) for f in feet], "arm_clash_verts", clash, flush=True)
    piv = np.array((0, 0, PIVOT_Z)); worst = 1e9
    for deg in np.arange(-MAX_SWING, MAX_SWING + 0.1, 2.5):
        r = math.radians(deg); c_, s2 = math.cos(r), math.sin(r)
        p = vv - piv; z = -p[:, 0] * s2 + p[:, 2] * c_ + PIVOT_Z
        worst = min(worst, z.min())
    print("MESHY lowest point over +-%d deg swing: %.2f m (floor %.2f, clearance %.2f)" % (MAX_SWING, worst, PF, worst - PF), flush=True)
    return h

# ---------------------------------------------------------------- 2. frame (two A-frames, static)
def build_frame():
    P = "VK_Frame"
    for s in (-1, 1):
        y = s * FRAME_Y
        apex = Vector((0, y, PIVOT_Z + 0.2))
        for sx in (-1, 1):
            foot = Vector((sx * FOOT_X, y, PF + 0.8))
            d = (apex - foot).normalized()
            n = Vector((0, 1, 0))
            # ladder leg: two purple rails (in the frame plane, across x) + red rungs + red inner rail
            perp = d.cross(n).normalized() * 0.42
            for k in (-1, 1):
                bar(P, foot + perp * k, apex + perp * k, 0.42, "purple", bevel=0.05)
            length = (apex - foot).length
            steps = int(length / 1.1)
            for i in range(1, steps):
                q = foot.lerp(apex, i / steps)
                bar(P, q - perp, q + perp, 0.13, "red")
            bar(P, foot + n * s * 0.3, apex + n * s * 0.3, 0.16, "red")                       # outer red rail
            for i in range(1, steps, 2):
                q = foot.lerp(apex, i / steps)
                sphere(P, 0.09, q + perp * 1.12 + n * s * 0.22, "gold", seg=8)
            box(P, (1.8, 1.8, 0.8), (foot.x, y, PF + 0.4), "stone", bevel=0.08)               # foot plinth
            box(P, (1.3, 1.3, 0.25), (foot.x, y, PF + 0.92), "purple", bevel=0.05)
            for cx in (-1, 1):
                for cy in (-1, 1):
                    sphere(P, 0.11, (foot.x + cx * 0.8, y + cy * 0.8, PF + 0.82), "gold", seg=8)
        bar(P, (-FOOT_X * 0.55, y, PF + 6.0), (FOOT_X * 0.55, y, PF + 6.0), 0.3, "red")        # tie beam
        # bearing head with skull medallion, flame lamp and flag
        box(P, (2.0, 1.6, 2.0), (0, y, PIVOT_Z + 0.3), "purple", bevel=0.12)
        box(P, (2.2, 1.7, 0.22), (0, y, PIVOT_Z - 0.6), "gold", bevel=0.04)
        box(P, (2.2, 1.7, 0.22), (0, y, PIVOT_Z + 1.25), "gold", bevel=0.04)
        spike_emblem(P, (0, y + s * 0.9, PIVOT_Z + 0.35), s * math.pi / 2, 0.75)
        flame_lamp(P, (0.55, y, PIVOT_Z + 1.36), 0.3, 1.0)
        banner(P, (-0.6, y, PIVOT_Z + 1.36), 3.2, 180 if s > 0 else 180)


# ---------------------------------------------------------------- 3. axle (static, through the hubs)
def build_axle():
    P = "VK_Axle"
    cyl(P, 0.42, 2 * FRAME_Y + 1.6, (0, 0, PIVOT_Z), "purple", verts=24, rot=(math.pi / 2, 0, 0), bevel=0.03, smooth=True)
    for y in (-1.2, 0.0, 1.2):
        cyl(P, 0.52, 0.16, (0, y, PIVOT_Z), "gold", verts=24, rot=(math.pi / 2, 0, 0), bevel=0.02)
    spike_emblem(P, (0, 0, PIVOT_Z + 1.25), -math.pi / 2, 0.75)                                 # centre skull above the axle
    box(P, (0.3, 0.3, 0.9), (0, 0, PIVOT_Z + 0.5), "purple", bevel=0.03)
    for s in (-1, 1):
        y = s * (FRAME_Y + 0.8)
        cyl(P, 0.6, 0.25, (0, y, PIVOT_Z), "gold", verts=24, rot=(math.pi / 2, 0, 0), bevel=0.03)
        flame_lamp(P, (0, y + s * 0.15, PIVOT_Z + 0.45), 0.0, 0.75)


# ---------------------------------------------------------------- 4. platform
def outline():
    """Chamfered rectangle (8 corners), counter-clockwise."""
    X, Y, C = PLAT_X, PLAT_Y, CHAMFER
    return [Vector(p) for p in ((X - C, -Y, 0), (X, -Y + C, 0), (X, Y - C, 0), (X - C, Y, 0), (-X + C, Y, 0), (-X, Y - C, 0), (-X, -Y + C, 0), (-X + C, -Y, 0))]


def prism_from_outline(part, pts, z0, z1, mat, grow=0.0, name="slab"):
    c = Vector((0, 0, 0))
    out = [c + (p - c) * 1.0 + (p - c).normalized() * grow for p in pts]
    n = len(out)
    verts = [(p.x, p.y, z0) for p in out] + [(p.x, p.y, z1) for p in out]
    faces = [tuple(range(n))[::-1], tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return mesh_obj(part, name, verts, faces, mat, bevel=0.04)


def build_platform():
    P = "VK_Platform"
    pts = outline()
    prism_from_outline(P, pts, 0.0, 0.3, "stone", grow=0.25, name="Footing")
    prism_from_outline(P, pts, 0.3, PF - 0.2, "stone", name="Wall")
    prism_from_outline(P, pts, PF - 0.25, PF - 0.02, "red", grow=0.15, name="TopBand")
    prism_from_outline(P, pts, PF - 0.3, PF - 0.22, "gold", grow=0.22, name="Trim")
    prism_from_outline(P, pts, PF - 0.03, PF, "deck", grow=0.05, name="Floor")
    # corner pilasters + lamps, railing along the rim (opening for the stairs at -Y)
    n = len(pts)
    stair_half = 2.6
    for i, p in enumerate(pts):
        box(P, (0.8, 0.8, PF + 0.1), (p.x, p.y, (PF + 0.1) / 2), "purple", rot=(0, 0, math.atan2(p.y, p.x)), bevel=0.06)
        inset = p - p.normalized() * 0.45
        flame_lamp(P, (inset.x, inset.y, PF), 1.4, 0.85)
    rail_h = 1.1
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        a = a - a.normalized() * 0.45; b = b - b.normalized() * 0.45
        seg_len = (b - a).length
        k = max(2, int(seg_len / 1.2))
        posts = [a.lerp(b, t / k) for t in range(k + 1)]
        front = abs(a.y + PLAT_Y) < 0.6 and abs(b.y + PLAT_Y) < 0.6
        spans = []
        if front:  # split around the stairs opening
            left = [q for q in posts if q.x < -stair_half] + [Vector((-stair_half, a.y, 0))]
            right = [Vector((stair_half, a.y, 0))] + [q for q in posts if q.x > stair_half]
            spans = [sorted(left, key=lambda q: q.x), sorted(right, key=lambda q: q.x)]
        else:
            spans = [posts]
        for sp in spans:
            for q in sp:
                cyl(P, 0.07, rail_h, (q.x, q.y, PF + rail_h / 2), "red", verts=8, bevel=0.0, smooth=True)
                sphere(P, 0.1, (q.x, q.y, PF + rail_h + 0.06), "gold", seg=8)
            for zz, mat, t in ((PF + rail_h, "red", 0.12), (PF + 0.55, "gold", 0.06)):
                bar(P, (sp[0].x, sp[0].y, zz), (sp[-1].x, sp[-1].y, zz), t, mat, round_=True)
            for q0, q1 in zip(sp, sp[1:]):                                                     # X pattern between posts
                bar(P, (q0.x, q0.y, PF + 0.12), (q1.x, q1.y, PF + rail_h - 0.1), 0.05, "red", round_=True)
    # front wall skull emblems either side of the stairs
    for xe in (-7.0, 7.0):
        spike_emblem(P, (xe, -PLAT_Y - 0.12, 0.7), -math.pi / 2, 0.45)
    for xe in (-11.0, 11.0):
        spike_emblem(P, (xe, PLAT_Y + 0.12, 0.7), math.pi / 2, 0.45)
    # stairs (-Y)
    steps = int(round(PF / 0.3))
    rise = PF / steps
    for k in range(1, steps + 1):
        top = PF - rise * k
        y1 = -PLAT_Y - 0.45 * (k - 1)
        if top <= 0.01: break
        box(P, (2 * stair_half - 0.2, 0.45, top), (0, y1 - 0.225, top / 2), "red", bevel=0.03)
    y_end = -PLAT_Y - 0.45 * steps
    for s in (-1, 1):
        x = s * stair_half
        bar(P, (x, -PLAT_Y, PF + 1.0), (x, y_end, 1.0), 0.1, "red", round_=True)
        bar(P, (x, -PLAT_Y, PF + 0.5), (x, y_end, 0.5), 0.05, "gold", round_=True)
        for t in (0.0, 0.5, 1.0):
            q = Vector((x, -PLAT_Y, PF)).lerp(Vector((x, y_end, 0)), t)
            cyl(P, 0.07, 1.0, (q.x, q.y, q.z + 0.5), "red", verts=8, bevel=0.0, smooth=True)
        box(P, (0.6, 0.6, 1.3), (x, y_end - 0.1, 0.65), "purple", bevel=0.05)
        flame_lamp(P, (x, y_end - 0.1, 1.3), 0.0, 0.7)
    # barrels by the stairs
    for bx, by in ((-4.3, -PLAT_Y - 1.2), (-5.4, -PLAT_Y - 0.9), (4.6, -PLAT_Y - 1.1)):
        cyl(P, 0.5, 1.1, (bx, by, 0.55), "barrel", verts=16, bevel=0.06, smooth=True)
        for zz in (0.2, 0.9):
            cyl(P, 0.53, 0.08, (bx, by, zz), "gold", verts=16, bevel=0.0)
    # tall banners at the back corners
    for x in (-PLAT_X + 1.5, PLAT_X - 1.5):
        banner(P, (x, PLAT_Y - 1.0, PF), 6.0, 90 if x < 0 else 90)


hull = build_ship_meshy(); build_frame(); build_axle(); build_platform()
ship = rc.finish("VK_Ship", (0, 0, PIVOT_Z))
hull.parent = ship; hull.matrix_parent_inverse = ship.matrix_world.inverted()
frame = rc.finish("VK_Frame", (0, 0, 0))
axle = rc.finish("VK_Axle", (0, 0, PIVOT_Z))
plat = rc.finish("VK_Platform", (0, 0, 0))
allp = [plat, frame, axle, ship, hull]
counts = {o.name: rc.tris(o) for o in allp}
print("TRIS", counts, flush=True)

# pendulum: amplitude ramps up over 4 swings, holds 2, ramps down 2 (30 fps, key every 3 frames)
fps = 30
rc.scene.render.fps = fps
cycles = 8
frames = int(cycles * PERIOD * fps)
for f in range(0, frames + 1, 3):
    t = f / fps
    k = t / PERIOD
    amp = MAX_SWING * (min(1.0, k / 4) if k < 6 else max(0.0, 1 - (k - 6) / 2))
    ship.rotation_euler.y = math.radians(amp * math.sin(2 * math.pi * k))
    ship.keyframe_insert("rotation_euler", index=1, frame=f + 1)
rc.scene.frame_start, rc.scene.frame_end = 1, frames + 1
rc.scene.frame_set(1)
ship.rotation_euler.y = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "VK_Viking.blend"))

for o in (plat, frame, axle):
    rc.export([o], os.path.join(OUT, o.name + ".fbx"))
rc.export(allp, os.path.join(OUT, "VK_Viking.fbx"))
rc.export([ship, hull], os.path.join(OUT, "VK_Ship.fbx"))   # arms + hull child in one prefab
json.dump(dict(platform_floor=PF, pivot_height=PIVOT_Z, arm_length=ARM_L, ship_length=SHIP_L, beam=BEAM,
               max_swing_deg=MAX_SWING, period_s=PERIOD, seats=20, tris=counts,
               note="Ship origin = pivot on the axle; swing = rotation about Blender Y (Unity X after export). Stairs at -Y (front)."),
          open(os.path.join(OUT, "viking.json"), "w"), indent=1)

if not RENDER:
    sys.exit(0)
ship.animation_data_clear()   # the saved .blend keeps the swing; stills set the angle by hand
st = rc.Stage(OUT, allp)
ship.rotation_euler.y = 0
st.shoot("vk_part_ship", [ship, hull], 18, 35, frame_objs=None)
st.ground.hide_render = True
st.shoot("vk_hull_under", [hull], -18, 30, res=(1200, 700))
st.shoot("vk_hull_under_side", [hull], -4, 0, res=(1200, 600))
st.ground.hide_render = False
st.shoot("vk_part_frame", [frame, axle], 14, 30)
st.shoot("vk_part_platform", [plat], 26, 25)
st.shoot("vk_part_axle", [axle], 12, 20, res=(1100, 700))
for name, ang in (("vk_assembled_0", 0.0), ("vk_assembled_swing", 45.0), ("vk_assembled_max", -MAX_SWING)):
    ship.rotation_euler.y = math.radians(ang)
    st.shoot(name, allp, 14, 32, res=(1300, 1000), fill=1.05)
ship.rotation_euler.y = 0
st.shoot("vk_front", allp, 6, 0, res=(1300, 1000), fill=1.05)
st.shoot("vk_bow", [ship, hull], 12, 70, res=(1100, 900), fill=0.55)
print("DONE", flush=True)
