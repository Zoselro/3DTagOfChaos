# Roller coaster source probe (2026-10-21, Request1009Plan.md §30): imports Coaster/Entrance, Track (rail + supports in one
# file) and Car, writes objects, sizes, triangle counts, materials and loose islands, renders front / back / side / top / iso.
# blender -b --factory-startup --python cur_coaster_probe.py -- <Source~ dir> [Name,Name,...]
import bpy, bmesh, sys, os, math, traceback
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Coaster")
ONLY = set(argv[1].split(",")) if len(argv) > 1 and argv[1] else None
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


def islands(o):
    bm = bmesh.new(); bm.from_mesh(o.data); bm.verts.ensure_lookup_table()
    seen, parts = set(), []
    for v in bm.verts:
        if v.index in seen: continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            a = stack.pop(); comp.append(a.co.copy())
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen: seen.add(b.index); stack.append(b)
        lo = Vector((min(c.x for c in comp), min(c.y for c in comp), min(c.z for c in comp)))
        hi = Vector((max(c.x for c in comp), max(c.y for c in comp), max(c.z for c in comp)))
        parts.append((len(comp), lo, hi))
    bm.free()
    return parts


def render(path, objs, elev, azim):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 800; sc.render.resolution_y = 800
    sc.render.image_settings.file_format = "JPEG"
    lo, hi = world_bbox(objs)
    c = (lo + hi) / 2; ext = (hi - lo).length
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = ext; cam.data.clip_end = ext * 10
    cam.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
    bpy.context.view_layer.update()
    cam.location = c + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * ext * 3
    sc.camera = cam
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


try:
    for name, sub in (("Entrance", "Entrance"), ("Track", "Track"), ("Support", "Support"), ("Car", "Car")):
        if ONLY and name not in ONLY: continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, sub, name + ".fbx"))
        objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        bpy.context.view_layer.update()
        lo, hi = world_bbox(objs)
        log(name, "all objects", [(o.name, o.type, tuple(round(v, 3) for v in o.scale), tuple(round(math.degrees(v), 1) for v in o.rotation_euler)) for o in bpy.context.scene.objects])
        log(name, "bbox", tuple(round(v, 3) for v in lo), tuple(round(v, 3) for v in hi), "size", tuple(round(v, 3) for v in hi - lo))
        for o in objs:
            me = o.data
            mats = []
            for m in me.materials:
                if m is None: mats.append(None); continue
                imgs = [n.image.name for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image] if m.use_nodes else []
                col = None
                if m.use_nodes and "Principled BSDF" in m.node_tree.nodes:
                    col = tuple(round(x, 2) for x in m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value)
                mats.append((m.name, imgs, col))
            log(name, o.name, "tris", sum(len(p.vertices) - 2 for p in me.polygons), "verts", len(me.vertices), "uv", [l.name for l in me.uv_layers],
                "mats", mats, "colors", [a.name for a in me.color_attributes])
            parts = islands(o)
            log(name, o.name, "islands", len(parts))
            parts.sort(key=lambda p: -p[0])
            for n, a, b in parts[:25]:
                log("   island verts %6d  lo %s  hi %s  size %s" % (n, tuple(round(v, 2) for v in a), tuple(round(v, 2) for v in b), tuple(round(v, 2) for v in b - a)))
        for tag, elev, azim in (("front", 0, 0), ("back", 0, 180), ("side", 0, 90), ("top", 90, 0), ("iso", 25, -35), ("isoback", 25, 145)):
            render(os.path.join(SRC, "probe_%s_%s.jpg" % (name, tag)), objs, elev, azim)
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
out.close()
