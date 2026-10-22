# Gyro drop ride for the Cursed Candy Carnival (2026-10-22). Builds the four parts from primitives —
# tower (pillar), ride ring (rotating seats), crown (top decoration), platform (with stairs) — textures them with the
# GD_*.png tiles (box-projected UVs, world scale), assembles them, keys a ride cycle on the ring (rise while turning ->
# hold -> free fall -> brake), exports FBX for Unity and renders part / assembly / ride-height pictures.
# Blender z up, metres. Ride ring origin = ring centre, moves along z only (Unity: local Y).
# python gyro_build.py -- <tex dir> <out dir> [render: 1|0]
import bpy, bmesh, sys, os, math, json
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
TEX, OUT = os.path.abspath(argv[0]), os.path.abspath(argv[1])
RENDER = len(argv) < 3 or argv[2] != "0"
os.makedirs(OUT, exist_ok=True)

# ---------------------------------------------------------------- dimensions (m)
PLAT_R, PLAT_H = 11.0, 3.0          # platform radius, floor height
TOWER_TOP = 38.0                    # top of the pillars
PILLAR = 1.35                       # pillar centre offset from the axis
RING_IN, RING_OUT = 2.6, 7.4        # ride ring inner / outer radius
RING_LOW, RING_HIGH = 4.4, 29.5     # ring origin heights (boarding, top of the lift)
CROWN_Z = 38.6
SEATS = 16
STAIR_W, STAIR_STEPS, STAIR_RISE, STAIR_RUN = 4.0, 10, 0.3, 0.45

# ---------------------------------------------------------------- scene
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, tex=None, color=(1, 1, 1), metallic=0.0, rough=0.6, emission=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if tex:
        img = bpy.data.images.load(os.path.join(TEX, tex + ".png"))
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = img
        nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*color, 1)
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1)
        bsdf.inputs["Emission Strength"].default_value = strength
    return m


MAT = {
    "stone": material("GD_PurpleStone", "GD_PurpleStone", rough=0.85),
    "floor": material("GD_FloorTiles", "GD_FloorTiles", rough=0.8),
    "red": material("GD_RedMetal", "GD_RedMetal", metallic=0.3, rough=0.4),
    "purple": material("GD_PurpleMetal", "GD_PurpleMetal", metallic=0.3, rough=0.45),
    "gold": material("GD_Gold", "GD_Gold", metallic=0.9, rough=0.3),
    "pad": material("GD_BlackPad", "GD_BlackPad", rough=0.7),
    "seat": material("GD_RedSeat", "GD_RedSeat", rough=0.5),
    "flag": material("GD_Flag", "GD_Flag", rough=0.8),
    "emblem": material("GD_Emblem", "GD_Emblem", rough=0.5),
    "skull": material("GD_Skull", "GD_Skull", rough=0.5),
    "bulb": material("GD_Bulb", None, (1.0, 0.75, 0.3), rough=0.3, emission=(1.0, 0.62, 0.2), strength=12.0),
}
UV_SIZE = {"stone": 2.0, "floor": 3.5, "red": 2.0, "purple": 2.0, "gold": 1.0, "pad": 0.8, "seat": 0.8, "bulb": 1.0}

parts = {}  # part name -> list of objects


def add(part, obj, mat, bevel=0.0, uv="box"):
    obj.data.materials.clear()
    obj.data.materials.append(MAT[mat])
    obj["gd_mat"] = mat
    obj["gd_uv"] = uv
    if bevel > 0:
        b = obj.modifiers.new("bevel", "BEVEL")
        b.width = bevel; b.segments = 2; b.limit_method = "ANGLE"
    parts.setdefault(part, []).append(obj)
    return obj


def active():
    return bpy.context.active_object


def box(part, size, loc, mat, rot=(0, 0, 0), bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = active(); o.scale = size
    return add(part, o, mat, bevel)


def cyl(part, r, depth, loc, mat, verts=24, rot=(0, 0, 0), bevel=0.03, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=rot)
    return add(part, active(), mat, bevel)


def sphere(part, r, loc, mat, seg=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=r, location=loc)
    o = active()
    for p in o.data.polygons: p.use_smooth = True
    return add(part, o, mat)


def torus(part, R, r, loc, mat, rot=(0, 0, 0), seg=32):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=8, location=loc, rotation=rot)
    o = active()
    for p in o.data.polygons: p.use_smooth = True
    return add(part, o, mat)


def annulus(part, r_in, r_out, z0, z1, mat, segs=48, name="annulus", bevel=0.03):
    """Thick ring (hole in the middle) as a closed quad mesh."""
    bm = bmesh.new()
    rings = [[], [], [], []]
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c, s = math.cos(a), math.sin(a)
        rings[0].append(bm.verts.new((r_out * c, r_out * s, z0)))
        rings[1].append(bm.verts.new((r_out * c, r_out * s, z1)))
        rings[2].append(bm.verts.new((r_in * c, r_in * s, z1)))
        rings[3].append(bm.verts.new((r_in * c, r_in * s, z0)))
    for i in range(segs):
        j = (i + 1) % segs
        for k in range(4):
            a, b = rings[k], rings[(k + 1) % 4]
            bm.faces.new((a[i], a[j], b[j], b[i]))
    me = bpy.data.meshes.new(name)
    bm.normal_update(); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me)
    scene.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    me.validate()
    # outward normals
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    return add(part, o, mat, bevel)


def prism(part, profile_yz, x0, x1, mat, name="prism"):
    """Extrude a y-z polygon along x (stair side walls)."""
    bm = bmesh.new()
    a = [bm.verts.new((x0, y, z)) for y, z in profile_yz]
    b = [bm.verts.new((x1, y, z)) for y, z in profile_yz]
    bm.faces.new(a); bm.faces.new(list(reversed(b)))
    n = len(a)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], b[i], b[j], a[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); scene.collection.objects.link(o)
    return add(part, o, mat, 0.03)


def bar(part, p0, p1, thick, mat, round_=False):
    """Beam between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    mid = (p0 + p1) / 2
    rot = d.to_track_quat("Z", "Y").to_euler()
    if round_:
        o = cyl(part, thick / 2, d.length, mid, mat, verts=10, rot=rot, bevel=0.0)
    else:
        o = box(part, (thick, thick, d.length), mid, mat, rot=rot, bevel=0.015)
    return o


def flag(part, base, height, yaw_deg):
    """Pole + gold ball + red skull flag (plane, UV 0..1)."""
    x, y, z = base
    cyl(part, 0.06, height, (x, y, z + height / 2), "gold", verts=10, bevel=0.0)
    sphere(part, 0.13, (x, y, z + height + 0.08), "gold", seg=10)
    w, h = 1.3, 0.75
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0))
    o = active()
    o.scale = (w, h, 1)
    o.rotation_euler = (math.pi / 2, 0, math.radians(yaw_deg))
    bpy.context.view_layer.update()
    d = Vector((math.cos(math.radians(yaw_deg)), math.sin(math.radians(yaw_deg)), 0))
    o.location = Vector((x, y, z + height - h / 2 - 0.05)) + d * (w / 2 + 0.05)
    sol = o.modifiers.new("solid", "SOLIDIFY"); sol.thickness = 0.03
    return add(part, o, "flag", uv="keep")


def ring_of(n, r, offset_deg=0.0, skip=None):
    for i in range(n):
        a = math.radians(offset_deg + i * 360.0 / n)
        if skip and skip(a): continue
        yield a, Vector((r * math.cos(a), r * math.sin(a), 0))


def medallion(part, center, normal_yaw, r, mat):
    """Disc facing outward (yaw), front face UV = whole image, gold ring around."""
    rot = (math.pi / 2, 0, normal_yaw + math.pi / 2)
    o = cyl(part, r, 0.16, center, mat, verts=32, rot=rot, bevel=0.0)
    o["gd_uv"] = "disc"; o["gd_disc_r"] = r
    torus(part, r, 0.09, center, "gold", rot=rot, seg=32)
    return o


# ---------------------------------------------------------------- 1. platform
def build_platform():
    P = "GD_Platform"
    stairs_dir = -math.pi / 2  # stairs at -Y (front)
    near_stairs = lambda a: abs(math.atan2(math.sin(a - stairs_dir), math.cos(a - stairs_dir))) < math.radians(13)
    cyl(P, PLAT_R + 0.25, 0.4, (0, 0, 0.2), "stone", verts=12, bevel=0.06)                  # footing
    cyl(P, PLAT_R, PLAT_H - 0.45, (0, 0, 0.4 + (PLAT_H - 0.45) / 2 - 0.05), "stone", verts=12, bevel=0.05)
    cyl(P, PLAT_R + 0.18, 0.35, (0, 0, PLAT_H - 0.25), "red", verts=12, bevel=0.04)          # top band
    cyl(P, PLAT_R + 0.26, 0.1, (0, 0, PLAT_H - 0.45), "gold", verts=12, bevel=0.02)
    cyl(P, PLAT_R + 0.3, 0.1, (0, 0, 0.42), "gold", verts=12, bevel=0.02)
    floor = cyl(P, PLAT_R + 0.05, 0.06, (0, 0, PLAT_H - 0.02), "floor", verts=12, bevel=0.0)
    # pilasters at the 12 corners + medallions on four faces
    for a, p in ring_of(12, PLAT_R + 0.05, 0.0):
        box(P, (0.75, 0.75, PLAT_H - 0.5), (p.x, p.y, 0.4 + (PLAT_H - 0.5) / 2), "purple", rot=(0, 0, a), bevel=0.06)
        sphere(P, 0.16, (p.x * 1.03, p.y * 1.03, 1.6), "bulb", seg=10)
    for k, deg in enumerate((15, 75, 135, 195, 315, 255)):
        a = math.radians(deg)
        if near_stairs(a): continue
        apothem = PLAT_R * math.cos(math.pi / 12)
        c = Vector((math.cos(a), math.sin(a), 0)) * (apothem + 0.12)
        medallion(P, (c.x, c.y, 1.45), a, 0.85, "emblem")
    # railing on the rim (opening at the stairs)
    rail_r, rail_h = PLAT_R - 0.35, 1.15
    posts = [(a, p) for a, p in ring_of(48, rail_r, 0.0, skip=near_stairs)]
    for a, p in posts:
        cyl(P, 0.06, rail_h, (p.x, p.y, PLAT_H + rail_h / 2), "red", verts=8, bevel=0.0)
        sphere(P, 0.09, (p.x, p.y, PLAT_H + rail_h + 0.05), "gold", seg=8)
    for (a0, p0), (a1, p1) in zip(posts, posts[1:] + posts[:1]):
        if abs(math.atan2(math.sin(a1 - a0), math.cos(a1 - a0))) > math.radians(9): continue  # the stairs gap
        for zz, mat, t in ((PLAT_H + rail_h, "red", 0.1), (PLAT_H + 0.55, "gold", 0.06)):
            bar(P, (p0.x, p0.y, zz), (p1.x, p1.y, zz), t, mat, round_=True)
    # big corner pillars with flags (six, away from the stairs)
    for deg in (30, 90, 150, 210, 330, 270 + 40, 270 - 40):
        a = math.radians(deg)
        if near_stairs(a): continue
        p = Vector((math.cos(a), math.sin(a), 0)) * (PLAT_R - 0.35)
        box(P, (0.5, 0.5, 1.7), (p.x, p.y, PLAT_H + 0.85), "purple", rot=(0, 0, a), bevel=0.05)
        cyl(P, 0.3, 0.7, (p.x, p.y, PLAT_H + 2.05), "gold", verts=8, r2=0.0, bevel=0.0)
        flag(P, (p.x, p.y, PLAT_H + 1.7), 2.4, deg + 90)
    # stairs (-Y): solid blocks, side walls, handrails
    y_edge = -(PLAT_R * math.cos(math.pi / 12)) + 0.3
    for k in range(1, STAIR_STEPS + 1):
        top = PLAT_H - STAIR_RISE * k
        y1 = y_edge - STAIR_RUN * (k - 1)
        y0 = y1 - STAIR_RUN
        if top <= 0: break
        box(P, (STAIR_W, STAIR_RUN, top), (0, (y0 + y1) / 2, top / 2), "red", bevel=0.03)
    y_end = y_edge - STAIR_RUN * STAIR_STEPS
    for s in (-1, 1):
        x0 = s * STAIR_W / 2
        prism(P, [(y_edge + 0.4, 0), (y_edge + 0.4, PLAT_H + 0.5), (y_end, 0.6), (y_end, 0)], x0, x0 + s * 0.45, "stone")
        top0 = Vector((x0 + s * 0.22, y_edge + 0.2, PLAT_H + 0.5))
        top1 = Vector((x0 + s * 0.22, y_end + 0.2, 0.6))
        for t in (0.0, 0.25, 0.5, 0.75, 1.0):
            q = top0.lerp(top1, t)
            cyl(P, 0.05, 1.0, (q.x, q.y, q.z + 0.5), "red", verts=8, bevel=0.0)
            sphere(P, 0.08, (q.x, q.y, q.z + 1.02), "gold", seg=8)
        bar(P, top0 + Vector((0, 0, 1.0)), top1 + Vector((0, 0, 1.0)), 0.09, "red", round_=True)
        bar(P, top0 + Vector((0, 0, 0.5)), top1 + Vector((0, 0, 0.5)), 0.05, "gold", round_=True)
        cyl(P, 0.3, 1.4, (top1.x, top1.y - 0.1, 1.3), "purple", verts=8, bevel=0.03)       # newel post
        cyl(P, 0.22, 0.6, (top1.x, top1.y - 0.1, 2.3), "gold", verts=8, r2=0.0, bevel=0.0)
        flag(P, (top1.x, top1.y - 0.1, 2.0), 2.0, -90 + s * 60)


# ---------------------------------------------------------------- 2. tower
def build_tower():
    P = "GD_Tower"
    z0 = PLAT_H
    box(P, (4.2, 4.2, 0.45), (0, 0, z0 + 0.225), "stone", bevel=0.06)                        # plinth tier 1
    box(P, (3.4, 3.4, 0.95), (0, 0, z0 + 0.45 + 0.475), "stone", bevel=0.06)                 # tier 2 (inside the ring hole)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(P, (0.9, 0.9, 0.6), (sx * 1.75, sy * 1.75, z0 + 0.75), "purple", bevel=0.05)   # corner feet
            sphere(P, 0.12, (sx * 2.12, sy * 2.12, z0 + 0.3), "gold", seg=8)
    base, top = z0 + 1.4, TOWER_TOP
    h = top - base
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(P, (0.55, 0.55, h), (sx * PILLAR, sy * PILLAR, base + h / 2), "purple", bevel=0.06)
    box(P, (1.7, 1.7, h), (0, 0, base + h / 2), "red", bevel=0.05)                           # red core
    for k in range(4):                                                                       # guide rails (ring rollers)
        a = k * math.pi / 2
        box(P, (0.22, 0.32, h), (math.cos(a) * 1.0, math.sin(a) * 1.0, base + h / 2), "gold", rot=(0, 0, a), bevel=0.02)
    bay = 2.8
    n = int(h / bay)
    bay = h / n
    for face in range(4):
        a = face * math.pi / 2
        d = Vector((math.cos(a), math.sin(a), 0)); t = Vector((-d.y, d.x, 0))
        off = d * PILLAR
        for i in range(n):
            za, zb = base + i * bay, base + (i + 1) * bay
            pa, pb = off - t * PILLAR, off + t * PILLAR
            bar(P, (pa.x, pa.y, za), (pb.x, pb.y, zb), 0.13, "red")
            bar(P, (pb.x, pb.y, za), (pa.x, pa.y, zb), 0.13, "red")
            bar(P, (pa.x, pa.y, za), (pb.x, pb.y, za), 0.16, "red")
    box(P, (3.6, 3.6, 0.6), (0, 0, top + 0.3), "purple", bevel=0.06)                          # cap
    for sx in (-1, 1):
        for sy in (-1, 1):
            sphere(P, 0.14, (sx * 1.75, sy * 1.75, top + 0.35), "gold", seg=8)


# ---------------------------------------------------------------- 3. ride ring (origin = ring centre)
def build_ring():
    P = "GD_Ring"
    annulus(P, RING_IN, RING_OUT - 0.3, -0.3, 0.1, "purple", name="RingPlate")
    annulus(P, RING_OUT - 0.4, RING_OUT, -0.5, 0.3, "red", name="RingRim")
    annulus(P, RING_OUT - 0.05, RING_OUT + 0.12, -0.12, 0.05, "gold", name="RingTrim", bevel=0.02)
    annulus(P, RING_IN, 5.6, -0.95, -0.3, "purple", name="RingSkirt")
    annulus(P, 5.55, 5.75, -0.98, -0.85, "gold", name="SkirtTrim", bevel=0.02)
    annulus(P, RING_IN, RING_IN + 0.55, -0.95, 1.7, "purple", name="Collar")
    annulus(P, RING_IN + 0.5, RING_IN + 0.65, 0.9, 1.25, "red", name="CollarBand", bevel=0.02)
    annulus(P, RING_IN + 0.5, RING_IN + 0.62, 1.6, 1.72, "gold", name="CollarTop", bevel=0.02)
    for a, p in ring_of(24, RING_OUT - 0.25, 7.5):                                           # spikes under the rim
        cyl(P, 0.2, 0.75, (p.x, p.y, -0.85), "gold", verts=8, r2=0.0, rot=(math.pi, 0, 0), bevel=0.0)
    for a, p in ring_of(32, RING_OUT + 0.08, 0.0):
        sphere(P, 0.13, (p.x, p.y, 0.12), "bulb", seg=10)
    for a, p in ring_of(16, 5.65, 11.25):
        sphere(P, 0.12, (p.x, p.y, -0.92), "bulb", seg=10)
    for a, p in ring_of(12, RING_IN + 0.56, 0.0):
        sphere(P, 0.09, (p.x, p.y, 0.4), "gold", seg=8)
    # seats facing outward (16 x 1.6 m wide around r 5.3 — packed like the reference)
    for i, (a, p) in enumerate(ring_of(SEATS, 5.25, 0.0)):
        d = Vector((math.cos(a), math.sin(a), 0)); rot = (0, 0, a - math.pi / 2)
        c = d * 5.55
        box(P, (1.45, 1.15, 0.55), (c.x, c.y, 0.38), "seat", rot=rot, bevel=0.12)              # seat cushion
        b = d * 4.85
        box(P, (1.45, 0.4, 2.1), (b.x, b.y, 1.15), "seat", rot=rot, bevel=0.14)               # back
        pad = d * 5.08
        box(P, (1.1, 0.1, 1.55), (pad.x, pad.y, 1.25), "pad", rot=rot, bevel=0.04)            # back padding
        cushion = d * 5.6
        box(P, (1.1, 0.95, 0.1), (cushion.x, cushion.y, 0.68), "pad", rot=rot, bevel=0.03)
        hz = d * 5.35
        # over-shoulder harness: black inverted U in front of the rider (hoop faces outward)
        torus(P, 0.55, 0.11, (hz.x, hz.y, 1.75), "pad", rot=(math.pi / 2, 0, a + math.pi / 2), seg=24)
        sphere(P, 0.12, (hz.x, hz.y, 2.32), "gold", seg=8)
        # dividers between seats
        m = a + math.pi / SEATS
        dm = Vector((math.cos(m), math.sin(m), 0)) * 5.3
        box(P, (0.16, 1.5, 1.6), (dm.x, dm.y, 0.9), "red", rot=(0, 0, m - math.pi / 2), bevel=0.05)
        sphere(P, 0.1, (dm.x, dm.y, 1.75), "gold", seg=8)


# ---------------------------------------------------------------- 4. crown (origin = its base, sits on the tower cap)
def build_crown():
    P = "GD_Crown"
    cyl(P, 2.1, 1.3, (0, 0, 0.65), "purple", verts=16, r2=4.4, bevel=0.04)                   # flared neck
    cyl(P, 4.3, 2.0, (0, 0, 2.3), "stone", verts=16, bevel=0.05)                             # drum
    cyl(P, 4.42, 0.45, (0, 0, 2.3), "red", verts=16, bevel=0.03)
    cyl(P, 4.5, 0.12, (0, 0, 1.33), "gold", verts=16, bevel=0.02)
    cyl(P, 4.5, 0.12, (0, 0, 3.3), "gold", verts=16, bevel=0.02)
    for a, p in ring_of(16, 4.25, 11.25):
        cyl(P, 0.22, 0.8, (p.x, p.y, 0.95), "gold", verts=8, r2=0.0, rot=(math.pi, 0, 0), bevel=0.0)
    for a, p in ring_of(24, 4.52, 0.0):
        sphere(P, 0.13, (p.x, p.y, 1.6), "bulb", seg=10)
        sphere(P, 0.11, (p.x, p.y, 3.05), "bulb", seg=10)
    for a, p in ring_of(12, 3.95, 15.0):                                                     # battlement
        box(P, (0.8, 0.55, 0.7), (p.x, p.y, 3.65), "purple", rot=(0, 0, a + math.pi / 2), bevel=0.05)
        cyl(P, 0.16, 0.45, (p.x, p.y, 4.2), "gold", verts=8, r2=0.0, bevel=0.0)
    cyl(P, 3.5, 6.0, (0, 0, 3.3 + 3.0), "purple", verts=16, r2=0.15, bevel=0.03)            # roof
    cyl(P, 3.55, 0.25, (0, 0, 3.45), "gold", verts=16, bevel=0.02)
    cyl(P, 0.4, 2.2, (0, 0, 9.3 + 1.1), "gold", verts=12, r2=0.0, bevel=0.0)                 # spire
    sphere(P, 0.42, (0, 0, 9.4), "gold", seg=12)
    medallion(P, (0, -2.95, 5.0), -math.pi / 2, 1.2, "skull")                                # front skull
    for a, p in ring_of(4, 3.7, 45.0):                                                       # turrets
        cyl(P, 0.5, 1.6, (p.x, p.y, 4.1), "purple", verts=12, bevel=0.03)
        cyl(P, 0.62, 1.4, (p.x, p.y, 5.6), "red", verts=12, r2=0.0, bevel=0.02)
        sphere(P, 0.14, (p.x, p.y, 6.35), "gold", seg=8)
        flag(P, (p.x, p.y, 6.3), 1.6, math.degrees(a) + 90)
    for deg in (-60, -120):
        a = math.radians(deg)
        p = Vector((math.cos(a), math.sin(a), 0)) * 3.0
        flag(P, (p.x, p.y, 4.0), 4.2, deg + 90)


# ---------------------------------------------------------------- finish: apply, UV, join
def uv_box(o, size):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")  # one named layer for every piece (joined meshes keep a single UV set)
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            if ax == 0: u, v = p.y, p.z
            elif ax == 1: u, v = p.x, p.z
            else: u, v = p.x, p.y
            l[uv].uv = (u / size, v / size)
    bm.to_mesh(me); bm.free()


def uv_disc(o, r):
    """Front/back caps get the whole image; the edge gets a thin strip of the rim colour."""
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")  # one named layer for every piece (joined meshes keep a single UV set)
    center = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    normals = [f.normal for f in bm.faces]
    cap = max(bm.faces, key=lambda f: f.calc_area()).normal.normalized()
    side = cap.orthogonal().normalized(); up = cap.cross(side).normalized()
    for f in bm.faces:
        for l in f.loops:
            d = l.vert.co - center
            l[uv].uv = (0.5 + d.dot(side) / (2 * r), 0.5 + d.dot(up) / (2 * r))
    bm.to_mesh(me); bm.free()


def finish(part, origin):
    objs = parts[part]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target="MESH")                       # applies modifiers
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in objs:
        mode = o.get("gd_uv", "box")
        if mode == "box": uv_box(o, UV_SIZE.get(o["gd_mat"], 2.0))
        elif mode == "disc": uv_disc(o, o["gd_disc_r"])
    for ob in objs:                                    # pieces without UVs get an empty UVMap so join keeps one set
        if "UVMap" not in ob.data.uv_layers: ob.data.uv_layers.new(name="UVMap")
        for l in [l for l in ob.data.uv_layers if l.name != "UVMap"]: ob.data.uv_layers.remove(l)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = part; o.data.name = part
    o.data.uv_layers.active = o.data.uv_layers["UVMap"]; o.data.uv_layers["UVMap"].active_render = True
    bm = bmesh.new(); bm.from_mesh(o.data)                       # caps (n-gons) -> triangles, so tangents export
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(o.data); bm.free()
    # origin
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = (0, 0, 0)
    return o


build_platform()
build_tower()
build_ring()
build_crown()
plat = finish("GD_Platform", (0, 0, 0))
tower = finish("GD_Tower", (0, 0, PLAT_H))
ring = finish("GD_Ring", (0, 0, 0))
crown = finish("GD_Crown", (0, 0, 0))
ring.location = (0, 0, RING_LOW)
crown.location = (0, 0, CROWN_Z)
tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in (plat, tower, ring, crown)}

# ride cycle on the ring (30 fps): rise while turning 10 s -> hold 2 s -> free fall 0.9 s -> brake 1.2 s -> board 2 s
scene.render.fps = 30
keys = [(1, RING_LOW, 0.0, "SINE"), (301, RING_HIGH, 360.0, "LINEAR"), (361, RING_HIGH, 360.0, "QUAD"),
        (388, 10.0, 360.0, "SINE"), (424, RING_LOW, 360.0, "LINEAR"), (484, RING_LOW, 360.0, "LINEAR")]
for f, z, yaw, interp in keys:
    ring.location.z = z; ring.rotation_euler.z = math.radians(yaw)
    ring.keyframe_insert("location", index=2, frame=f); ring.keyframe_insert("rotation_euler", index=2, frame=f)
fc_all = ring.animation_data.action.fcurves if hasattr(ring.animation_data.action, "fcurves") else []
for fc in fc_all:
    for kp, (f, z, yaw, interp) in zip(fc.keyframe_points, keys):
        kp.interpolation = "QUAD" if interp == "QUAD" else ("SINE" if interp == "SINE" else "LINEAR")
        if interp == "QUAD": kp.easing = "EASE_IN"
        if interp == "SINE": kp.easing = "EASE_OUT"
scene.frame_start, scene.frame_end = 1, 484
scene.frame_set(1)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "GD_GyroDrop.blend"))


# ---------------------------------------------------------------- export (Unity: same settings as the coaster FBX)
def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", embed_textures=False)


scene.frame_set(1)
for o in (plat, tower, ring, crown):
    export([o], os.path.join(OUT, o.name + ".fbx"))
export([plat, tower, ring, crown], os.path.join(OUT, "GD_GyroDrop.fbx"))
meta = dict(platform_floor=PLAT_H, tower_top=TOWER_TOP, crown_base=CROWN_Z, ring_low=RING_LOW, ring_high=RING_HIGH,
            ring_inner=RING_IN, ring_outer=RING_OUT, seats=SEATS, platform_radius=PLAT_R,
            cycle_seconds=dict(rise=10.0, hold=2.0, fall=0.9, brake=1.2, board=2.0), tris=tris,
            note="Blender z up. Unity: ring origin = ring centre, move along local Y between ring_low and ring_high; front (stairs) = -Y in Blender.")
json.dump(meta, open(os.path.join(OUT, "gyro_drop.json"), "w"), indent=1)
print("TRIS", tris)

if not RENDER:
    sys.exit(0)

# ---------------------------------------------------------------- renders
sc = scene
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = 48
sc.cycles.use_denoising = True
try:
    sc.cycles.denoiser = "OPENIMAGEDENOISE"
except Exception:
    pass
sc.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in sc.view_settings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
sc.view_settings.look = "None"
sc.view_settings.exposure = 0.6
world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.024, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0


def light(name, kind, loc, energy, color, size=4.0, rot=None):
    l = bpy.data.lights.new(name, kind); l.energy = energy; l.color = color
    if kind == "AREA": l.size = size
    o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.location = loc
    if rot is None:
        o.rotation_euler = (Vector((0, 0, 15)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    return o


light("Key", "AREA", (-28, -40, 45), 60000, (1.0, 0.85, 0.75), 18)
light("Rim", "AREA", (35, 30, 30), 30000, (0.65, 0.45, 1.0), 16)
light("Fill", "AREA", (40, -30, 8), 9000, (1.0, 0.55, 0.4), 12)
ground = None
bpy.ops.mesh.primitive_plane_add(size=400, location=(0, 0, -0.01)); ground = active()
gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.022, 0.03, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
ground.data.materials.append(gm)
cam_data = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_data); sc.collection.objects.link(cam); sc.camera = cam


def world_bbox(objs):
    lo = Vector((1e9,) * 3); hi = -lo
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def shoot(name, visible, elev, azim, res=(1100, 1100), fill=1.12, lens=50):
    for o in (plat, tower, ring, crown):
        o.hide_render = o not in visible
    bpy.context.view_layer.update()
    lo, hi = world_bbox(visible)
    ground.location.z = lo.z - 0.01                                  # stand the part on the floor
    c = (lo + hi) / 2
    w, h = res
    sc.render.resolution_x, sc.render.resolution_y = res
    cam_data.lens = lens
    cam_data.sensor_fit = "AUTO"
    half = math.atan(18.0 / lens)                                     # half fov on the longer side
    half_h = half if w >= h else math.atan(math.tan(half) * w / h)
    half_v = half if h >= w else math.atan(math.tan(half) * h / w)
    e = math.radians(elev)
    span_h = max(hi.x - lo.x, hi.y - lo.y) / 2 * fill
    span_v = ((hi.z - lo.z) * math.cos(e) + max(hi.x - lo.x, hi.y - lo.y) * math.sin(e)) / 2 * fill
    dist = max(span_h / math.tan(half_h), span_v / math.tan(half_v)) + max(hi.x - lo.x, hi.y - lo.y) / 2
    d = Vector((math.cos(e) * math.sin(math.radians(azim)), -math.cos(e) * math.cos(math.radians(azim)), math.sin(e)))
    cam.location = c + d * dist
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = dist * 4
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", name, flush=True)


ring.animation_data_clear()   # the saved .blend keeps the ride cycle; stills set the height by hand
ring.location.z = RING_LOW
shoot("part_tower", [tower], 12, 30, res=(700, 1300), fill=1.05)
ring.location.z = 0.0
shoot("part_ring", [ring], 28, 30)
shoot("part_ring_top", [ring], 89, 0, res=(900, 900))
crown.location.z = 0.0
shoot("part_crown", [crown], 14, 30)
crown.location.z = CROWN_Z
shoot("part_platform", [plat], 24, 25)
for label, z in (("assembled_low", RING_LOW), ("assembled_mid", (RING_LOW + RING_HIGH) / 2), ("assembled_high", RING_HIGH)):
    ring.location.z = z
    shoot(label, [plat, tower, ring, crown], 10, 28, res=(800, 1300), fill=1.0)
ring.location.z = RING_LOW
print("DONE")
