# Carousel source probe (2026-10-21): imports Carousel/Body.fbx and Carousel/Horse.fbx, writes sizes, triangle counts,
# materials, and the vertical poles of the body (where horses ride), and renders front / top / iso views.
# blender -b --factory-startup --python cur_carousel_probe.py -- <Source~ dir>
import bpy, sys, os, math, traceback
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Carousel")
out = open(os.path.join(SRC, "probe.txt"), "w")


def log(*a):
    out.write(" ".join(str(x) for x in a) + "\n"); out.flush()


def world_bbox(objs):
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector((min(lo[i], w[i]) for i in range(3))); hi = Vector((max(hi[i], w[i]) for i in range(3)))
    return lo, hi


def render(path, objs, elev, azim, ortho=True):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 900; sc.render.resolution_y = 900
    sc.render.image_settings.file_format = "JPEG"
    lo, hi = world_bbox(objs)
    c = (lo + hi) / 2; ext = (hi - lo).length
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = ext * 1.0
    cam.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
    bpy.context.view_layer.update()
    cam.location = c + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * ext * 3
    cam.data.clip_end = ext * 10
    sc.camera = cam
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


try:
    for name in ("Body", "Horse"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name, name + ".fbx"))
        objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        bpy.context.view_layer.update()
        lo, hi = world_bbox(objs)
        log(name, "objects", [(o.name, o.type, tuple(round(v, 3) for v in o.scale), tuple(round(math.degrees(v), 1) for v in o.rotation_euler)) for o in bpy.context.scene.objects])
        log(name, "bbox", tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi), "size", tuple(round(v, 3) for v in hi - lo))
        for o in objs:
            me = o.data
            log(name, o.name, "tris", sum(len(p.vertices) - 2 for p in me.polygons), "verts", len(me.vertices), "uv", [l.name for l in me.uv_layers],
                "mats", [m.name if m else None for m in me.materials])
        if name == "Body":
            # vertical poles: columns of vertices between 25% and 70% height, clustered on a coarse grid (top view)
            h = hi.z - lo.z
            cells = {}
            for o in objs:
                m = o.matrix_world
                for v in o.data.vertices:
                    w = m @ v.co
                    if lo.z + h * 0.3 < w.z < lo.z + h * 0.65:
                        k = (round(w.x / (h * 0.02)), round(w.y / (h * 0.02)))
                        cells[k] = cells.get(k, 0) + 1
            top = sorted(cells.items(), key=lambda kv: -kv[1])[:60]
            log("Body mid-height vertex cells (grid %.3f):" % (h * 0.02), [(round(k[0] * h * 0.02, 2), round(k[1] * h * 0.02, 2), n) for k, n in top])
        render(os.path.join(SRC, "probe_%s_front.jpg" % name), objs, 0, 0)
        render(os.path.join(SRC, "probe_%s_top.jpg" % name), objs, 90, 0)
        render(os.path.join(SRC, "probe_%s_iso.jpg" % name), objs, 30, -35)
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
out.close()
