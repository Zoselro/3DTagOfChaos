# Coaster entrance profile probe (2026-10-21, §30): walking-surface height along the middle (x ~ 0) from front to back,
# the facade line, the mouth opening (empty columns in front view) and the stair width. Writes Coaster/entrance_probe.txt.
# blender -b --factory-startup --python cur_entrance_probe.py -- <Source~ dir>
import bpy, sys, os, math, traceback
from mathutils import Vector
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Coaster")
out = open(os.path.join(SRC, "entrance_probe.txt"), "w")


def log(*a):
    out.write(" ".join(str(x) for x in a) + "\n"); out.flush()


try:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Entrance", "Entrance.fbx"))
    o = [x for x in bpy.context.scene.objects if x.type == "MESH"][0]
    o.data.transform(o.matrix_world); o.matrix_world.identity()
    vs = [v.co for v in o.data.vertices]
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    log("bbox", tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi))
    bvh = BVHTree.FromObject(o, bpy.context.evaluated_depsgraph_get())
    H = hi.z - lo.z
    # 1) walking surface down the middle: ray from above at x = 0, every 0.02 along y
    prof = []
    y = lo.y
    while y <= hi.y:
        hit = bvh.ray_cast(Vector((0, y, hi.z + 0.1)), Vector((0, 0, -1)))
        prof.append((round(y, 3), round(hit[0].z, 3) if hit[0] else None))
        y += 0.02
    log("top surface at x=0 (y, z):", prof)
    # 2) lowest surface under the mouth: rays from the front (-y) at heights, x = 0 -> where the first hit is (depth of the opening)
    depth = []
    z = lo.z + 0.01
    while z <= lo.z + H * 0.7:
        hit = bvh.ray_cast(Vector((0, lo.y - 0.1, z)), Vector((0, 1, 0)))
        depth.append((round(z, 3), round(hit[0].y, 3) if hit[0] else None))
        z += 0.02
    log("front ray at x=0 (z, first y hit):", depth)
    # 3) same from the back (+y)
    back = []
    z = lo.z + 0.01
    while z <= lo.z + H * 0.7:
        hit = bvh.ray_cast(Vector((0, hi.y + 0.1, z)), Vector((0, -1, 0)))
        back.append((round(z, 3), round(hit[0].y, 3) if hit[0] else None))
        z += 0.02
    log("back ray at x=0 (z, first y hit):", back)
    # 4) opening width: front rays at the mouth height across x
    for zz in (0.2, 0.3, 0.4, 0.5):
        zr = lo.z + H * zz
        row = []
        x = lo.x
        while x <= hi.x:
            hit = bvh.ray_cast(Vector((x, lo.y - 0.1, zr)), Vector((0, 1, 0)))
            row.append((round(x, 2), round(hit[0].y, 2) if hit[0] else None))
            x += 0.04
        log("front ray row z %.3f (x, y hit):" % zr, row)
    # 5) stair width: top surface across x at a few y in front of the facade
    for yy in (0.1, 0.2, 0.3):
        yr = lo.y + (hi.y - lo.y) * yy
        row = []
        x = lo.x
        while x <= hi.x:
            hit = bvh.ray_cast(Vector((x, yr, hi.z + 0.1)), Vector((0, 0, -1)))
            row.append((round(x, 2), round(hit[0].z, 3) if hit[0] else None))
            x += 0.03
        log("top row y %.3f (x, z):" % yr, row)
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
out.close()
