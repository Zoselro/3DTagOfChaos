# Probe interiors / shells: floor height and height profile on a polar grid (after scaling to target width).
import bpy, sys, os, json, math
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]; OUT = os.path.join(SRC, "_split")
JOBS = [("Interior1", 0, 17.0), ("Interior234", 0, 17.0), ("Interior234", 1, 17.0), ("Interior234", 2, 17.0),
        ("MatBuildings", 0, 23.0), ("MatBuildings", 1, 23.0), ("MatBuildings", 2, 23.0), ("MatBuildings", 3, 23.0),
        ("Whimsical", 0, 14.0), ("Whimsical", 1, 16.0), ("Tiles", 4, 16.0), ("GateItems", 6, 9.0), ("GateItems", 7, 9.0)]
res = {}
for setname, idx, width in JOBS:
    bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, setname + ".blend"))
    o = bpy.data.objects["%s_%02d" % (setname, idx)]
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    s = width / max(hi.x - lo.x, hi.y - lo.y)
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    pts = [((v.x - cx) * s, (v.y - cy) * s, (v.z - lo.z) * s) for v in vs]
    # floor: dominant z among up-facing polygons (area weighted)
    hist = {}
    for p in o.data.polygons:
        n = (o.matrix_world.to_3x3() @ p.normal).normalized()
        if n.z < 0.9: continue
        z = round(((o.matrix_world @ p.center).z - lo.z) * s / 0.1) * 0.1
        hist[z] = hist.get(z, 0) + p.area * s * s
    floor = max(hist.items(), key=lambda kv: kv[1])[0] if hist else 0
    # height map on 1m grid: max z per cell
    grid = {}
    for x, y, z in pts:
        k = (math.floor(x), math.floor(y)); grid[k] = max(grid.get(k, -1), z)
    rows = []
    half = int(math.ceil(width / 2)) + 1
    for gy in range(half, -half - 1, -1):
        row = ""
        for gx in range(-half, half + 1):
            z = grid.get((gx, gy))
            row += " ." if z is None else ("%2d" % min(99, round(z - floor)))
        rows.append(row)
    res["%s_%02d" % (setname, idx)] = dict(scale=s, size=[(hi.x - lo.x) * s, (hi.y - lo.y) * s, (hi.z - lo.z) * s], floor=floor, map=rows)
json.dump(res, open(os.path.join(OUT, "probe.json"), "w"), indent=1)
open(os.path.join(OUT, "probe.done"), "w").write("ok")
