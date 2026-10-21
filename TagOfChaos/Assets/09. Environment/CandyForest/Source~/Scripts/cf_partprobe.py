# Loose-part probe (2026-10-21, §28.16): are the bumps on a floor block (candy balls, bushes, lava rim) separate
# mesh islands? Uniform scale like ground_block, then lists islands that sit on the walking face. Writes _split/partprobe.txt.
# blender -b --factory-startup --python cf_partprobe.py -- <Source~ dir>
import bpy, bmesh, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SPLIT = os.path.join(argv[0], "_split")
CASES = [("Tiles", 1, "y"), ("Tiles", 2, "y"), ("GreenFloor", 0, "max")]
out = open(os.path.join(SPLIT, "partprobe.txt"), "w")
for setname, idx, fit in CASES:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(os.path.join(SPLIT, setname + ".blend"), link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n == "%s_%02d" % (setname, idx)]
    o = dst.objects[0]
    bpy.context.scene.collection.objects.link(o)
    vs = [v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    d = hi - lo
    s = 10.0 / (d.y if fit == "y" else max(d.x, d.y))
    o.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    hist = {}
    for p in o.data.polygons:
        if p.normal.z < 0.85: continue
        k = round(p.center.z / 0.1) * 0.1
        hist[k] = hist.get(k, 0.0) + p.area
    face = max(hist.items(), key=lambda kv: kv[1])[0]
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop(); comp.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); stack.append(b)
        zs = [c.co.z for c in comp]
        xs = [c.co.x for c in comp]; ys = [c.co.y for c in comp]
        parts.append((len(comp), min(zs) - face, max(zs) - face, (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, max(xs) - min(xs)))
    parts.sort(key=lambda p: -p[0])
    out.write("%s_%02d face %.2f islands %d\n" % (setname, idx, face, len(parts)))
    for n, zl, zh, cx, cy, w in parts[:40]:
        out.write("  verts %6d z %+.2f..%+.2f at (%.2f, %.2f) w %.2f\n" % (n, zl, zh, cx, cy, w))
    bm.free()
out.write("DONE\n")
out.close()
