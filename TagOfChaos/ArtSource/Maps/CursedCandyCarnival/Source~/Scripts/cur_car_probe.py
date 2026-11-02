# Coaster car probe (2026-10-21, Request1009Plan.md §31 Q2): vertex histogram along the train (x), sizes, materials.
# blender -b --factory-startup --python cur_car_probe.py -- <Source~ dir>
import bpy, sys, os, traceback
argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Coaster")
out = open(os.path.join(SRC, "car_probe.txt"), "w")
def log(*a): out.write(" ".join(str(x) for x in a) + "\n"); out.flush()
try:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Car", "Car.fbx"))
    o = [x for x in bpy.context.scene.objects if x.type == "MESH"][0]
    log("obj", o.name, "loc", tuple(o.location), "rot", tuple(o.rotation_euler), "scale", tuple(o.scale))
    o.data.transform(o.matrix_world); o.matrix_world.identity()
    vs = [v.co for v in o.data.vertices]
    for ax in range(3):
        log("axis", ax, "min %.4f max %.4f" % (min(v[ax] for v in vs), max(v[ax] for v in vs)))
    lo = min(v.x for v in vs); hi = max(v.x for v in vs); nb = 400
    h = [0] * nb
    for v in vs: h[min(nb - 1, int((v.x - lo) / (hi - lo) * nb))] += 1
    log("hist x lo %.4f hi %.4f bins %d" % (lo, hi, nb))
    log(" ".join(str(c) for c in h))
    zlo = min(v.z for v in vs)
    for th in (0.02, 0.04, 0.06):
        hh = [0] * nb
        for v in vs:
            if v.z > zlo + th: hh[min(nb - 1, int((v.x - lo) / (hi - lo) * nb))] += 1
        log("above %.2f:" % th, " ".join(str(c) for c in hh))
    zl = [-1.0] * nb
    for v in vs:
        b = min(nb - 1, int((v.x - lo) / (hi - lo) * nb)); zl[b] = max(zl[b], v.z - zlo)
    log("zmax:", " ".join("%.2f" % z for z in zl))
    log("mats", [m.name for m in o.data.materials], "uv", [l.name for l in o.data.uv_layers], "tris", sum(len(p.vertices) - 2 for p in o.data.polygons))
    with open(os.path.join(SRC, "car_verts.txt"), "w") as fv:
        fv.write(";".join("%.4f,%.4f,%.4f" % (v.x, v.y, v.z) for v in vs))
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
out.close()
