# Floor tile analysis (2026-10-21): original piece (uniform scale, original thickness and detail) next to the in-game
# version made by cf_build.build_tile / build_moss_tile (non-uniform squash + decimate). Renders to _split/cmp_<set>_<view>.jpg.
# blender -b --factory-startup --python cf_tilecompare.py -- <Source~ dir>
import bpy, sys, os, math
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SPLIT = os.path.join(argv[0], "_split")
CASES = [  # set, index, in-game footprint (x, y), in-game thickness or z factor, decimate target
    ("Tiles", 1, (6.0, 10.0), ("thick", 0.3), 500),
    ("Tiles", 2, (6.0, 10.0), ("thick", 0.3), 500),
    ("GreenFloor", 0, (10.0, None), ("zfac", 0.45), 900),
]


def load(setname, idx, name):
    with bpy.data.libraries.load(os.path.join(SPLIT, setname + ".blend"), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n == "%s_%02d" % (setname, idx)]
    o = dst.objects[0]
    bpy.context.scene.collection.objects.link(o)
    o.data = o.data.copy()
    o.name = name
    return o


def bbox(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def decimate(o, target):
    md = o.modifiers.new("dec", "DECIMATE")
    md.ratio = max(0.002, target / tris(o))
    md.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=md.name)


def render(path, target, ext, elev, azim):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 1400; sc.render.resolution_y = 600
    sc.render.image_settings.file_format = "JPEG"
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "PERSP"; cam.data.lens = 35
    cam.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
    bpy.context.view_layer.update()
    cam.location = target + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * ext
    sc.camera = cam
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


report = []
for setname, idx, foot, z, target in CASES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    a = load(setname, idx, "orig")
    lo, hi = bbox(a); d = hi - lo
    s = foot[0] / d.x if foot[1] else foot[0] / max(d.x, d.y)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, hi.z))
    a.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-c))  # same width, original proportions
    b = load(setname, idx, "game")
    if z[0] == "thick":
        m = Matrix.Diagonal((foot[0] / d.x, foot[1] / d.y, z[1] / d.z, 1))
    else:
        m = Matrix.Diagonal((s, s, s * z[1], 1))
    b.data.transform(m @ Matrix.Translation(-c))
    t0 = tris(b)
    decimate(b, target)
    la, ha = bbox(a); lb, hb = bbox(b)
    gap = (ha.x - la.x) * 0.25
    b.location.x = ha.x - lb.x + gap
    bpy.context.view_layer.update()
    report.append("%s_%02d orig %d tris size %s | game %d tris size %s" % (
        setname, idx, t0, tuple(round(v, 2) for v in (ha - la)), tris(b), tuple(round(v, 2) for v in (hb - lb))))
    lo2, hi2 = bbox(a); lo3, hi3 = bbox(b)
    mid = (Vector((min(lo2.x, lo3.x), min(lo2.y, lo3.y), min(lo2.z, lo3.z))) + Vector((max(hi2.x, hi3.x), max(hi2.y, hi3.y), max(hi2.z, hi3.z)))) / 2
    span = max(hi3.x, hi2.x) - min(lo2.x, lo3.x)
    tag = "%s_%02d" % (setname, idx)
    render(os.path.join(SPLIT, "cmp_%s_iso.jpg" % tag), mid, span * 1.25, 35, -20)
    render(os.path.join(SPLIT, "cmp_%s_low.jpg" % tag), mid + Vector((0, 0, 0)), span * 0.9, 8, -10)
open(os.path.join(SPLIT, "cmp_report.txt"), "w").write("\n".join(report) + "\n")
