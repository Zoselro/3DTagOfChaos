# Floor tile probe (2026-10-21): with uniform scale, where is the walking face, how high do bumps rise above it,
# and how many triangles sit below it (the part buried in the ground). Writes _split/tileprobe.txt.
# blender -b --factory-startup --python cf_tileprobe.py -- <Source~ dir>
import bpy, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SPLIT = os.path.join(argv[0], "_split")
CASES = [("Tiles", 0, "y", 10.0), ("Tiles", 1, "y", 10.0), ("Tiles", 2, "y", 10.0), ("GreenFloor", 0, "max", 10.0)]
out = open(os.path.join(SPLIT, "tileprobe.txt"), "w")
for setname, idx, axis, size in CASES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(os.path.join(SPLIT, setname + ".blend"), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n == "%s_%02d" % (setname, idx)]
    o = dst.objects[0]
    bpy.context.scene.collection.objects.link(o)
    vs = [v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    d = hi - lo
    s = size / (d.y if axis == "y" else max(d.x, d.y))
    o.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    hist = {}
    for p in o.data.polygons:
        if p.normal.z < 0.85: continue
        k = round(p.center.z / 0.05) * 0.05
        hist[k] = hist.get(k, 0.0) + p.area
    face = max(hist.items(), key=lambda kv: kv[1])[0]
    above = sorted(p.center.z - face for p in o.data.polygons)
    top = max(v.co.z for v in o.data.vertices) - face
    total = len(o.data.polygons)
    below = sum(1 for p in o.data.polygons if max(o.data.vertices[i].co.z for i in p.vertices) < face - 0.6)
    bumps = {h: sum(p.area for p in o.data.polygons if p.center.z - face > h) for h in (0.2, 0.5, 1.0, 1.5)}
    hz = sum(1 for z in above if z > 0.3)
    out.write("%s_%02d scale %.2f size %s face z %.2f (thickness %.2f) top +%.2f | polys %d, fully >0.6 below face %d | area above +0.2/0.5/1.0/1.5: %s\n" % (
        setname, idx, s, tuple(round(v * s, 2) for v in d), face, face, top, total, below,
        "/".join("%.1f" % bumps[h] for h in (0.2, 0.5, 1.0, 1.5))))
out.write("DONE\n")
out.close()
